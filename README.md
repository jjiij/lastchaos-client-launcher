# LastChaos Launcher (WinForms)

Modern .NET 8 WinForms launcher replacing the legacy AutoPlay project.

## Repository Layout

- `src/Launcher.UI` — WinForms application
- `src/Launcher.Core` — domain contracts/models
- `src/Launcher.Infrastructure` — GitHub updates, download, repair, settings migration
- `tests/` — unit + integration tests
- `.github/workflows/build.yml` — Windows CI build/test/publish/release

## Features

- GitHub-only update sources:
  - Game builds from `jjiij/lastchaos-client` releases
  - Assets from `jjiij/lastchaos-client-assets`
  - Launcher self-update from `jjiij/lastchaos-client-launcher` releases
- Resumable downloads (`.part` files)
- Dual progress bars (game/assets) with speed + transferred size details
- Manual update trigger (no forced auto-download on launch)
- Repair/checklist flow and legacy config migration support
- Self-contained publish target for runtime-bundled Windows binaries

## Build (Windows)

```powershell
dotnet restore Launcher.WinForms.sln
dotnet build Launcher.WinForms.sln -c Release
dotnet test Launcher.WinForms.sln -c Release
```

## Run

```powershell
dotnet run --project src/Launcher.UI/Launcher.UI.csproj
```

## CLI Arguments

- `-dev`
- `-resetsettings`
- `-installdependencies`
- `-createlist="<path>"`

## Notes

- Legacy AutoPlay assets/source were intentionally removed from this repository.
- This repository now tracks only the WinForms launcher codebase.
- For no-admin launches, place `msvcp100.dll` and `msvcr100.dll` in `src/Launcher.UI/runtime-dlls/`; the launcher will copy them into `Bin/` automatically before starting the game.
