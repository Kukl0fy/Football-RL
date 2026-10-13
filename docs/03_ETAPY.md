# Etapy realizacji

Pracuj od działającego wyniku do kolejnego. Kolejność G0–G6 jest dla agentów programistycznych. Kolejność U1–U5 jest dla właściciela. Nie ma obowiązku wdrażania całej wizji przed pierwszym treningiem.

## G0 — repozytorium i uruchamialny projekt

Wykonawca: agent programistyczny.

Sprawdź środowisko; wybierz zainstalowaną/wspieraną wersję Unity zgodną z planowanym wydaniem ML-Agents. Zapisz dokładny zestaw wersji Unity, pakietu C#, Python, pakietów Python i systemu w `docs/ENVIRONMENT.md`, z linkiem do oficjalnego źródła. Jeśli nie sprawdzono integracji, oznacz zestaw jako planowany. Nie instaluj jeszcze trenera.

Utwórz projekt, konfigurację Git i prostą scenę, ustaw krok fizyki 0,02 s jako wartość startową. Commituj ustawienia projektu i `.meta`. Wybierz jeden system wejścia zgodny z wersją Unity. Zapisz instrukcję otwarcia.

Odbiór: projekt otwiera się bez błędów kompilacji, scena uruchamia się; brak zależności ML-Agents. Gdy edytor niedostępny, etap ma status niezweryfikowany.

## G1 — arena i piłka

Boisko, ściany, bramki z rzeczywistymi otworami, piłka, kamera, konfiguracje materiałów i wymiarów. Weryfikacja skali oraz kolizji. Prefab areny zamiast ręcznej konstrukcji niemożliwej do powielenia.

Odbiór: piłka spada, toczy się, odbija, zatrzymuje zgodnie z parametrami; wnętrza bramek są dostępne. Nie ma przypadkowych szczelin w podłożu.

## G2 — zawodnik i komendy

PlayerCommand, wejście areny, PlayerMotor, klawiatura, HUD komend. Zablokuj przewracanie i ogranicz ruch diagonalny. Jawnie zapewnij kolejność wejścia i kroku fizyki.

Odbiór: W/S, A/D, Q/E działają zgodnie z orientacją; puszczenie klawiszy zatrzymuje ruch według przyjętej dynamiki; gracz pcha piłkę. Ruch nie zależy od FPS renderowania.

## G3 — kopnięcia

Zaimplementuj próg, siłę, kąt, zasięg, stożek, przesłonięcie, wysokość i cooldown. Obsłuż commandId oraz podgląd zasięgu. Dodaj zdarzenie udanego kopnięcia.

Odbiór: strzał po ziemi, lob, odbicie o ścianę; brak kopnięcia za plecami i przez ścianę; jedno naciśnięcie = jeden impuls. Wynik ograniczeń widoczny w HUD.

## G4 — koniec rundy i reset

GoalDetector, ArenaBounds, fazy Running/Ended, limit czasu, RoundEnded, ResetEpisode. Tryb ręczny resetuje po krótkiej pauzie; automatyczny reset można wyłączyć.

Odbiór: gol liczony raz, przelot nad poprzeczką nie jest golem, BallOut i timeout działają. Reset obejmuje prędkości, cooldowny, zegar i komendy. Stan końcowy można odczytać przed resetem.

## G5 — przygotowanie pod eksperymenty

ArenaSnapshot, SpawnSampler, seed, scenariusze `ManualSandbox`, `MoveToTarget`, `ShootAtEmptyGoal`. Cel nawigacyjny może zgłosić osiągnięcie; nie przyznaje nagrody RL. Dodaj starty łatwe i losowe, ale nie system automatycznego curriculum. Udostępnij jawny reset z konkretną pozycją.

Odbiór: poprawne snapshoty, identyczne starty dla tego samego seeda, brak błędnych/nakładających się spawnów. Scenariusz strzelecki może wyłączyć regulację kąta i ustawić piłkę przed zawodnikiem.

## G6 — odbiór gry i przekazanie właścicielowi

Dokończ testy i instrukcję. Sprawdź dwie areny uruchomione równocześnie: reset, komendy i zdarzenia jednej nie mogą zmieniać drugiej. Areny muszą być fizycznie odseparowane; piłka opuszczająca arenę zostaje zakończona/wyłączona, zanim trafi do sąsiedniej.

Przygotuj build bez zależności edytora, jeśli dostępne jest odpowiednie środowisko budowania. Nie wymagaj renderowania do pracy logiki. Zmierz prosty przebieg na 1, 4 i opcjonalnie 16 arenach przed wnioskami o wydajności; nie gwarantuj konkretnej liczby.

Przekaż mapę metod: komenda, snapshot, reset, zdarzenie końca. Dodaj instrukcję dodania komponentu gracza do przyszłego adaptera. Nie implementuj ML-Agents.

Odbiór: wszystkie kryteria obowiązkowe w dokumencie 04, raport rzeczywistych wyników i lista znanych ograniczeń.

## U1 — Twoja pierwsza integracja ML-Agents

Instalacja kompatybilnego zestawu, własny komponent Agent, CollectObservations, OnActionReceived, Heuristic i koordynacja epizodów. Najpierw sterowanie heuristic i losowe akcje; potem gotowy PPO na MoveToTarget. Patrz dokument 05.

Cel: model dociera do celu w ≥90% z 200 ustalonych epizodów testowych; próg projektowy, podany razem z limitem czasu. Oceniaj bez aktualizacji wag.

## U2 — pierwsze strzały i pełna akcja

Najpierw piłka przed agentem, krótki strzał do pustej bramki, stały kąt 0°. Potem różne kąty ustawienia, dalsze dystanse, podejście do piłki, na końcu losowy start całej akcji. Zachowuj część łatwych przypadków.

Nie zakładaj, że model nawigacji trzeba koniecznie kontynuować. Porównaj inicjalizację wcześniejszymi wagami i nowy trening, jeśli zgodność obserwacji na to pozwala. Najpierw ma działać zadanie, nie transfer za wszelką cenę.

## U3 — własne PPO jako osobny eksperyment

Po pierwszym działającym treningu i zrozumieniu przepływu danych; nie trzeba czekać na 1 vs 1. Wróć do nawigacji. Najpierw poprawne zbieranie przejść przez Python LLAPI, potem sieć actor–critic i PPO. Porównuj identyczne scenariusze i budżety interakcji.

## U4 — 1 vs 1

Osobne zlecenie rozszerzenia gry: drugi zawodnik, drużyny, bot regułowy, symetryczne starty, wspólny koniec rundy. Właściciel wdraża trening przeciw botowi, zamrożonemu modelowi, a potem self-play (grę z poprzednimi wersjami modelu).

## U5 — rozszerzenia niezależne

Lob nad przeszkodą i strzał z odbiciem: najpierw ręcznie potwierdź wykonalność, następnie dodaj informacje o przeszkodzie do obserwacji. Współpraca 2 vs 2: osobny etap badawczy, najpierw proste sytuacje podaniowe. Zwykłe kopiowanie nagrody drużynowej nie daje gwarancji dobrej współpracy.

Nie implementuj wszystkich U4/U5 na zapas. Architektura ma umożliwiać rozszerzenie, nie zastępować eksperymentów spekulacyjnym kodem.
