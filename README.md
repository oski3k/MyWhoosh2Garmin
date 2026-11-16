# Whoosh2Garmin

Mała aplikacja na Maca: upuszczasz plik `.fit` z MyWhoosh, obok powstaje kopia
`nazwa-garmin.fit` gotowa do importu w Garmin Connect. Oryginał zostaje nietknięty.

## Użycie

1. `./build.sh` — buduje `build/Whoosh2Garmin.app` (wymaga Xcode).
2. Upuść plik `.fit` na okno aplikacji albo na jej ikonę w Docku.
3. Wgraj plik `*-garmin.fit` na <https://connect.garmin.com/modern/import-data>.

## Co jest zmieniane

| Komunikat FIT | Zmiana |
| --- | --- |
| `file_id` | producent → Garmin (1), produkt → Forerunner 965 (4315) |
| `device_info` (creator) | to samo; jeśli wpisu brak, jest dodawany |
| `activity` | `local_timestamp` → czas lokalny (MyWhoosh zapisuje tu błędną wartość) |

Próbki, okrążenia i sesja są kopiowane bajt w bajt. Po zapisie plik jest ponownie
wczytywany i sprawdzany (CRC, urządzenie, nienaruszone próbki).

## Pliki

- `Sources/FitPatcher.swift` — cała logika, sam Foundation (do przeniesienia na Windows).
- `Sources/App.swift` — okno aplikacji (SwiftUI).
- `Tools/cli.swift` — wersja terminalowa do testów, `Tools/icon.swift` — ikona.
- `RESEARCH.md` — źródła i tło.
