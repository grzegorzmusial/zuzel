# Żużel Online 2.0

## Dołączenie do gry

1. Uruchom `Zuzel-2.0-Online.exe` na Windows x64.
2. Wybierz tryb indywidualny albo drużynowy, potem **Graj online**.
3. Wpisz nick. Domyślny serwer to **34.116.198.108**, port **9050 UDP**.
4. Kliknij **Połącz**. Serwer automatycznie wybierze pokój i zawodnika.
5. Poczekaj na znajomych. Dopiero potem naciśnij **Gotowy / Spacja**.

Kiedy wszyscy obecni gracze są gotowi, zaczyna się turniej. Można też wystartować samemu z trzema AI. Gdy zacznie się odliczanie, pokój zostaje zamknięty dla nowych uczestników na cały turniej. Wcześniej rozłączeni gracze są zastępowani przez AI.

W online używasz **W/A/D** albo własnego przypisania **gracza 1** w F2, niezależnie od otrzymanego koloru. Działa także pad 1. Jeden uruchomiony klient zajmuje jedno miejsce. Lokalna gra 1–4 osób nadal działa osobno.

**Esc** rozłącza z serwerem. P nie zatrzymuje wspólnej gry. Po utracie aktywności okna gaz jest puszczany; inni jadą dalej. Zbyt wczesny gaz przed podniesieniem taśmy może spowodować falstart.

## Pokoje

- Serwer uruchamia się z pokojem nr 1.
- Każdy pokój ma cztery miejsca; piąty gracz otrzymuje kolejny pokój.
- Najpierw wykorzystywane są istniejące pasujące pokoje z wolnymi miejscami.
- Indywidualny i drużynowy to różne rozgrywki: gracze obu trybów nie są mieszani. Pokój 1 przyjmuje tryb pierwszego gracza, jeśli jest pusty.
- Gdy pokój rozpoczął turniej, nowi gracze kierowani są do innego pokoju nawet po zwolnieniu miejsca.
- Puste dodatkowe pokoje są usuwane. Pokój nr 1 wraca do stanu oczekiwania.
- Obecny limit to **16 pokoi** na jednym serwerze, bez automatycznego tworzenia maszyn.

## Turniej

Serwer rozstrzyga fizykę, kolizje, taśmę, okrążenia i wyniki. Oba tryby mają 15 biegów po cztery okrążenia. Punkty 3–2–1–0 sumują się na serwerze; wykluczenie daje 0.

Po biegu wszyscy klikają Gotowy, aby przejść dalej. Po 12 sekundach przejście następuje również automatycznie. Po końcowych wynikach gracze wracają do lobby po gotowości wszystkich albo po 30 sekundach. Aby nie blokować pokoju, zawodnik, który nie ukończy biegu w 150 sekund, zostaje wykluczony.

## Architektura

- `Zuzel.Network`: wspólna fizyka i turnieje, binarny protokół, pokoje, klient i serwer UDP (LiteNetLib 1.3.5).
- `Zuzel.Server`: konsolowy proces .NET 10 dla Linux/Windows, bez grafiki i dźwięku.
- `Zuzel`: natywny klient Windows, obecna grafika i gra lokalna.
- Serwer liczy fizykę 120 razy/s. Stan czterech motocykli wysyłany jest do 30 razy/s; klienci interpolują pozycję i obrót.
- Ważne zmiany stanu są przesyłane z potwierdzeniami. Ruch używa sekwencjonowanych pakietów; stare komendy są odrzucane.
- Utrata wejścia na 0,4 s zeruje gaz i skręt. Po rozłączeniu miejsce w aktywnym turnieju przejmuje AI.
- Komunikacja sieciowa jest odpytywana w pętli gry; brak oczekiwania na odpowiedź podczas rysowania.

To pierwsza wersja online. Nie zawiera jeszcze lokalnego przewidywania własnej fizyki ani ponownego dołączania do tego samego miejsca po zerwaniu połączenia. Przy dużym pingu opóźnienie reakcji będzie zauważalne. Należy dobierać serwer blisko graczy. Bieżące pokoje nie są utrwalane po restarcie procesu.

