# Contributing

Thanks for your interest in Whoosh2Garmin! Bug reports, ideas and pull requests are all welcome.

## Reporting a problem

Please [open an issue](https://github.com/oski3k/MyWhoosh2Garmin/issues/new/choose) and include:

- your macOS version and Mac type (Apple Silicon / Intel),
- your Garmin device model,
- what happened vs. what you expected (e.g. "imported, but no Exercise Load"),
- if you can, the original MyWhoosh `.fit` file.

> FIT files contain your ride data (timestamps, heart rate, power, sometimes location).
> Only attach one if you're comfortable sharing it publicly.

## Development

Requirements: macOS 14+, Xcode or the Xcode Command Line Tools.

```bash
./build.sh            # builds build/Whoosh2Garmin.app
./build.sh --install  # builds and installs to /Applications
```

The quickest way to test changes to the FIT logic is the command-line tool, which never deletes
its input:

```bash
swiftc -O -parse-as-library Tools/cli.swift Sources/FitPatcher.swift -o fitfix
./fitfix ride.fit out.fit
```

Useful for inspecting results: Garmin's [FitCSVTool](https://developer.garmin.com/fit/fitcsvtool/)
or [fitfiletools.com](https://www.fitfiletools.com/).

## Guidelines

- Keep `Sources/FitPatcher.swift` free of UI/AppKit dependencies so it stays portable.
- Never modify sample (`record`) data — the patcher must copy everything it doesn't need to change byte for byte.
- Keep the post-write verification in place; if you change what's patched, extend `verify` accordingly.
- Small, focused pull requests with a short description of *why* are easiest to review.
- Add an entry to `CHANGELOG.md` under **Unreleased**.

## Releasing (maintainers)

Push a tag like `v1.1.0`. The **Build** workflow builds a universal app, zips it and attaches
`Whoosh2Garmin.zip` to a GitHub release for that tag.
