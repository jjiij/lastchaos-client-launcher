#!/usr/bin/env python3
import sys
import os
import zipfile
import subprocess
import urllib.request
import json
import time

CLIENT_REPO = "jjiij/lastchaos-client"
ASSETS_REPO = "jjiij/lastchaos-client-assets"
API_URL = "https://api.github.com/repos"

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

def get_latest_release(repo):
    url = f"{API_URL}/{repo}/releases"
    req = urllib.request.Request(url, headers={"User-Agent": "LastChaos-Downloader"})
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.loads(resp.read())
            if data:
                return data[0]
    except Exception as e:
        log(f"Error: {e}")
    return None

def download_file(url, path, callback=None):
    def reporthook(block, bs, size):
        if callback:
            callback(block, bs, size)
    
    urllib.request.urlretrieve(url, path, reporthook=reporthook)

def main():
    log("Starting LastChaos Downloader")
    install_path = get_install_path()
    
    version_file = os.path.join(install_path, ".client_version")
    assets_version_file = os.path.join(install_path, ".assets_version")
    
    client_ver = None
    assets_ver = None
    
    if os.path.exists(version_file):
        with open(version_file, 'r') as f:
            client_ver = f.read().strip()
    if os.path.exists(assets_version_file):
        with open(assets_version_file, 'r') as f:
            assets_ver = f.read().strip()
    
    log(f"Install path: {install_path}")
    log(f"Client version: {client_ver}")
    log(f"Assets version: {assets_ver}")
    
    print("\n=== LastChaos Launcher ===")
    print("1. Download/Update Game")
    print("2. Download Assets")  
    print("3. Launch Game (LC.exe)")
    print("4. Exit")
    print("========================")
    
    choice = input("Select option: ").strip()
    
    if choice == "1":
        log("Getting client release info...")
        release = get_latest_release(CLIENT_REPO)
        if not release:
            log("Failed to get client release")
            return
            
        new_client_ver = release.get("tag_name", "")
        assets = release.get("assets", [])
        
        part_urls = []
        total_size = 0
        for asset in assets:
            name = asset.get("name", "").lower()
            if ".part" in name and get_platform() in name:
                part_urls.append((name, asset.get("browser_download_url"), asset.get("size", 0)))
                total_size += asset.get("size", 0)
        
        if not part_urls:
            log("No client files found")
            return
            
        print(f"Client: {new_client_ver}")
        print(f"Parts: {len(part_urls)}")
        print(f"Total size: {total_size / 1024 / 1024:.1f} MB")
        
        confirm = input("Download? (y/n): ").lower()
        if confirm != 'y':
            return
            
        temp_parts = []
        downloaded = 0
        
        for i, (name, url, size) in enumerate(part_urls):
            part_path = os.path.join(install_path, name)
            temp_parts.append(part_path)
            
            if os.path.exists(part_path) and os.path.getsize(part_path) == size:
                print(f"Part {i+1} already exists, skipping...")
                downloaded += size
                continue
                
            print(f"Downloading part {i+1}/{len(part_urls)}...")
            
            last_time = time.time()
            last_bytes = 0
            
            def make_hook(idx):
                def hook(block, bs, total):
                    nonlocal last_time, last_bytes
                    current = block * bs
                    total_downloaded = downloaded + current
                    now = time.time()
                    if now - last_time > 0.5:
                        speed = (current - last_bytes) / (now - last_time) / 1024
                        pct = total_downloaded / total_size * 100
                        print(f"  {pct:.1f}% ({speed:.0f} KB/s)")
                        last_time = now
                        last_bytes = current
                return hook
            
            try:
                download_file(url, part_path, make_hook(i))
                downloaded += size
            except Exception as e:
                print(f"Error downloading: {e}")
                return
        
        print("Combining parts...")
        combined_path = os.path.join(install_path, "game.zip")
        with open(combined_path, 'wb') as out:
            for part_path in temp_parts:
                with open(part_path, 'rb') as inp:
                    while True:
                        chunk = inp.read(1024 * 1024)
                        if not chunk:
                            break
                        out.write(chunk)
                os.remove(part_path)
        
        print("Extracting...")
        with zipfile.ZipFile(combined_path, 'r') as zf:
            zf.extractall(install_path)
        os.remove(combined_path)
        
        with open(version_file, 'w') as f:
            f.write(new_client_ver)
            
        print("Client downloaded!")
        
    elif choice == "2":
        log("Getting assets...")
        release = get_latest_release(ASSETS_REPO)
        
        zip_url = f"https://github.com/{ASSETS_REPO}/archive/refs/heads/main.zip"
        zip_path = os.path.join(install_path, "assets.zip")
        
        print("Downloading assets...")
        download_file(zip_url, zip_path)
        
        print("Extracting...")
        with zipfile.ZipFile(zip_path, 'r') as zf:
            zf.extractall(install_path)
        
        # Move contents up
        assets_folder = os.path.join(install_path, f"{ASSETS_REPO.split('/')[1]}-main")
        if os.path.exists(assets_folder):
            for item in os.listdir(assets_folder):
                src = os.path.join(assets_folder, item)
                dst = os.path.join(install_path, item)
                if not os.path.exists(dst):
                    os.rename(src, dst)
            os.rmdir(assets_folder)
        
        os.remove(zip_path)
        
        with open(assets_version_file, 'w') as f:
            f.write("main")
            
        print("Assets downloaded!")
        
    elif choice == "3":
        lc_exe = os.path.join(install_path, "LC.exe")
        if os.path.exists(lc_exe):
            subprocess.Popen(lc_exe, cwd=install_path)
            log("Launched LC.exe")
        else:
            print("LC.exe not found - download game first")
    
    elif choice == "4":
        print("Bye!")
    else:
        print("Invalid option")

if __name__ == "__main__":
    main()