# Architektura

## 1. Zasada przewodnia

Jedno środowisko, różne źródła komend. Klawiatura, prosty bot i przyszły model RL przekazują ten sam `PlayerCommand`. Mechanika nie wie, kto wybrał działanie. W drugą stronę środowisko udostępnia `ArenaSnapshot`, czyli uporządkowany odczyt aktualnego stanu.

ML-Agents jest adapterem: tłumaczy stan na obserwacje oraz wynik sieci na komendy. Adapter nie implementuje ponownie ruchu, strzałów ani resetowania fizyki.

## 2. Kierunek zależności

| Moduł | Może zależeć od | Nie może zależeć od |
|---|---|---|
| Contracts | podstawowych typów Unity | Input, UI, ML-Agents |
| Gameplay | Contracts, Unity Physics | ML-Agents, trenerów, UI |
| Scenarios | Gameplay, Contracts | trenerów, funkcji nagrody ML |
| Input | Contracts, publicznego wejścia areny | bezpośredniego modyfikowania piłki |
| Presentation | publicznego stanu i zdarzeń Gameplay | sterowania regułami meczu |
| MLAgents — później | Gameplay, Contracts, Scenarios, pakietu ML-Agents | kodu edytora |
| Python — później | interfejsu ML-Agents po komunikacji procesów | wewnętrznych klas C# |

Na początku wystarczy jeden runtime assembly dla gry i osobny dla narzędzi edytora/testów. Nie twórz osobnego assembly dla każdej klasy. Assembly integracji ML-Agents powstaje dopiero po instalacji pakietu przez właściciela.

## 3. Odpowiedzialności

| Element | Odpowiedzialność |
|---|---|
| `ArenaController` | jeden zegar, komendy graczy, cykl areny, koniec i reset |
| `PlayerMotor` | ruch na XZ, obrót wokół Y, ograniczenia prędkości |
| `PlayerKicker` | zasięg, kierunek, cooldown, pojedynczy impuls piłki |
| `BallState` | dostęp do Rigidbody piłki, odczyt i reset |
| `GoalDetector` | rozpoznanie przejścia całej piłki przez otwór bramki |
| `ArenaBounds` | wykrywanie opuszczenia obszaru gry i awaryjnego upadku |
| `ArenaStateReader` | spójny odczyt stanu; nie zmienia świata |
| `SpawnSampler` | poprawne pozycje początkowe, seed i kontrola nakładania |
| `ScenarioDefinition` | dozwolone spawny, obiekty, czas, dostępność mechanik |
| `KeyboardCommandSource` | odczyt klawiszy i zamiana na PlayerCommand |
| `ArenaDebugHUD` | wynik, czas, siła/kąt, cooldown, ostatni powód zakończenia |
| `ArenaFactory` | późniejsze tworzenie wielu niezależnych aren z prefabu |

Nie twórz jednego ogromnego `GameManager`, który czyta klawiaturę, liczy nagrody i rusza wszystkimi obiektami. Nie potrzebujemy też rozbudowanego globalnego systemu zdarzeń: wystarczą lokalne zdarzenia C# na danej arenie.

## 4. Docelowa struktura repozytorium

Katalogi w poniższej tabeli są planem. Twórz je, gdy są potrzebne; nie generuj pustych atrap kodu.

| Ścieżka od korzenia | Zawartość |
|---|---|
| `README.md`, `AGENTS.md`, `docs/`, `tasks/` | ten pakiet i dalsze ustalenia |
| `unity/FootballRL/Assets/_Project/Scripts/Contracts/` | komendy, snapshoty, typy zdarzeń |
| `unity/FootballRL/Assets/_Project/Scripts/Gameplay/` | arena, ruch, kopanie, bramki |
| `unity/FootballRL/Assets/_Project/Scripts/Scenarios/` | reset, spawny, definicje zadań |
| `unity/FootballRL/Assets/_Project/Scripts/Input/` | klawiatura, później prosty bot |
| `unity/FootballRL/Assets/_Project/Scripts/Presentation/` | kamera, HUD, podgląd zasięgu |
| `unity/FootballRL/Assets/_Project/Editor/` | narzędzia budowy i walidacji scen |
| `unity/FootballRL/Assets/_Project/Scenes/` | `ManualPlayground`, później `Training` |
| `unity/FootballRL/Assets/_Project/Prefabs/` | arena, gracz, piłka, bramka, przeszkoda |
| `unity/FootballRL/Assets/_Project/Configs/` | konfiguracje jako ScriptableObject |
| `unity/FootballRL/Assets/_Project/Tests/` | EditMode i PlayMode |
| `unity/FootballRL/Assets/_Project/MLAgents/` | później kod właściciela: adaptery RL |
| `unity/FootballRL/Packages/` | manifest i lock zależności |
| `unity/FootballRL/ProjectSettings/` | wersjonowane ustawienia Unity |
| `training/mlagents/` | później YAML gotowego trenera |
| `training/custom_ppo/` | później własny trener Python |
| `experiments/` | małe opisy eksperymentów i wyniki zbiorcze |
| `runs/`, `builds/`, `recordings/` | duże wyniki; domyślnie poza Git |

## 5. Struktura sceny

`ManualPlayground` zawiera kamerę, światło, HUD i instancję `Arena`.
Prefab `Arena` zawiera podłoże, ściany z otworami na bramki, dwie bramki, piłkę, kontener graczy i kontener opcjonalnych przeszkód. Arena przekazuje referencje do swoich obiektów jawnie.

Początkowo aktywny jest jeden gracz. Drugi może powstać dopiero przy G6 lub rozszerzeniu meczu. Używaj stabilnego `PlayerId` i `TeamId`, aby nie utożsamiać zawodnika z indeksem obiektu na scenie.

## 6. Jeden właściciel czasu i resetu

Cykl: `Running` → `Ended` → jawny `Reset` → `Running`.

Podczas Running kontroler pobiera ostatnie komendy, wykonuje ruch/kopnięcia i aktualizuje zegar oraz cooldowny na kroku fizyki. Zdarzenia kolizji są przetwarzane zgodnie z ustaloną kolejnością; koniec może zostać zatwierdzony tylko raz. Po Ended komendy nie wywołują nowych działań. Ostatni stan pozostaje dostępny do odczytu.

Domyślne tryby zakończenia: `Goal`, `BallOut`, `TimeLimit`, `InvalidState`, `ManualReset`. Przy kilku zdarzeniach w tym samym kroku: InvalidState, potem prawidłowy Goal, potem BallOut, potem TimeLimit. Wynik zapisuje jednoznaczny powód.

W trybie ręcznym prezenter może wywołać reset po krótkiej pauzie. W trybie treningowym to przyszły koordynator RL decyduje, kiedy zakończyć epizody i zresetować arenę. Gameplay nie resetuje natychmiast po golu; inaczej Python mógłby dostać stan nowego epizodu zamiast końcowego.

W grze wieloagentowej koordynator resetuje wspólną arenę jeden raz. `OnEpisodeBegin()` każdego zawodnika nie może niezależnie losować wspólnej piłki.

## 7. Konfiguracje

`MovementConfig`, `KickConfig`, `BallPhysicsConfig`, `ArenaConfig`, `ScenarioDefinition`. Konfiguracje opisują parametry, nie bieżący stan. Nie zapisuj cooldownu lub wyniku do współdzielonego ScriptableObject — wiele aren korzysta z tego samego assetu.

Zmiana fizyki w środku porównania algorytmów unieważnia prostą interpretację porównania. Zapisuj wersję kontraktu, konfiguracje i commit razem z wynikami.
