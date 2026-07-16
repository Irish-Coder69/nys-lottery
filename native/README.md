# Native Windows App (WPF)

This folder contains a true Windows-native desktop version of the NYS Lottery app.

## Project

- Project: native/NysLottery.Native/NysLottery.Native.csproj
- Framework: .NET 8, WPF

## Run In Development

```powershell
dotnet run --project native/NysLottery.Native/NysLottery.Native.csproj
```

## Build

```powershell
dotnet build native/NysLottery.Native/NysLottery.Native.csproj -c Release
```

## Publish Standalone EXE

```powershell
dotnet publish native/NysLottery.Native/NysLottery.Native.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

Published output:

- native/NysLottery.Native/bin/Release/net8.0-windows/win-x64/publish/NysLottery.Native.exe

## Features Included

- Native WPF UI (not browser-hosted).
- Lottery game picker and number generation.
- Local history panel.
- Version manifest check against GitHub version.json.

## Next Migration Steps

1. Port remaining game rules and presets from the Electron version.
2. Port historical fetch/data parsing into C# services.
3. Add a native installer (MSIX or MSI) and Windows auto-update pipeline.
