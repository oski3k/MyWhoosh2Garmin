# Whoosh2Garmin

[![Build](https://github.com/oski3k/MyWhoosh2Garmin/actions/workflows/build.yml/badge.svg)](https://github.com/oski3k/MyWhoosh2Garmin/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![macOS 14+](https://img.shields.io/badge/macOS-14%2B-lightgrey)

A tiny macOS app that makes **MyWhoosh** rides count towards your **Garmin training load**.

Drop a `.fit` file exported from MyWhoosh onto the app and it rewrites the file so Garmin Connect
treats it as a ride recorded on a Garmin device. After a manual import, Garmin can then compute
**Exercise Load**, **Training Effect** and contribute the ride to **Acute Load** — which it normally
skips for MyWhoosh activities.

Everything happens locally. No accounts, no network, no tracking.

## Download & install

1. Download `Whoosh2Garmin.zip` from the latest [release](https://github.com/oski3k/MyWhoosh2Garmin/releases/latest).
2. Unzip it and move `Whoosh2Garmin.app` to **Applications**.
3. First launch: the app is not notarized by Apple, so macOS will block it. Either
   - open it once, then go to **System Settings → Privacy & Security** and click **Open Anyway**, or
   - run in Terminal:
     ```bash
     xattr -dr com.apple.quarantine /Applications/Whoosh2Garmin.app
     ```

Requires macOS 14 Sonoma or newer (Apple Silicon or Intel).

## Usage

1. In MyWhoosh, open your ride history and download the ride's `.fit` file
   ([how](https://www.mywhoosh.com/docs/how-do-i-view-my-past-rides-on-mywhoosh/)).
2. Drop the file onto the Whoosh2Garmin window or its Dock icon (or click **choose…**).
   Several files at once are fine.
3. Next to it you'll get `<ride title> - <date> - fixed.fit`.
4. Upload that file at [Garmin Connect → Import Data](https://connect.garmin.com/modern/import-data).
5. Sync your Garmin watch/bike computer (sometimes twice) so the training status updates.

> [!WARNING]
> **The original file is deleted** after the fixed copy has been written and verified.
> Keep a backup if you want to keep the untouched MyWhoosh export.

If the file can't be processed (not a FIT file, not an activity, damaged), the app beeps and
leaves the file untouched.

> [!NOTE]
> Garmin needs **heart-rate data** in the ride to compute Exercise Load. Rides without HR will import
> fine but won't add any load.

## What exactly is changed

| FIT message | Change |
| --- | --- |
| `file_id` | manufacturer → Garmin (`1`), product → Forerunner 965 (`4315`), `product_name` cleared |
| `device_info` (creator, `device_index = 0`) | same IDs; the entry is added if it's missing |
| `activity` | `local_timestamp` → correct local time (MyWhoosh writes a wrong value) |

All samples (`record`), laps, sessions, events and developer fields are copied **byte for byte**.
Afterwards the header size, header CRC and file CRC are recalculated, and the output is parsed
again and checked (valid CRC, Garmin device, identical samples). If verification fails, nothing is
written.

Background and sources: [RESEARCH.md](RESEARCH.md).

## Build from source

Requires Xcode or the Xcode Command Line Tools (`xcode-select --install`).

```bash
git clone https://github.com/oski3k/MyWhoosh2Garmin.git
```

```bash
cd MyWhoosh2Garmin && ./build.sh --install
```

| Command | Result |
| --- | --- |
| `./build.sh` | builds a universal `build/Whoosh2Garmin.app` |
| `./build.sh --install` | also copies it to `/Applications` (replacing an older copy) |
| `./build.sh --zip` | also creates `build/Whoosh2Garmin.zip` for a release |

### Command-line version

Handy for testing or batch processing — it doesn't delete the input:

```bash
swiftc -O -parse-as-library Tools/cli.swift Sources/FitPatcher.swift -o fitfix
```

```bash
./fitfix ride.fit ride-garmin.fit Europe/Warsaw
```

The last argument (time zone) is optional; it defaults to the system time zone.

## Project layout

| Path | What it is |
| --- | --- |
| `Sources/FitPatcher.swift` | all FIT parsing/patching logic, Foundation only (portable) |
| `Sources/App.swift` | SwiftUI window, drag & drop, file naming |
| `Tools/cli.swift` | terminal version |
| `Tools/icon.swift` | draws the app icon at build time |
| `build.sh` | build script (no Xcode project needed) |

## FAQ

**Does this break Garmin's terms?** You're changing the device ID in your own file. It's a gray area —
use it at your own discretion. The tool doesn't touch your ride data.

**My ride imported but still shows no load.** Check that the ride has heart-rate data, then sync your
device again. Garmin doesn't document which fields are needed; if it still fails, please
[open an issue](https://github.com/oski3k/MyWhoosh2Garmin/issues/new/choose) with your Garmin model.

**Windows / Linux?** Not yet. `FitPatcher.swift` has no UI dependencies, so a port is possible —
contributions welcome.

**Duplicate activity?** If the ride is already in Garmin Connect (e.g. via Strava sync), delete it
first, otherwise the import is rejected as a duplicate.

## Contributing

Bug reports and pull requests are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md).

## Disclaimer

This is an independent project, not affiliated with or endorsed by Garmin or MyWhoosh.
Garmin, Garmin Connect and Forerunner are trademarks of Garmin Ltd.; MyWhoosh is a trademark of its
owner. Provided "as is", without warranty — see [LICENSE](LICENSE).
