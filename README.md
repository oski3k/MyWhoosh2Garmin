# Whoosh2Garmin

[![Build](https://github.com/oski3k/MyWhoosh2Garmin/actions/workflows/build.yml/badge.svg)](https://github.com/oski3k/MyWhoosh2Garmin/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Windows 11 x64](https://img.shields.io/badge/Windows%2011-x64-lightgrey)
![macOS 14+](https://img.shields.io/badge/macOS-14%2B-lightgrey)

Whoosh2Garmin prepares MyWhoosh ride exports for manual import into Garmin Connect. It updates
the recording-device metadata in the `.fit` file so a compatible Garmin device may calculate
training metrics after syncing. Conversion runs entirely on your computer.

[Windows setup](#windows) · [macOS setup](#macos) · [Convert and import a ride](#convert-and-import-a-ride)

## Windows

Tested on Windows 11 x64. The portable app includes its .NET runtime.

1. Open [GitHub Releases](https://github.com/oski3k/MyWhoosh2Garmin/releases/latest) and download
   `Whoosh2Garmin-Windows-x64.zip` from **Assets**.
2. Right-click the ZIP in File Explorer and choose **Extract All…**.
3. Open the extracted folder and double-click `Whoosh2Garmin.exe`. No installation is needed.

If the Windows ZIP is missing from the latest release, you can
[build the current source](CONTRIBUTING.md#development).

## macOS

Requires macOS 14 Sonoma or later. The app supports Apple Silicon and Intel Macs.

1. Open [GitHub Releases](https://github.com/oski3k/MyWhoosh2Garmin/releases/latest) and download
   `Whoosh2Garmin.zip` from **Assets**.
2. Double-click the ZIP in Finder and move `Whoosh2Garmin.app` into **Applications**.
3. Open the app from **Applications**. The app is not notarized by Apple. If macOS blocks it, follow
   Apple's instructions for **Open Anyway** in **System Settings → Privacy & Security**.
   [Apple Support](https://support.apple.com/en-us/102445)

## Convert and import a ride

> [!WARNING]
> On both platforms, Whoosh2Garmin **permanently deletes the source file** after saving the
> verified converted file. Make a separate backup first if you want to keep the original export.

1. Sign in on the MyWhoosh website. Open your **Profile**, find **Activity Files**, and download the
   ride's `.fit` file. [MyWhoosh export guide](https://www.mywhoosh.com/docs/how-do-i-view-my-past-rides-on-mywhoosh/)
2. Drag the file onto the **Whoosh → Garmin** window or click **or choose…** to select it.
   You can select several files at once. On macOS, you can also drop files onto the app's Dock icon.
3. In the source folder, find the new file ending in `- fixed.fit`, for example
   `Morning Ride - 2026-11-16 - fixed.fit`. If that filename already exists, look for a numbered suffix.
4. Open Garmin Connect in your browser and upload the converted file. [Garmin Connect Import Data](https://connect.garmin.com/modern/import-data)
5. Sync your compatible Garmin watch or bike computer. If metrics are missing, sync again and
   check **Exercise Load** on the imported activity. Garmin decides which metrics to calculate.
   [Garmin guidance on syncing training metrics](https://support.garmin.com/en-CA/?faq=7ZzrBvVky79ktNeANR0vP8)

## Troubleshooting

- **The app beeped and there is no converted file.** Check that you selected a `.fit` activity
  exported from MyWhoosh and that you can write to its folder. If the file appears damaged,
  download it again. A failed conversion leaves the source file in place.
- **Garmin Connect reports a duplicate.** Check whether the ride has already arrived through a
  linked service such as Strava or an earlier import.
- **The ride appears without Exercise Load.** Check that the ride includes heart-rate data and
  that your Garmin device supports Training Status. Sync it again after importing. The workaround
  depends on how Garmin processes the activity and cannot guarantee Exercise Load, Training Effect,
  or a contribution to Acute Load. [Garmin Training Status requirements](https://support.garmin.com/en-GB/?faq=VxKazDQ2mkAmDoQbJriEBA)

Still stuck? [Open a GitHub issue](https://github.com/oski3k/MyWhoosh2Garmin/issues/new/choose)
with your operating system, app version, Garmin model, and what happened.

## What the app changes

| FIT message | Updated fields |
| --- | --- |
| `file_id` | Sets the manufacturer to Garmin (`1`) and product to Forerunner 965 (`4315`), and clears the product name. |
| `device_info` | Applies the same IDs to the recording device entry (`device_index = 0`) and adds the entry if it is missing. |
| `activity` | Corrects the local timestamp using the computer's time zone for the activity date. |

Recorded samples, laps, sessions, events and developer data are copied without changing their
values. The app recalculates the FIT checksums and verifies the result before saving.
[Technical background](RESEARCH.md)

## Build and contribute

See [CONTRIBUTING.md](CONTRIBUTING.md) for Windows and macOS build instructions, the macOS
command-line tool, and bug reporting. See [CHANGELOG.md](CHANGELOG.md) for release changes.

Whoosh2Garmin is an independent project. Garmin and MyWhoosh do not endorse it. The source is
available under the [MIT license](LICENSE).
