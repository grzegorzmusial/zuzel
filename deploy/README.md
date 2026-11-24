# Kontener serwera

Instrukcja uruchomienia, konfiguracja Google Cloud, koszty i testy: [docs/ONLINE.md](../docs/ONLINE.md).

Dockerfile kompiluje tylko wspólną logikę i proces konsolowy. Klient Windows nie jest uruchamiany w kontenerze.

Skrypt `startup.sh` jest jednorazowym przygotowaniem Ubuntu do pracy z Dockerem; właściwy kontener należy zbudować i uruchomić zgodnie z instrukcją. `restart: unless-stopped` odpowiada za ponowne uruchamianie kontenera po awarii i restarcie hosta.
