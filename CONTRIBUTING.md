# Contributing

Bug reports, feature requests and pull requests are welcome.

## Reporting a problem

Use the bug report form and include the steps you took, what you expected and what happened. [Open an issue](https://github.com/oski3k/MyWhoosh2Garmin/issues/new/choose)

- **Windows** — include the Windows version and system type, for example Windows 11 x64.
- **macOS** — include the macOS version and chip type, for example macOS 15 on Apple Silicon.
- **Whoosh2Garmin** — include the release tag or commit you built from.
- **Garmin Connect** — for import or training metrics issues, include the Garmin device model and any error shown.

> GitHub issues and attachments are public. A FIT file can contain timestamps, heart rate, power and location. A description or a synthetic sample can help reproduce a problem without exposing your activity data.

## Development

Clone the repository and run the commands from its root.

```sh
git clone https://github.com/oski3k/MyWhoosh2Garmin.git
cd MyWhoosh2Garmin
```

### Windows

Install the .NET 10 SDK on Windows and run these commands in PowerShell.

```powershell
dotnet publish .\Windows\Whoosh2Garmin.csproj -c Release -r win-x64 --self-contained true -o .\build\windows-x64
```

Run `build\windows-x64\Whoosh2Garmin.exe`. To check conversion, open a disposable copy of a MyWhoosh export and confirm that the app writes a `- fixed.fit` file in the source folder.

### macOS

Use macOS 14 or later and Xcode or the Xcode Command Line Tools (`xcode-select --install`).

```bash
./build.sh            # builds build/Whoosh2Garmin.app
./build.sh --install  # replaces the installed app in /Applications
./build.sh --zip      # also creates build/Whoosh2Garmin.zip
```

The macOS command-line tool writes to a separate path and leaves the input file in place. The optional time zone defaults to the system time zone.

```bash
swiftc -O -parse-as-library Tools/cli.swift Sources/FitPatcher.swift -o fitfix
./fitfix ride.fit ride-garmin.fit Europe/Warsaw
```

Use Garmin's FIT CSV Tool to inspect results locally. [FIT CSV Tool](https://developer.garmin.com/fit/fitcsvtool/)

### Project layout

| Path | Role |
| --- | --- |
| `Sources/App.swift` | macOS window, file selection and output naming |
| `Sources/FitPatcher.swift` | macOS FIT converter |
| `Tools/cli.swift` | macOS command-line tool |
| `Tools/icon.swift`, `build.sh` | macOS icon and app packaging |
| `Windows/` | Windows window, FIT converter and file handling |

## Guidelines

- Keep FIT parsing and conversion independent of each platform's UI.
- Preserve sample (`record`) data byte for byte and verify the patched FIT before writing it.
- Check both converters when changing shared FIT behavior. Use a disposable copy for testing either desktop app because successful conversion deletes the input.
- Add an entry to `CHANGELOG.md` under **Unreleased**.

## Releasing (maintainers)

Push a version tag such as `v1.1.0`. The **Build** workflow publishes the release after both platform jobs pass.

- macOS (Apple Silicon and Intel) uses `Whoosh2Garmin.zip`.
- Windows x64 uses `Whoosh2Garmin-Windows-x64.zip`.
