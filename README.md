# Football RL

Arena piłkarska w Unity przygotowana pod uczenie ze wzmocnieniem (ML-Agents). **Gra działa bez ML-Agents** — można ją uruchomić i sterować z klawiatury.

- **Unity:** 6000.6.2f1  
- **Projekt:** `unity/FootballRL`

## Szybki start

1. Otwórz folder `unity/FootballRL` w Unity Hub (wersja **6000.6.2f1**).
2. Otwórz scenę **`Assets/_Project/Scenes/ManualPlayground.unity`**.
3. Naciśnij **Play**.

Domyślnie jest jedna arena ze scenariuszem **ManualSandbox**.

## Sterowanie

| Wejście | Działanie |
|---|---|
| **W / S** | Przód / tył |
| **A / D** | Bok w lewo / prawo |
| **Q / E** | Obrót w lewo / prawo |
| **↑ / ↓** | Kąt kopnięcia (ManualSandbox) |
| **← / →** | Siła kopnięcia (Kick) |
| **Spacja** | Kopnięcie (zbocze) |
| **R** | Reset rundy / epizodu (zależnie od fazy) |

**Kamera:** prawy przycisk myszy — orbita, kółko — zoom, środkowy przycisk — przesuwanie (opis także w HUD na ekranie).

Szczegóły kopnięcia i fazy rundy widać w **ArenaDebugHUD** w lewym górnym rogu.

## ML-Agents

Pakiet **ML-Agents nie jest częścią tego repozytorium**. Właściciel projektu podłącza go sam (integracja przez publiczne API areny — m.in. `SubmitCommand`, `CaptureSnapshot`, `TryResetEpisode`). Zobacz `docs/MAPA_INTEGRACJI.md`.

## Etap projektu (G0–G6)

Mechanika areny, kopnięcia, koniec rundy, snapshot, scenariusze i weryfikacja batch (**G0–G6**) są w repozytorium. Ostatni odbiór automatyczny opisuje `docs/progress/G6.md`.

- **T21 (ręczna gra w oknie Game):** wymaga Play w edytorze — w raportach oznaczane jako **NOT RUN**, jeśli batch tego nie zastępuje.
- **T20 / benchmark / build:** status w `docs/progress/G6.md` po ostatnim batchu.

## Weryfikacja (dla deweloperów)

Batch (gdy edytor nie trzyma locka projektu):

```text
Unity.exe -batchmode -nographics -projectPath <ścieżka>/unity/FootballRL ^
  -executeMethod FootballRL.Editor.G4Verification.RunBatch -logFile unity/g4-verify.log

Unity.exe -batchmode -nographics -projectPath <ścieżka>/unity/FootballRL ^
  -executeMethod FootballRL.Editor.G6Verification.RunBatch -logFile unity/g6-verify.log
```

Logi i buildy player (`builds/`) nie trafiają do Gita.
