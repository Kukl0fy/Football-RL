# Mapa integracji — Football RL (przed ML-Agents)

Ten dokument opisuje **publiczne punkty** gry, z których korzysta przyszły adapter RL. Mechanika areny (`ArenaController`, fizyka, reset) pozostaje w module Gameplay; adapter tylko tłumaczy stan ↔ komendy.

## Główne API areny

| Metoda / zdarzenie | Rola | Uwagi |
|---|---|---|
| `SubmitCommand(playerId, PlayerCommand, commandId)` | Wejście decyzji gracza | Działa tylko w fazie `Running`. |
| `CaptureSnapshot()` | Odczyt stanu (lokalny układ areny) | Nie zmienia świata; w `Ended` zwraca zamrożony koniec rundy. |
| `TryResetEpisode(ResetEpisodeRequest, out error)` | Start epizodu | Seed, scenariusz, opcjonalnie `ExactEpisodeState`. Nieudany reset **nie** rusza pozycji. |
| `ResetEpisode()` / `ResetEpisode(request)` | Reset z logiem błędu | Wersja „miękka”; przy błędzie zapisuje `LastResetError`. |
| `RoundEnded` | Koniec rundy | `RoundEndedEventArgs`: powód, drużyna strzelca, czas. **Nie** oznacza automatycznego resetu w trybie treningowym. |

Pomocniczo: `Phase`, `EpisodeId`, `LastEndReason`, `TryGetEndState`, `TryGetActiveCommand`, zdarzenie `KickSucceeded`.

## PlayerCommand i commandId

- `PlayerCommand` (`Forward`, `Strafe`, `Turn`, `Kick`, `Loft`) jest **wspólne** dla klawiatury, bota i RL.
- **`commandId` zwiększaj przy każdej nowej decyzji agenta**, nawet gdy wektor akcji jest identyczny jak poprzednio. Gdy agent **powtarza tę samą już wysłaną decyzję** (ten sam `commandId`), numer zostaje — wtedy `Kick > 0` przez wiele kroków daje **co najwyżej jeden** impuls (deduplikacja w `ArenaController`).
- Przy braku kopnięcia (`Kick` poniżej progu) arena i tak stosuje ruch/obrót z komendy.

## CaptureSnapshot

- Zwraca `ArenaSnapshot` z wersją kontraktu (`Arena.ContractVersion`).
- Pozycje/prędkości w **układzie lokalnym korzenia areny** — arena może stać w dowolnym miejscu sceny (ważne przy wielu arenach).
- Obserwacje RL budujesz w adapterze z snapshotu; **nie** duplikuj odczytu Rigidbody w `Agent`.

## Reset epizodu

- `ResetEpisodeRequest.ForScenario(ScenarioDefinition, seed)` — losowanie przez `SpawnSampler` (seed deterministyczny).
- `ResetEpisodeRequest.WithExactState(...)` — układ jawny (walidacja nakładania).
- **Jeden koordynator** (np. manager treningu) resetuje **całą arenę raz** na epizod wspólny.
- **`OnEpisodeBegin` w wielu agentach nie może** niezależnie losować piłki ani wołać resetu areny — wywołałby wyścig i podwójne spawny.

## RoundEnded a trening

- Po `Goal`, `BallOut`, `TimeLimit` itd. faza przechodzi w `Ended`; komendy nie uruchamiają już kopnięć.
- Adapter powinien odczytać snapshot / `TryGetEndState`, **nagrodzić (własna logika Python/C#)**, a dopiero potem wywołać `TryResetEpisode` — gameplay **nie** resetuje natychmiast po golu, jeśli wyłączysz auto-reset w `RoundConfig`.

## Wiele aren (G6)

- `ArenaFactory.InstantiateArena(prefab, pozycja, rotacja)` — osobna instancja prefabu, osobny `ArenaController`, generator seedów i zdarzeń.
- W jednej scenie fizycznej: wspólny `Physics.Simulate`; krok wielu aren: `ArenaController.StepManyWithSharedSimulation(lista, dt)`.
- Po końcu rundy piłka jest zatrzymywana i **collider wyłączany**, żeby nie wpływać na sąsiednią arenę.
- Odstęp (domyślnie **100 m** w X) — areny nie dzielą tej samej przestrzeni boiska.

## Dodanie przyszłego komponentu gracza (adapter RL)

1. **Nie przenoś** fizyki do `MonoBehaviour` agenta — zostaw `PlayerMotor` / `PlayerKicker` w Gameplay.
2. Dodaj osobny komponent (np. w `Assets/_Project/MLAgents/` po instalacji pakietu) na obiekcie gracza lub obok areny:
   - W `FixedUpdate` / callbacku ML: zbuduj `PlayerCommand` z akcji sieci.
   - Wywołaj `arena.SubmitCommand(mojPlayerId, cmd, commandId)` z **nowym** `commandId` przy każdej nowej decyzji sieci (także gdy akcja się nie zmieniła). Ten sam `commandId` zostaw tylko przy ponownym wysłaniu tej samej decyzji bez nowego kroku decyzyjnego.
3. Trzymaj referencję do **konkretnego** `ArenaController` (serializowana lub przekazana przy spawnie areny) — **bez** `FindObjectOfType` / globalnego singletonu.
4. `CollectObservations`: wywołaj `arena.CaptureSnapshot()`, wybierz wpis gracza po `PlayerId`, zmapuj pola na wektor (normalizacja po stronie adaptera).
5. Epizod: subskrybuj `RoundEnded` **raz** na arenę; po obsłużeniu końca wywołaj reset przez koordynator, nie z każdego agenta osobno.

## Sterowanie ręczne (ManualPlayground)

- `KeyboardCommandSource` → `SubmitCommand` — punkt odniesienia dla heurystyki ML (`Heuristic`).
- Domyślna scena: **jedna** arena, scenariusz **ManualSandbox** — bez zmian dla właściciela.

## Czego tu nie ma

- Przykładowych nagród, `Agent`, `Act()`, YAML PPO ani konfiguracji trenera — to etap **U1+** (`docs/05`).
