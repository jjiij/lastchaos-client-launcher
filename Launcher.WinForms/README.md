# Launcher.WinForms

.NET 8 WinForms migration of the legacy AutoPlay launcher.

## Projects
- `src/Launcher.UI`: WinForms app with style1/style2/style3/style4/devscreen forms
- `src/Launcher.Core`: contracts, models, command parser, state enums
- `src/Launcher.Infrastructure`: GitHub updates, resumable downloads, settings migration, repair/checklist, dependencies, launch services
- `tests/*`: unit + integration tests

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

## CLI parity
- `-dev`
- `-resetsettings`
- `-installdependencies`
- `-createlist="<path>"`

## Update channels
- Game release: `jjiij/lastchaos-client`
- Assets archive: `jjiij/lastchaos-client-assets` (`main.zip`)
- Launcher self-update: `jjiij/lastchaos-client-launcher`

## Config files
- New canonical: `launcher.settings.json`
- Legacy compatibility: `lccnct.dta`, `sl.dta`, `vtm.brn`
- Version markers: `.client_version`, `.assets_version`, `.launcher_version`

## Runtime Requirement

- Release artifacts are published as **self-contained win-x64** binaries (single-file), so end users do **not** need to install .NET separately.
