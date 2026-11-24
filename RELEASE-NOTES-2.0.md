## Żużel 2.0 Online — Windows x64

Pobierz **Zuzel-2.0-Online.exe** i uruchom. Gra lokalna nadal działa offline.

### Gra przez internet

1. Wybierz tryb indywidualny lub drużynowy i kliknij **Graj online**.
2. Wpisz nick; adres serwera **34.116.198.108** jest już ustawiony.
3. Po dołączeniu poczekaj na znajomych, potem wybierz **Gotowy / Spacja**.

Serwer automatycznie przydziela pokoje po cztery osoby. Piąty gracz otwiera kolejny pokój. Gotowość wszystkich obecnych graczy rozpoczyna turniej; wolne miejsca zajmuje AI. Każdy turniej ma 15 biegów z sumowaniem punktów. Skład pokoju pozostaje zamknięty do końca turnieju.

W online kierujesz **W/A/D** (lub swoim przypisaniem gracza 1), niezależnie od koloru. **Esc** rozłącza. **F2** zmienia klawisze, **F3** pokazuje FPS, **F11** przełącza okno, **M** wycisza.

Serwer działa w Dockerze w Google Cloud, Warszawa. To pierwsza wersja online: bez ponownego dołączania do tego samego miejsca i bez lokalnego przewidywania fizyki; przy dużym pingu reakcja na sterowanie może być opóźniona.

Przeszły testy lokalnych klientów UDP, podziału 4+1, 15 biegów, połączenia z Google Cloud i sterowania przez gotowy EXE. Szczegóły są w [instrukcji online](../../blob/main/docs/ONLINE.md).
