#!/usr/bin/env python3
import sys
import os
import zipfile
import threading
import traceback
import time
import shutil

DEBUG = True

def log(msg):
    print(f"[LAUNCHER] {msg}")
    sys.stdout.flush()

log(f"Starting - Python {sys.version}")

def get_resource_path(filename):
    if getattr(sys, 'frozen', False):
        base = sys._MEIPASS
    else:
        base = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base, filename)

def get_install_path():
    if getattr(sys, 'frozen', False):
        return os.path.dirname(sys.executable)
    return os.path.dirname(os.path.abspath(__file__))

try:
    from tkinter import Tk, Label, Button, StringVar, messagebox, Frame, Canvas
    from PIL import Image, ImageTk
    import urllib.request
    import urllib.error
    import json
    log("tkinter imported successfully")
except Exception as e:
    log(f"ERROR importing: {e}")
    traceback.print_exc()
    input("Press Enter to exit...")
    sys.exit(1)

CLIENT_REPO = "jjiij/lastchaos-client"
ASSETS_REPO = "jjiij/lastchaos-client-assets"
LAUNCHER_REPO = "jjiij/lastchaos-client-launcher"
API_URL = "https://api.github.com/repos"

COLOR_BG = "#0d0d0d"
COLOR_ACCENT = "#e94560"
COLOR_TEXT = "#ffffff"

def get_platform():
    import platform
    system = platform.system().lower()
    if system == "windows":
        return "windows"
    elif system == "darwin":
        return "macos"
    elif system == "linux":
        return "linux"
    return system


