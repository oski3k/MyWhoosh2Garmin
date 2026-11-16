# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [1.0.0] - 2026-11-16

### Added
- macOS app: drop a MyWhoosh `.fit` file to get a Garmin Connect–ready copy.
- Device ID set to Garmin Forerunner 965 in `file_id` and `device_info` (creator entry added if missing).
- Correct `local_timestamp` in the `activity` message.
- Output named `<ride title> - <date> - fixed.fit`, using the MyWhoosh ride title when available.
- Verification of the output (CRC, device, untouched samples) before saving.
- Command-line version (`Tools/cli.swift`).
- Universal build (Apple Silicon + Intel) via `build.sh`.
