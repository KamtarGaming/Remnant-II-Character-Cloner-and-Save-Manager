# Remnant II Character Cloner and Save Manager

Portable Windows utility for Remnant II character slots in `profile.sav`.

## Features

- Clone a character into an empty slot with a separate object graph, slot ID, and Unreal internal name.
- Copy character data and world (`save_N.sav`) independently.
- Export/import portable `.r2character` archives.
- Recover two legacy slots that already refer to the same character without altering either existing world file.
- Make a complete `.sav` backup before every write and restore backups from the interface.

## Build

Requires the .NET 8 SDK on Windows.

```powershell
dotnet build .\src\RemnantCharacterCloner\RemnantCharacterCloner.csproj -c Release
dotnet publish .\src\RemnantCharacterCloner\RemnantCharacterCloner.csproj -c Release -r win-x64 --self-contained true
```

The application automatically uses French on French Windows; English is the default and fallback.

## Safety

Close Remnant II before editing saves. Keep the automatic `CharacterClonerBackups` folder until the result has been verified in game. This is an unofficial community utility; use it at your own risk.

## Third-party code

`third_party/lib.remnant2.saves` is included under its MIT license. See its `LICENSE` file and repository README.
