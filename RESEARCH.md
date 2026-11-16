# MyWhoosh → Garmin Connect. Obciążenie treningowe z plików FIT

Stan na 16 listopada 2026 r.

**Pierwsza zmiana do przetestowania to ustawienie w kopii FIT identyfikatora Garmin Edge 1030 Plus** (`manufacturer = 1`, `product = 3570`). Użytkownicy zgłaszają, że po takiej zmianie i ręcznym imporcie treningi z MyWhoosh zaczęły wpływać na obciążenie. Inni zgłaszają, że zmiana urządzenia nie wystarczyła. Efekt na Twoim pliku i koncie pozostaje do sprawdzenia. [Relacja o Edge 1030 Plus](https://www.reddit.com/r/Garmin/comments/1oxq3pn/garmin_and_mywhoosh/) · [Relacje o różnym działaniu](https://www.reddit.com/r/Garmin/comments/1calv3e/incompatible_indoor_bike_trainer_with_garmin/)

## Co potwierdza Garmin

- Ręczny import `.fit` zapisuje aktywność w Garmin Connect. Garmin osobno opisuje naliczanie metryk dla aktywności z połączonych, obsługiwanych platform. MyWhoosh nie figuruje na tej liście. Oficjalna ścieżka wymaga kompatybilnego urządzenia Garmin i synchronizacji po przesłaniu aktywności. Garmin podaje też, że może być potrzebna druga synchronizacja. [Import aktywności](https://support.garmin.com/pl-PL/?faq=Ht3ZP52Kju075uKvqTqu99) · [Metryki z platform zewnętrznych](https://support.garmin.com/pl-PL/?faq=7ZzrBvVky79ktNeANR0vP8)
- Garmin wymaga danych tętna do obliczenia **Exercise Load** w obsługiwanej ścieżce zewnętrznej. Kolarskie **VO₂ max** wymaga także danych mocy i odpowiedniego wysiłku. **Exercise Load** dotyczy jednej jazdy, **Acute Load** jest ważoną miarą z ostatnich siedmiu dni, a **Training Effect** to osobna metryka. Przy teście w pierwszej kolejności sprawdź obciążenie konkretnej jazdy. [Warunki obliczania metryk](https://support.garmin.com/en-GB/?faq=VxKazDQ2mkAmDoQbJriEBA) · [Rodzaje obciążenia](https://support.garmin.com/pl-PL/?faq=SEkNpdGyhR917js0qQL3Q6) · [Wymagania VO₂ max](https://support.garmin.com/en-US/?faq=lWqSVlq3w76z5WoihLy5f8)

## Gdzie zmienić plik FIT

| Komunikat FIT | Pola do sprawdzenia | Pierwszy wariant modyfikacji |
| --- | --- | --- |
| `file_id` (0) | `manufacturer` (1), `product` (2) | Ustaw producenta `1` (Garmin) i produkt `3570` (Edge 1030 Plus). Profil FIT przypisuje MyWhoosh ID producenta `331`. |
| `device_info` (23) | `device_index` (0), `manufacturer` (2), `product` (4) | Dla `device_index = 0` (`creator`) ustaw te same identyfikatory. Pozostałe wpisy mogą opisywać czujniki i trenażer. Jeśli wpisu `creator` brakuje, sprawdź na porównaniu plików, czy warto go dodać. |
| `record` (20) | `heart_rate` (3), `power` (7) | Sprawdź obecność poprawnych próbek. Zachowaj ich wartości i znaczniki czasu. |

Numery pól opisuje profil FIT. Pole `garmin_product` widoczne po dekodowaniu to interpretacja pola `product` zależna od wartości `manufacturer`. Garmin zaleca umieszczenie danych urządzenia rejestrującego w `file_id` i `device_info` z indeksem `creator`. To wskazuje miejsca do sprawdzenia. Garmin nie dokumentuje, który zestaw pól wystarczy do naliczenia obciążenia po ręcznym imporcie zmodyfikowanego pliku. [Dokumentacja formatu aktywności](https://developer.garmin.com/fit/file-types/activity/) · [Profil FIT Garmina](https://github.com/garmin/fit-javascript-sdk/blob/c39bf8c6faedcc7a0e673452e13851186c9d3527/src/profile.js)

Starsze eksporty MyWhoosh bywały pozbawione średnich tętna, mocy i kadencji w podsumowaniu `session`. Autor innego konwertera znalazł te średnie w nowszym eksporcie. To dodatkowa rzecz do sprawdzenia w konkretnym pliku. W pierwszej próbie zostaw podsumowanie i numer seryjny w spokoju, żeby zmierzyć efekt samej zmiany identyfikatorów. [Kod obsługujący brakujące średnie](https://github.com/JayQueue/MyWhoosh2Garmin/blob/main/myWhoosh2Garmin.py) · [Obserwacje z nowszego eksportu](https://github.com/daveemu/WhooshToGarmin#what-the-converter-fixes)

## Jak sprawdzić obejście przed budową aplikacji

1. Pobierz z profilu MyWhoosh oryginalny FIT świeżej jazdy z tętnem i mocą, której jeszcze nie ma w Garmin Connect. Zachowaj oryginał. [Eksport z MyWhoosh](https://www.mywhoosh.com/docs/how-do-i-view-my-past-rides-on-mywhoosh/)
2. W FIT File Tools wybierz **Device Changer → Garmin → Edge 1030 Plus**. Pobierz wynik i porównaj oba pliki po zdekodowaniu do CSV. Sprawdź `file_id`, `device_info` i `session` oraz zgodność próbek `record`. Device Changer przesyła plik do serwera narzędzia. Analiza obu plików pokaże, co faktycznie zmienił. [Device Changer](https://www.fitfiletools.com/changer) · [FitCSVTool na macOS](https://developer.garmin.com/fit/fitcsvtool/osx/)
3. Prześlij zmodyfikowany FIT przez **Importuj dane** w Garmin Connect. Zsynchronizuj urządzenie Garmin, a jeśli trzeba, powtórz synchronizację. Sprawdź kolejno rozpoznany model urządzenia, **Exercise Load** tej jazdy i jej wkład w **Acute Load**. Samo wyświetlenie modelu potwierdza odczyt metadanych. Pojawienie się Exercise Load potwierdza efekt, którego szukasz. [Import w Garmin Connect](https://connect.garmin.com/modern/import-data) · [Synchronizacja metryk treningowych](https://support.garmin.com/pl-PL/?faq=7ZzrBvVky79ktNeANR0vP8)

## Zakres pierwszej wersji aplikacji

Wskazujesz `.fit`, aplikacja od razu sprawdza, czy to plik aktywności, pokazuje źródło i dostępność próbek tętna oraz mocy, a następnie zapisuje osobną kopię `*-garmin.fit`. Zakres modyfikacji warto ustalić po porównaniu z plikiem, który dał obciążenie w Garmin Connect. Konwerter powinien zachować próbki, okrążenia, zdarzenia i pola niestandardowe. Po zmianach powinien przeliczyć końcową sumę CRC, przy zmianie długości zaktualizować nagłówek i zweryfikować, czy wyjście daje się poprawnie odczytać. Cały ten etap może działać lokalnie, a gotowy plik można wgrać ręcznie. [Protokół FIT](https://developer.garmin.com/fit/protocol/) · [Weryfikacja integralności FIT](https://developer.garmin.com/fit/cookbook/isfit-checkintegrity-read/)

Jeśli zmodyfikowany plik przejdzie import, a obciążenie się nie pojawi, do dalszej diagnostyki przydadzą się model Twojego Garmina, oryginalny FIT z MyWhoosh i FIT z jazdy zarejestrowanej na Garminie, dla której widzisz Exercise Load. Te pliki pozwolą porównać metadane i dostępne próbki. Oficjalnym sposobem na naliczenie obciążenia podczas jazdy na MyWhoosh jest równoległe zarejestrowanie jej na kompatybilnym Garminie w profilu jazdy indoor z tętnem. [Garmin o aktywnościach uwzględnianych w statusie treningu](https://support.garmin.com/en-MY/?faq=VxKazDQ2mkAmDoQbJriEBA)
