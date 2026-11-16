# MyWhoosh → Garmin Connect: training load from FIT files

Background research as of November 16, 2026. It describes the reasoning that led to this app; the
app itself now uses the Forerunner 965 ID (see [README](README.md#what-exactly-is-changed)).

**The first change worth testing was setting the Garmin Edge 1030 Plus ID in a copy of the FIT file**
(`manufacturer = 1`, `product = 3570`). Users report that after this change and a manual import,
MyWhoosh rides started counting towards training load. Others report that changing the device was
not enough. The effect on a given file and account has to be verified.
[Report about the Edge 1030 Plus](https://www.reddit.com/r/Garmin/comments/1oxq3pn/garmin_and_mywhoosh/) ·
[Reports of mixed results](https://www.reddit.com/r/Garmin/comments/1calv3e/incompatible_indoor_bike_trainer_with_garmin/)

## What Garmin confirms

- A manual `.fit` import saves the activity in Garmin Connect. Garmin separately describes how metrics
  are computed for activities from connected, supported platforms. MyWhoosh is not on that list. The
  official path requires a compatible Garmin device and a sync after the activity is uploaded. Garmin
  also notes that a second sync may be needed.
  [Importing activities](https://support.garmin.com/en-US/?faq=Ht3ZP52Kju075uKvqTqu99) ·
  [Metrics from third-party platforms](https://support.garmin.com/en-US/?faq=7ZzrBvVky79ktNeANR0vP8)
- Garmin requires heart-rate data to compute **Exercise Load** in the supported third-party path.
  Cycling **VO₂ max** additionally needs power data and a suitable effort. **Exercise Load** applies
  to a single ride, **Acute Load** is a weighted measure of the last seven days, and **Training
  Effect** is a separate metric. When testing, check the load of the specific ride first.
  [Metric requirements](https://support.garmin.com/en-GB/?faq=VxKazDQ2mkAmDoQbJriEBA) ·
  [Types of load](https://support.garmin.com/en-US/?faq=SEkNpdGyhR917js0qQL3Q6) ·
  [VO₂ max requirements](https://support.garmin.com/en-US/?faq=lWqSVlq3w76z5WoihLy5f8)

## Where to change the FIT file

| FIT message | Fields to check | First modification |
| --- | --- | --- |
| `file_id` (0) | `manufacturer` (1), `product` (2) | Set manufacturer `1` (Garmin) and product `3570` (Edge 1030 Plus). The FIT profile assigns MyWhoosh manufacturer ID `331`. |
| `device_info` (23) | `device_index` (0), `manufacturer` (2), `product` (4) | For `device_index = 0` (`creator`) set the same IDs. Other entries may describe sensors and the trainer. If the `creator` entry is missing, compare files to decide whether to add it. |
| `record` (20) | `heart_rate` (3), `power` (7) | Check that valid samples are present. Keep their values and timestamps. |

Field numbers come from the FIT profile. The `garmin_product` field shown after decoding is an
interpretation of `product` that depends on `manufacturer`. Garmin recommends putting the recording
device's data in `file_id` and in `device_info` with the `creator` index. That shows where to look.
Garmin doesn't document which set of fields is enough for load to be computed after a manual import
of a modified file.
[Activity file docs](https://developer.garmin.com/fit/file-types/activity/) ·
[Garmin FIT profile](https://github.com/garmin/fit-javascript-sdk/blob/c39bf8c6faedcc7a0e673452e13851186c9d3527/src/profile.js)

Older MyWhoosh exports sometimes lacked average heart rate, power and cadence in the `session`
summary. The author of another converter found those averages in a newer export. It's an extra thing
to check in a given file. In the first attempt, leave the summary and serial number alone to measure
the effect of the ID change alone.
[Code handling missing averages](https://github.com/JayQueue/MyWhoosh2Garmin/blob/main/myWhoosh2Garmin.py) ·
[Notes on a newer export](https://github.com/daveemu/WhooshToGarmin#what-the-converter-fixes)

## Testing the workaround before building the app

1. Download from your MyWhoosh profile the original FIT of a fresh ride with heart rate and power that
   isn't in Garmin Connect yet. Keep the original.
   [Exporting from MyWhoosh](https://www.mywhoosh.com/docs/how-do-i-view-my-past-rides-on-mywhoosh/)
2. In FIT File Tools choose **Device Changer → Garmin → Edge 1030 Plus**. Download the result and
   compare both files decoded to CSV. Check `file_id`, `device_info` and `session`, and that the
   `record` samples match. Device Changer uploads the file to the tool's server. Comparing both files
   shows what it actually changed.
   [Device Changer](https://www.fitfiletools.com/changer) ·
   [FitCSVTool on macOS](https://developer.garmin.com/fit/fitcsvtool/osx/)
3. Upload the modified FIT via **Import Data** in Garmin Connect. Sync your Garmin device and repeat
   the sync if needed. Check in order: the recognized device model, the ride's **Exercise Load**, and
   its contribution to **Acute Load**. Seeing the model only confirms the metadata was read. Exercise
   Load appearing confirms the effect you're after.
   [Import in Garmin Connect](https://connect.garmin.com/modern/import-data) ·
   [Syncing training metrics](https://support.garmin.com/en-US/?faq=7ZzrBvVky79ktNeANR0vP8)

## Scope of the first app version

You pick a `.fit`, the app checks it's an activity file, detects the source and the availability of
heart-rate and power samples, and writes a separate fixed copy. The converter should keep samples,
laps, events and custom fields. After the changes it should recalculate the final CRC, update the
header when the length changes, and verify that the output can be read back. All of this can run
locally, and the result can be uploaded manually.
[FIT protocol](https://developer.garmin.com/fit/protocol/) ·
[FIT integrity check](https://developer.garmin.com/fit/cookbook/isfit-checkintegrity-read/)

If the modified file imports but load still doesn't appear, useful diagnostics are: your Garmin model,
the original MyWhoosh FIT, and a FIT of a ride recorded on a Garmin for which you do see Exercise Load.
These allow comparing metadata and available samples. The official way to get load for a MyWhoosh
ride is to record it in parallel on a compatible Garmin using an indoor ride profile with heart rate.
[Garmin on activities counted in training status](https://support.garmin.com/en-MY/?faq=VxKazDQ2mkAmDoQbJriEBA)
