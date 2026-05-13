#!/usr/bin/env python3
import os
import re
import shutil
import subprocess
import time
import urllib.request
import json
import zipfile

CLIENT_REPO = "jjiij/lastchaos-client"
ASSETS_REPO = "jjiij/lastchaos-client-assets"
API_URL = "https://api.github.com/repos"
ASSETS_BRANCH = "main"


def log(msg):
    print(f"[DOWNLOADER] {msg}")


def get_install_path():
    return os.path.dirname(os.path.abspath(__file__))


def get_platform():
    import platform

    system = platform.system().lower()
    if system == "windows":
        return "windows"
    return system


def get_json(url):
    req = urllib.request.Request(url, headers={"User-Agent": "LastChaos-Downloader"})
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read())


def get_latest_release(repo):
    try:
        data = get_json(f"{API_URL}/{repo}/releases")
        if isinstance(data, list) and data:
            return data[0]
    except Exception as e:
        log(f"release list lookup failed for {repo}: {e}")

    return None


def download_file(url, path, callback=None):
    def reporthook(block, bs, size):
        if callback:
            callback(block, bs, size)

    urllib.request.urlretrieve(url, path, reporthook=reporthook)


def get_remote_size(url):
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "LastChaos-Downloader"})
    with urllib.request.urlopen(req, timeout=60) as resp:
        length = resp.headers.get("Content-Length")
        return int(length) if length else None


def supports_range(url):
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "LastChaos-Downloader"})
    with urllib.request.urlopen(req, timeout=60) as resp:
        return "bytes" in (resp.headers.get("Accept-Ranges") or "").lower()


def download_file_resumable(url, path):
    part_path = f"{path}.part"
    downloaded = os.path.getsize(part_path) if os.path.exists(part_path) else 0
    remote_size = get_remote_size(url)
    can_resume = supports_range(url)

    if remote_size and downloaded == remote_size:
        os.replace(part_path, path)
        return

    headers = {"User-Agent": "LastChaos-Downloader"}
    mode = "wb"
    if can_resume and downloaded > 0:
        headers["Range"] = f"bytes={downloaded}-"
        mode = "ab"
        print(f"Resuming download at {downloaded / 1024 / 1024:.1f} MB")
    elif downloaded > 0:
        os.remove(part_path)
        downloaded = 0

    req = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(req, timeout=300) as resp, open(part_path, mode) as out:
        chunk_size = 1024 * 1024
        last_time = time.time()
        last_bytes = downloaded

        while True:
            chunk = resp.read(chunk_size)
            if not chunk:
                break
            out.write(chunk)
            downloaded += len(chunk)

            now = time.time()
            if now - last_time >= 0.5:
                speed = (downloaded - last_bytes) / (now - last_time) / 1024
                if remote_size:
                    pct = downloaded / remote_size * 100
                    print(f"  {pct:.1f}% ({speed:.0f} KB/s)")
                else:
                    print(f"  {downloaded / 1024 / 1024:.1f} MB ({speed:.0f} KB/s)")
                last_time = now
                last_bytes = downloaded

    if remote_size and downloaded != remote_size:
        raise RuntimeError(
            f"Incomplete download: expected {remote_size} bytes, got {downloaded} bytes."
        )

    os.replace(part_path, path)


def extract_zip(path, destination):
    with zipfile.ZipFile(path, "r") as zf:
        zf.extractall(destination)


def merge_tree(src, dst):
    for item in os.listdir(src):
        src_path = os.path.join(src, item)
        dst_path = os.path.join(dst, item)
        if os.path.isdir(src_path):
            shutil.copytree(src_path, dst_path, dirs_exist_ok=True)
        else:
            os.makedirs(os.path.dirname(dst_path), exist_ok=True)
            shutil.copy2(src_path, dst_path)


def pick_game_assets(release_assets, platform_name):
    normalized = []
    for asset in release_assets:
        name = asset.get("name", "")
        lower = name.lower()
        normalized.append(
            {
                "name": name,
                "lower": lower,
                "url": asset.get("browser_download_url"),
                "size": asset.get("size", 0),
            }
        )

    split_parts = [
        a
        for a in normalized
        if a["url"]
        and (
            ".part" in a["lower"]
            or re.search(r"\.zip\.\d+$", a["lower"])
            or re.search(r"\.z\d+$", a["lower"])
        )
        and (platform_name in a["lower"] or "win" in a["lower"])
    ]
    if not split_parts:
        split_parts = [
            a
            for a in normalized
            if a["url"]
            and (
                ".part" in a["lower"]
                or re.search(r"\.zip\.\d+$", a["lower"])
                or re.search(r"\.z\d+$", a["lower"])
            )
        ]

    if split_parts:
        split_parts.sort(key=lambda a: a["lower"])
        return {"type": "parts", "assets": split_parts}

    zip_candidates = [a for a in normalized if a["url"] and a["lower"].endswith(".zip")]
    if zip_candidates:
        best = [a for a in zip_candidates if platform_name in a["lower"] or "win" in a["lower"]]
        selected = best[0] if best else zip_candidates[0]
        return {"type": "zip", "assets": [selected]}

    return {"type": None, "assets": []}


