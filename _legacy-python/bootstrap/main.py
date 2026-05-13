#!/usr/bin/env python3
import sys
import os
import urllib.request
import urllib.error
import json
import subprocess
import tempfile
import shutil

REPO = "jjiij/lastchaos-client-launcher"
API_URL = f"https://api.github.com/repos/{REPO}/releases/latest"

def log(msg):
    print(f"[BOOTSTRAP] {msg}")
    sys.stdout.flush()

def main():
    log("Starting bootstrapper")
    try:
        log("Fetching release info...")
        req = urllib.request.Request(API_URL, headers={
            "User-Agent": "LastChaos-Bootstrap",
            "Accept": "application/vnd.github+json"
        })
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.loads(resp.read())
            version = data.get("tag_name", "unknown")
            log(f"Latest version: {version}")

        download_url = None
        for asset in data.get("assets", []):
            name = asset.get("name", "")
            if name.endswith(".zip") and "windows" in name.lower():
                download_url = asset.get("browser_download_url")
                break

        if not download_url:
            log("No Windows asset found")
            sys.exit(1)

        log(f"Downloading: {download_url}")
        tmpdir = tempfile.mkdtemp()
        zip_path = os.path.join(tmpdir, "launcher.zip")
        urllib.request.urlretrieve(download_url, zip_path)

        log("Extracting...")
        import zipfile
        with zipfile.ZipFile(zip_path, 'r') as zf:
            zf.extractall(tmpdir)

        os.remove(zip_path)

        exe_path = None
        for root, dirs, files in os.walk(tmpdir):
            for f in files:
                if f.endswith(".exe"):
                    exe_path = os.path.join(root, f)
                    break

        if not exe_path:
            log("No exe found in zip")
            sys.exit(1)

        log(f"Launching: {exe_path}")
        subprocess.Popen(exe_path, cwd=os.path.dirname(exe_path))
        log("Launched, exiting")
        shutil.rmtree(tmpdir)
        sys.exit(0)

    except Exception as e:
        log(f"Error: {e}")
        input("Press Enter to exit...")
        sys.exit(1)

if __name__ == "__main__":
    main()