Nie ma kont ani haseł graczy. Połączenie UDP nie ma warstwy szyfrowania aplikacyjnego; nie przesyłaj w nicku danych poufnych. Pakiety mają ograniczenia rozmiaru, wejścia są walidowane, a klient może sterować wyłącznie swoim miejscem.

## Uruchomienie serwera

Z katalogu repozytorium:

```sh
docker build -f deploy/Dockerfile -t zuzel-server:2.0 .
docker run -d --name zuzel-server --restart unless-stopped \
  --read-only --cap-drop ALL --security-opt no-new-privileges:true \
  --memory 512m --cpus 1 -p 9050:9050/udp -e MAX_ROOMS=16 \
  --log-opt max-size=5m --log-opt max-file=2 zuzel-server:2.0
```

Alternatywnie `docker compose -f deploy/compose.yaml up -d --build`.

Bez Dockera:

```sh
dotnet run --project src/Zuzel.Server -c Release
```

`PORT` określa port UDP (domyślnie 9050), `MAX_ROOMS` limit pokoi (domyślnie 16).

## Google Cloud

Wdrożenie z 24 listopada 2026:

| Element | Konfiguracja |
| --- | --- |
| Projekt | `zuzel-515709` |
| VM | `zuzel-server` |
| Strefa | `europe-central2-a` (Warszawa) |
| Maszyna | `e2-small`, Ubuntu 24.04, dysk 10 GB pd-standard |
| Stały IPv4 | `34.116.198.108` |
| Kontener | `zuzel-server`, restart `unless-stopped` |
| Port graczy | UDP 9050 |
| Limit | 16 pokoi, 512 MB pamięci kontenera, 1 CPU |

Serwer działa bez konta usługi Google Cloud i bez dostępu do API projektu. Logi kontenera mają limit 2 × 5 MB. Domyślny obraz nie zawiera grafik gry ani plików audio.

Utworzono miesięczny alert kosztowy **100 PLN**, ograniczony do tego projektu, z progami 50%, 90% i 100%. Domyślni odbiorcy to administratorzy i użytkownicy rozliczeń konta. Alert **nie wyłącza** serwera. Transfer sieciowy jest dodatkowo rozliczany; liczba pokoi sama nie zmienia typu maszyny.

Sprawdzenie procesu:

```sh
gcloud compute ssh zuzel-server --project=zuzel-515709 --zone=europe-central2-a \
  --command='sudo docker ps; sudo docker logs --tail 30 zuzel-server'
```

Zatrzymanie naliczania czasu maszyny wymaga zatrzymania VM; sam `docker stop` nie zatrzymuje VM. Dysk i zarezerwowany adres IP mogą być dalej płatne po zatrzymaniu VM.

## Testy

```sh
dotnet run --project tests/Zuzel.OnlineTests -c Release
dotnet run --project tests/Zuzel.OnlineTests -c Release -- --probe 34.116.198.108
```

`--live-five` zamiast `--probe` tworzy pięć krótkich połączeń testowych, więc używaj go, gdy serwer nie jest zajęty graczami. Test `Zuzel.exe --online-test report.txt` sprawdza połączenie rzeczywistego klienta Windows, gotowość, start i gaz na domyślnym serwerze oraz generuje obraz.

Sprawdzono lokalny podział 4+1, pięciu klientów UDP, wspólny start, sterowanie motocyklem, 15-biegowy turniej online, timeout, wykluczenia i powrót do lobby. Sprawdzono również 5 połączeń z publicznym adresem Google Cloud i rzeczywisty EXE z 110 odebranymi stanami, startem oraz ruchem motocykla. Interaktywny test kliknięć nowego okna połączenia nie był dostępny w środowisku narzędziowym; obraz i przepływ klienta zweryfikowano automatycznie.