def install_game_from_release(install_path, version_file):
    log("Getting client release info...")
    release = get_latest_release(CLIENT_REPO)
    if not release:
        print("Failed to fetch latest game release metadata.")
        return False

    new_client_ver = release.get("tag_name") or release.get("name") or "unknown"
    selected = pick_game_assets(release.get("assets", []), get_platform())
    assets = selected["assets"]

    if not assets:
        print("No downloadable game artifacts found in latest release.")
        return False

    total_size = sum(a["size"] for a in assets)
    print(f"Game release: {new_client_ver}")
    print(f"Artifacts: {len(assets)}")
    print(f"Total size: {total_size / 1024 / 1024:.1f} MB")

    downloaded = 0
    temp_files = []

    for i, asset in enumerate(assets):
        part_path = os.path.join(install_path, asset["name"])
        temp_files.append(part_path)

        if os.path.exists(part_path) and os.path.getsize(part_path) == asset["size"]:
            print(f"Artifact {i + 1} already exists, skipping download...")
            downloaded += asset["size"]
            continue

        print(f"Downloading artifact {i + 1}/{len(assets)}: {asset['name']}")
        last_time = time.time()
        last_bytes = 0

        def hook(block, bs, _total):
            nonlocal last_time, last_bytes
            current = block * bs
            total_downloaded = downloaded + current
            now = time.time()
            if now - last_time > 0.5:
                speed = (current - last_bytes) / (now - last_time) / 1024
                pct = (total_downloaded / total_size * 100) if total_size else 0
                print(f"  {pct:.1f}% ({speed:.0f} KB/s)")
                last_time = now
                last_bytes = current

        try:
            download_file(asset["url"], part_path, hook)
            downloaded += asset["size"]
        except Exception as e:
            print(f"Error downloading {asset['name']}: {e}")
            return False

    combined_zip = os.path.join(install_path, "game.zip")

    try:
        if selected["type"] == "parts":
            print("Combining split artifacts...")
            with open(combined_zip, "wb") as out:
                for file_path in temp_files:
                    with open(file_path, "rb") as inp:
                        while True:
                            chunk = inp.read(1024 * 1024)
                            if not chunk:
                                break
                            out.write(chunk)
            for file_path in temp_files:
                if os.path.exists(file_path):
                    os.remove(file_path)
        else:
            shutil.move(temp_files[0], combined_zip)

        print("Extracting game files...")
        extract_zip(combined_zip, install_path)
    finally:
        if os.path.exists(combined_zip):
            os.remove(combined_zip)

    with open(version_file, "w", encoding="utf-8") as f:
        f.write(new_client_ver)

    print("Game files updated!")
    return True


def install_assets(install_path, assets_version_file):
    zip_url = f"https://github.com/{ASSETS_REPO}/archive/refs/heads/{ASSETS_BRANCH}.zip"
    zip_path = os.path.join(install_path, f"{ASSETS_REPO.split('/')[1]}-{ASSETS_BRANCH}.zip")
    extract_dir = os.path.join(install_path, "_assets_extract")

    print("Downloading asset archive (resumable)...")
    download_file_resumable(zip_url, zip_path)

    if os.path.exists(extract_dir):
        shutil.rmtree(extract_dir, ignore_errors=True)
    os.makedirs(extract_dir, exist_ok=True)

    print("Extracting assets...")
    extract_zip(zip_path, extract_dir)

    extracted_root = os.path.join(extract_dir, f"{ASSETS_REPO.split('/')[1]}-{ASSETS_BRANCH}")
    if not os.path.exists(extracted_root):
        candidates = [d for d in os.listdir(extract_dir) if os.path.isdir(os.path.join(extract_dir, d))]
        raise RuntimeError(f"Assets archive has unexpected structure: {candidates}")

    print("Merging assets into game folder...")
    merge_tree(extracted_root, install_path)

    asset_ver = "main"
    try:
        release = get_latest_release(ASSETS_REPO)
        if release:
            asset_ver = release.get("tag_name") or release.get("name") or "main"
    except Exception:
        asset_ver = "main"

    with open(assets_version_file, "w", encoding="utf-8") as f:
        f.write(asset_ver)

    shutil.rmtree(extract_dir, ignore_errors=True)
    print("Assets merged!")
    return True


def main():
    log("Starting LastChaos Downloader")
    install_path = get_install_path()

    version_file = os.path.join(install_path, ".client_version")
    assets_version_file = os.path.join(install_path, ".assets_version")

    client_ver = None
    assets_ver = None

    if os.path.exists(version_file):
        with open(version_file, "r", encoding="utf-8") as f:
            client_ver = f.read().strip()
    if os.path.exists(assets_version_file):
        with open(assets_version_file, "r", encoding="utf-8") as f:
            assets_ver = f.read().strip()

    log(f"Install path: {install_path}")
    log(f"Client version: {client_ver}")
    log(f"Assets version: {assets_ver}")

    print("\n=== LastChaos Launcher ===")
    print("1. Download/Update Game + Assets")
    print("2. Download/Update Assets Only")
    print("3. Launch Game (LC.exe)")
    print("4. Exit")
    print("========================")

    choice = input("Select option: ").strip()

    if choice == "1":
        confirm = input("Download latest game release and merge required assets? (y/n): ").lower()
        if confirm != "y":
            return

        if not install_game_from_release(install_path, version_file):
            return

        try:
            install_assets(install_path, assets_version_file)
            print("Full game setup complete.")
        except Exception as e:
            print(f"Game files installed, but assets step failed: {e}")

    elif choice == "2":
        try:
            install_assets(install_path, assets_version_file)
        except Exception as e:
            print(f"Assets update failed: {e}")

    elif choice == "3":
        lc_exe = os.path.join(install_path, "LC.exe")
        if os.path.exists(lc_exe):
            subprocess.Popen(lc_exe, cwd=install_path)
            log("Launched LC.exe")
        else:
            print("LC.exe not found - run option 1 first")

    elif choice == "4":
        print("Bye!")
    else:
        print("Invalid option")


if __name__ == "__main__":
    main()