class Launcher:
    def __init__(self, window):
        log("Launcher.__init__ started")
        self.window = window
        self.install_path = get_install_path()
        log(f"Install path: {self.install_path}")
        self.download_in_progress = False
        self.bg_image = None
        self.downloaded = 0
        self.total_size = 0
        self.update_in_progress = False
        self.update_cancelled = False
        self.client_downloaded = 0
        self.assets_downloaded = 0
        self.total_client_size = 0
        self.total_assets_size = 0
        self.client_download_urls = []
        self.assets_download_url = None
        self._resize_timer = None
        self._bg_update_pending = False
        self.client_version = None

        window.title("LastChaos")
        window.geometry("800x600")
        window.resizable(True, True)
        window.configure(bg=COLOR_BG)

        self.status_var = StringVar(value="Ready")

        log("Setting up UI...")
        self.setup_ui()
        self.check_installed()
        log("Launcher init complete")

    def setup_ui(self):
        log("setup_ui called")

        bg_path = get_resource_path("background.jpg")
        self.bg_label = None
        if os.path.exists(bg_path):
            try:
                self.bg_original = Image.open(bg_path)
                log(f"Background loaded: {self.bg_original.size}")
                self._update_background_image()
            except Exception as e:
                log(f"Failed to load background: {e}")
                Frame(self.window, bg=COLOR_BG).place(x=0, y=0, relwidth=1, relheight=1)
        else:
            log("background not found")
            Frame(self.window, bg=COLOR_BG).place(x=0, y=0, relwidth=1, relheight=1)

        self.play_btn = Button(
            self.window,
            text="PLAY",
            font=("Georgia", 20, "bold italic"),
            bg="#5D4037",
            fg="#FFD700",
            activebackground="#6D4C41",
            activeforeground="#FFEB3B",
            bd=0,
            cursor="hand2",
            command=self.launch_game,
            highlightthickness=0,
            relief="flat"
        )
        self.play_btn.config(bg="#5D4037", activebackground="#6D4C41")
        self.play_btn.bind("<Enter>", lambda e: self.play_btn.config(bg="#6D4C41"))
        self.play_btn.bind("<Leave>", lambda e: self.play_btn.config(bg="#5D4037"))
        self.play_btn.place(relx=0.98, rely=0.95, anchor="se")
        self.play_btn.update_idletasks()
        min_w = max(100, int(self.play_btn.winfo_width()))
        min_h = max(60, int(self.play_btn.winfo_height()))
        self.play_btn.place_configure(width=min_w, height=min_h)

        self.update_frame = Frame(self.window, bg="#1a1a2e", bd=0)
        self.update_frame.place(relx=0.5, rely=0.5, anchor="center", width=400, height=200)
        self.update_frame.lower()

        Label(self.update_frame, text="Updating Launcher...", font=("Arial", 16, "bold"),
              bg="#1a1a2e", fg=COLOR_TEXT).pack(pady=(20, 10))

        self.update_var = StringVar(value="Checking for updates...")
        Label(self.update_frame, textvariable=self.update_var,
              font=("Arial", 12), bg="#1a1a2e", fg=COLOR_TEXT).pack()

        self.update_progress_canvas = Canvas(self.update_frame, bg="#0d0d15", highlightthickness=0, height=20, width=360)
        self.update_progress_canvas.pack(pady=15)
        self.update_progress_rect = self.update_progress_canvas.create_rectangle(0, 0, 0, 20, fill="#3498db", outline="")

        self.update_close_btn = Button(self.update_frame, text="Cancel", font=("Arial", 10),
                            bg="#e74c3c", fg=COLOR_TEXT, bd=0, padx=20, pady=5,
                            command=self.cancel_update)
        self.update_close_btn.pack(pady=10)

        self.update_btn = Button(
            self.window,
            text="UPDATE",
            font=("Arial", 12, "bold"),
            bg="#3498db",
            fg=COLOR_TEXT,
            activebackground="#5dade2",
            activeforeground="white",
            bd=0,
            padx=15,
            pady=8,
            cursor="hand2",
            command=self.version_check
        )
        self.update_btn.bind("<Enter>", lambda e: self.update_btn.config(bg="#5dade2"))
        self.update_btn.bind("<Leave>", lambda e: self.update_btn.config(bg="#3498db"))
        self.update_btn.place(relx=0.02, rely=0.95, anchor="sw")
        self.update_btn.lower()

        self.status_frame = Frame(self.window, bg="#000000")
        self.status_frame.place(relx=0, rely=0.95, relwidth=1, relheight=0.05)

        self.progress_canvas = Canvas(self.status_frame, bg="#1a1a1a", highlightthickness=0, height=20)
        self.progress_canvas.pack(fill="x", expand=True)
        self.progress_canvas.update_idletasks()
        self.progress_rect = self.progress_canvas.create_rectangle(0, 0, 0, 20, fill="#1b5e20", outline="")

        self.status_label = Label(
            self.progress_canvas,
            textvariable=self.status_var,
            font=("Arial", 11),
            bg="#1a1a1a",
            fg=COLOR_TEXT,
            padx=10,
            pady=2
        )
        self.status_label.place(relx=0.5, rely=0.5, anchor="center")

        self.window.bind("<Configure>", self.on_resize)

    def _update_background_image(self):
        if not hasattr(self, 'bg_original'):
            return
        self.window.update_idletasks()
        win_w = self.window.winfo_width()
        win_h = self.window.winfo_height()
        if win_w < 100:
            win_w, win_h = 800, 600

        orig_w, orig_h = self.bg_original.size
        scale = max(win_w / orig_w, win_h / orig_h)
        new_w = int(orig_w * scale)
        new_h = int(orig_h * scale)

        resized = self.bg_original.resize((new_w, new_h), Image.Resampling.LANCZOS)

        left = (new_w - win_w) // 2
        top = (new_h - win_h) // 2
        right = left + win_w
        bottom = top + win_h
        cropped = resized.crop((left, top, right, bottom))

        self.bg_image = ImageTk.PhotoImage(cropped)
        if self.bg_label is None:
            self.bg_label = Label(self.window, image=self.bg_image)
            self.bg_label.place(x=0, y=0, relwidth=1, relheight=1)
        else:
            self.bg_label.config(image=self.bg_image)

    def on_resize(self, event):
        if self._resize_timer:
            self.window.after_cancel(self._resize_timer)
        self._resize_timer = self.window.after(150, self._do_resize)

    def _do_resize(self):
        if self._bg_update_pending:
            return
        self._bg_update_pending = True

        def do_bg():
            try:
                w = self.window.winfo_width()
                h = self.window.winfo_height()
                if w > 0 and h > 0:
                    self._update_background_image()
                    play_size = max(14, int(h * 0.04))
                    min_w = max(100, int(w * 0.08))
                    min_h = max(60, int(h * 0.06))
                    self.play_btn.place_configure(width=min_w, height=min_h)
                    self.play_btn.config(font=("Impact", play_size, "bold"))
                    status_size = max(10, int(h * 0.02))
                    self.status_label.config(font=("Arial", status_size))
                    total = self.total_client_size + self.total_assets_size
                    if total > 0:
                        downloaded = self.client_downloaded + self.assets_downloaded
                        pct = min(1.0, downloaded / total)
                        self.progress_canvas.coords(self.progress_rect, 0, 0, int(w * pct), 20)
                        if self.download_in_progress:
                            pct_int = int(pct * 100)
                            self.play_btn.config(text=f"{pct_int}%")
            finally:
                self._bg_update_pending = False

        self.window.after(0, do_bg)

    def check_installed(self):
        log("check_installed started")
        self.update_btn.lower()

        def check():
            current_c = self.get_local_version("client")
            current_a = self.get_local_version("assets")
            latest_l = self.get_github_version(LAUNCHER_REPO)
            current_l = self.get_local_version("launcher")

            self.window.after(0, lambda: self._update_status(current_c, current_a, current_l, latest_l))
            log("Check complete")

        threading.Thread(target=check, daemon=True).start()

    def version_check(self):
        log("version_check clicked")
        self.update_cancelled = False

        def check():
            current_l = self.get_local_version("launcher")
            latest_l = self.get_github_version(LAUNCHER_REPO)

            has_update = current_l != latest_l and latest_l not in (None, "error", "unknown")
            if has_update:
                log(f"Update available: {current_l} -> {latest_l}")
                self.window.after(0, self.prompt_update)
            log("Update check done")

        threading.Thread(target=check, daemon=True).start()

    def prompt_update(self):
        self.update_frame.lift()
        self.update_var.set("New version available! Downloading...")

        self.update_in_progress = True
        self.update_progress_canvas.coords(self.update_progress_rect, 0, 0, 0, 20)

        def do_update():
            try:
                url = f"{API_URL}/{LAUNCHER_REPO}/releases"
                req = urllib.request.Request(url, headers={"User-Agent": "LastChaos-Launcher"})
                with urllib.request.urlopen(req, timeout=30) as resp:
                    data = json.loads(resp.read())
                    if data and len(data) > 0:
                        release = data[0]
                        version = release.get("tag_name", "")
                        assets = release.get("assets", [])
                    else:
                        version = ""
                        assets = []

                download_url = None
                current_platform = get_platform()
                for asset in assets:
                    name = asset.get("name", "").lower()
                    if name.endswith(".zip") and current_platform in name:
                        download_url = asset.get("browser_download_url")
                        break

                if not download_url:
                    self.window.after(0, lambda: self.update_var.set(f"No {current_platform} release found"))
                    return

                self.window.after(0, lambda: self.update_var.set(f"Downloading {version}..."))

                zip_path = os.path.join(self.install_path, "launcher_update.zip")
                urllib.request.urlretrieve(download_url, zip_path)

                if self.update_cancelled:
                    os.remove(zip_path)
                    return

                self.window.after(0, lambda: self.update_var.set("Extracting..."))

                with zipfile.ZipFile(zip_path, 'r') as zf:
                    zf.extractall(self.install_path)

                os.remove(zip_path)
                self.save_version("launcher", version)
                log(f"Updated to {version}")

                self.window.after(0, lambda: self.update_var.set(f"Updated to {version}! Restart to apply."))
                self.window.after(0, lambda: self.update_close_btn.config(text="OK", command=self.close_update_frame))

            except Exception as e:
                log(f"Update error: {e}")
                self.window.after(0, lambda msg=str(e): self.update_var.set(f"Error: {msg}"))
                self.window.after(0, lambda: self.update_close_btn.config(text="Close", command=self.cancel_update))

        threading.Thread(target=do_update, daemon=True).start()

    def cancel_update(self):
        self.update_cancelled = True
        self.update_in_progress = False
        self.update_frame.lower()

    def close_update_frame(self):
        self.update_in_progress = False
        self.update_frame.lower()
        exe_path = os.path.join(self.install_path, "LastChaosLauncher.exe")
        import subprocess
        subprocess.Popen(f'"{exe_path}"', shell=True, cwd=self.install_path)
        self.window.destroy()

    def _update_status(self, client_ver, assets_ver, launcher_ver=None, latest_launcher=None):
        show_update = False
        if latest_launcher and latest_launcher not in ("error", "unknown"):
            if launcher_ver and launcher_ver not in ("Not installed", "error", "unknown"):
                if launcher_ver != latest_launcher:
                    show_update = True
            elif not launcher_ver or launcher_ver == "Not installed":
                pass

        if show_update:
            self.update_btn.lift()
            self.update_btn.configure(state="normal")
        else:
            self.update_btn.lower()
            self.update_btn.configure(state="disabled")

        can_play = client_ver != "Not installed"
        needs_assets = assets_ver == "Not installed"

        if client_ver == "Not installed":
            self.play_btn.config(text="Download", state="normal", command=self.download_game)
            self.status_var.set("Click Download to get game files")
        elif needs_assets:
            self.play_btn.config(text="Download Assets", state="normal", command=self.download_assets)
            self.status_var.set(f"Game ready ({client_ver}) - Need assets")
        elif self.download_in_progress:
            pass
        else:
            self.play_btn.config(text="Play", state="normal", command=self.launch_game)
            self.status_var.set("Ready to play!")

    def get_github_version(self, repo):
        try:
            url = f"{API_URL}/{repo}/releases"
            req = urllib.request.Request(url, headers={
                "User-Agent": "LastChaos-Launcher",
                "Accept": "application/vnd.github+json"
            })
            with urllib.request.urlopen(req, timeout=15) as resp:
                data = json.loads(resp.read())
                if data and len(data) > 0:
                    return data[0].get("tag_name", "unknown")
                return "unknown"
        except Exception as e:
            log(f"Error getting version for {repo}: {e}")
            return "error"

    def get_local_version(self, component):
        ver_file = os.path.join(self.install_path, f".{component}_version")
        if os.path.exists(ver_file):
            with open(ver_file, 'r') as f:
                return f.read().strip()
        return "Not installed"

    def save_version(self, component, version):
        ver_file = os.path.join(self.install_path, f".{component}_version")
        with open(ver_file, 'w') as f:
            f.write(version)

    def download_game(self):
        log(">>> download_game CLICKED")
        if self.download_in_progress:
            log("Already downloading")
            return

        self.download_in_progress = True
        self.client_downloaded = 0
        self.assets_downloaded = 0
        self.total_client_size = 0
        self.total_assets_size = 0
        self.play_btn.config(text="...", state="disabled")
        self.status_var.set("Fetching release info...")

        def do_download():
            try:
                client_url = f"{API_URL}/{CLIENT_REPO}/releases"
                req = urllib.request.Request(client_url, headers={
                    "User-Agent": "LastChaos-Launcher",
                    "Accept": "application/vnd.github+json"
                })
                with urllib.request.urlopen(req, timeout=30) as resp:
                    data = json.loads(resp.read())
                    if data:
                        release = data[0]
                        self.client_version = release.get("tag_name", "")
                        log(f"Client release: {self.client_version}")
                        log(f"Available assets: {[a.get('name') for a in release.get('assets', [])]}")

                        self.client_download_urls = []
                        self.total_client_size = 0

                        assets = release.get("assets", [])
                        sorted_assets = sorted(assets, key=lambda a: a.get("name", ""))

                        for asset in sorted_assets:
                            name = asset.get("name", "").lower()
                            if ".part" in name and "windows" in name:
                                url = asset.get("browser_download_url")
                                size = asset.get("size", 0)
                                self.client_download_urls.append((name, url, size))
                                self.total_client_size += size
                                log(f"Part: {name} ({size} bytes)")

                        if not self.client_download_urls:
                            self.window.after(0, lambda: self.handle_error("No Windows client files found"))
                            return

                log(f"Total download size: {self.format_size(self.total_client_size)}")
                self.window.after(0, lambda: self.status_var.set("Downloading client..."))

                temp_parts = []
                for i, (name, url, size) in enumerate(self.client_download_urls):
                    part_path = os.path.join(self.install_path, name)
                    temp_parts.append(part_path)

                    exists = os.path.exists(part_path)
                    file_size = os.path.getsize(part_path) if exists else 0
                    if exists and file_size == size:
                        log(f"Part {i+1} already complete, skipping")
                        self.client_downloaded += size
                        continue

                    log(f"Downloading part {i+1}/{len(self.client_download_urls)}: {name}")
                    self.window.after(0, lambda n=name, i=i: self.status_var.set(f"Downloading {n}..."))

                    last_time = time.time()
                    last_bytes = 0

                    def make_hook(part_idx):
                        def hook(block, bs, total):
                            nonlocal last_time, last_bytes
                            now = time.time()
                            current_bytes = block * bs
                            self.client_downloaded = current_bytes
                            for j in range(part_idx):
                                self.client_downloaded += self.client_download_urls[j][2]
                            if now - last_time > 0.2:
                                delta_bytes = current_bytes - last_bytes
                                delta_time = now - last_time
                                speed = delta_bytes / delta_time if delta_time > 0 else 0
                                last_bytes = current_bytes
                                last_time = now
                                self.report_progress(speed, f"Part {part_idx+1}")
                        return hook

                    urllib.request.urlretrieve(url, part_path, reporthook=make_hook(i))

                    self.client_downloaded += size

                self.window.after(0, lambda: self.status_var.set("Combining parts..."))

                combined_path = os.path.join(self.install_path, "game.zip")
                with open(combined_path, 'wb') as outf:
                    for part_path in temp_parts:
                        log(f"Merging {part_path}")
                        with open(part_path, 'rb') as inf:
                            shutil.copyfileobj(inf, outf)
                        os.remove(part_path)

                if self.update_cancelled:
                    os.remove(combined_path)
                    return

                self.window.after(0, lambda: self.status_var.set("Extracting..."))

                try:
                    with zipfile.ZipFile(combined_path, 'r') as zf:
                        names = zf.namelist()[:5]
                        log(f"Zip contents: {names}")
                        zf.extractall(self.install_path)
                except Exception as e:
                    log(f"Failed to extract game.zip: {e}")
                    raise

                os.remove(combined_path)
                self.save_version("client", self.client_version)
                log("Client downloaded")

                self.window.after(0, self.check_assets_after_client)

            except Exception as e:
                log(f"Download error: {e}")
                self.window.after(0, lambda err=str(e): self.handle_error(err))

        threading.Thread(target=do_download, daemon=True).start()

    def check_assets_after_client(self):
        assets_ver = self.get_local_version("assets")
        if assets_ver == "Not installed":
            self.download_assets()
        else:
            self.finish_download()

    def download_assets(self):
        log(">>> download_assets CLICKED")
        if self.download_in_progress:
            log("Already downloading")
            return

        self.download_in_progress = True
        self.assets_downloaded = 0
        self.total_assets_size = 0
        self.play_btn.config(text="...", state="disabled")
        self.status_var.set("Fetching assets...")

        def do_download():
            try:
                assets_url = f"https://github.com/{ASSETS_REPO}/archive/refs/heads/main.zip"
                if not self.assets_download_url:
                    self.assets_download_url = assets_url

                log(f"Downloading assets from: {self.assets_download_url}")
                self.window.after(0, lambda: self.status_var.set("Downloading assets..."))

                zip_path = os.path.join(self.install_path, "assets.zip")
                last_time = time.time()
                last_bytes = 0

                def hook(block, bs, size):
                    nonlocal last_time, last_bytes
                    now = time.time()
                    current_bytes = block * bs
                    self.assets_downloaded = current_bytes
                    if now - last_time > 0.2:
                        delta_bytes = current_bytes - last_bytes
                        delta_time = now - last_time
                        speed = delta_bytes / delta_time if delta_time > 0 else 0
                        last_bytes = current_bytes
                        last_time = now
                        self.report_progress(speed, "Assets")

                urllib.request.urlretrieve(self.assets_download_url, zip_path, reporthook=hook)

                self.window.after(0, lambda: self.status_var.set("Extracting assets..."))

                with zipfile.ZipFile(zip_path, 'r') as zf:
                    zf.extractall(self.install_path)

                os.remove(zip_path)
                self.save_version("assets", "main")
                log("Assets downloaded")

                self.window.after(0, self.finish_download)

            except Exception as e:
                log(f"Assets download error: {e}")
                self.window.after(0, lambda err=str(e): self.handle_error(err))

        threading.Thread(target=do_download, daemon=True).start()

    def report_progress(self, speed, phase=""):
        combined = self.client_downloaded + self.assets_downloaded
        combined_total = self.total_client_size + self.total_assets_size
        speed_str = self.format_size(speed) + "/s"
        time_left = ""
        if speed > 0 and combined_total > 0:
            remaining = combined_total - combined
            secs = remaining / speed if speed > 0 else 0
            time_left = f" - {self.format_time(secs)} left" if secs > 0 else ""
        self.window.after(0, lambda b=combined, t=combined_total, s=speed_str, p=phase, tl=time_left: self.update_progress_combined(b, t, s, tl, p))

    def format_size(self, bytes_val):
        for unit in ['B', 'KB', 'MB', 'GB']:
            if bytes_val < 1024:
                return f"{bytes_val:.1f} {unit}"
            bytes_val /= 1024
        return f"{bytes_val:.1f} TB"

    def format_time(self, seconds):
        if seconds < 60:
            return f"{int(seconds)}s"
        elif seconds < 3600:
            return f"{int(seconds/60)}m"
        else:
            return f"{int(seconds/3600)}h {int((seconds%3600)/60)}m"

    def finish_download(self):
        log("finish_download")
        self.download_in_progress = False
        self.status_var.set("Ready to play!")
        self.play_btn.config(text="Play", state="normal", command=self.launch_game)

    def update_progress_combined(self, downloaded, total, speed, time_left="", phase=""):
        combined_total = self.total_client_size + self.total_assets_size

        if combined_total > 0:
            pct = min(1.0, downloaded / combined_total)
        else:
            pct = 0

        w = self.progress_canvas.winfo_width()
        if w < 1:
            w = 800
        self.progress_canvas.coords(self.progress_rect, 0, 0, int(w * pct), 20)

        phase_str = f"[{phase}] " if phase else ""
        status = f"{phase_str}{self.format_size(downloaded)} / {self.format_size(combined_total)}"
        if speed and speed != "0.0 B/s":
            status += f" ({speed}{time_left})"
        self.status_var.set(status)

        pct_int = int(pct * 100)
        time_str = time_left.replace(" - ", "").replace(" left", "") if time_left else ""
        btn_text = f"{pct_int}%"
        if time_str:
            btn_text = f"{btn_text}\n{time_str}"
        self.play_btn.config(text=btn_text)

    def handle_error(self, error):
        log(f"handle_error: {error}")
        self.download_in_progress = False
        self.status_var.set(f"Error: {error}")
        self.play_btn.config(text="RETRY", state="normal", command=self.download_game)
        messagebox.showerror("Error", error)

    def launch_game(self):
        log("launch_game")
        nksp_path = os.path.join(self.install_path, "Nksp.exe")
        lc_path = os.path.join(self.install_path, "LC.exe")

        if os.path.exists(nksp_path):
            exe_path = nksp_path
        elif os.path.exists(lc_path):
            exe_path = lc_path
        else:
            messagebox.showerror("Error", "Game not found. Click Download to get game files.")
            return

        self.status_var.set("Launching...")
        import subprocess
        subprocess.Popen(exe_path, cwd=self.install_path)
        self.window.destroy()


def main():
    log("main() called")
    try:
        window = Tk()
        log("Tk() created")
        app = Launcher(window)
        log("Launcher created, entering mainloop")
        window.mainloop()
        log("mainloop exited")
    except Exception as e:
        log(f"FATAL: {e}")
        traceback.print_exc()
    finally:
        log("Done. Press Enter to close...")
        input()


if __name__ == "__main__":
    main()