# Remnant II Character Cloner and Save Manager

Portable Windows utility for Remnant II character slots in `profile.sav`.

## Features

- Clone a character into an empty slot with a separate object graph, slot ID, and Unreal internal name.
- Copy character data and world (`save_N.sav`) independently.
- Export/import portable `.r2character` archives.
- Recover two legacy slots that already refer to the same character without altering either existing world file.
- Make a complete `.sav` backup before every write and restore backups from the interface.

## Build from source

### Requirements

- Windows x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — the SDK, not only the runtime
- Git

### Build steps

Open PowerShell and run:

```powershell
git clone https://github.com/KamtarGaming/Remnant-II-Character-Cloner-and-Save-Manager.git
cd Remnant-II-Character-Cloner-and-Save-Manager
dotnet build .\src\RemnantCharacterCloner\RemnantCharacterCloner.csproj -c Release
```

The framework-dependent build is written to:

```text
src\RemnantCharacterCloner\bin\Release\net8.0-windows\RemnantCharacterCloner.exe
```

### Create a portable release

To create a self-contained Windows x64 build that does not require the .NET runtime on the target computer:

```powershell
dotnet publish .\src\RemnantCharacterCloner\RemnantCharacterCloner.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

Run `publish\win-x64\RemnantCharacterCloner.exe`. Keep every file from that output folder together when distributing it.

The application automatically uses French on French Windows; English is the default and fallback.

## Safety

Close Remnant II before editing saves. Keep the automatic `CharacterClonerBackups` folder until the result has been verified in game. This is an unofficial community utility; use it at your own risk.

## Third-party code

`third_party/lib.remnant2.saves` is included under its MIT license. See its `LICENSE` file and repository README.
