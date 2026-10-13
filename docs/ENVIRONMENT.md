# Środowisko projektu Football RL

**Data sprawdzenia:** 2026-10-13 (maszyna deweloperska, Windows)

## System operacyjny

| Element | Wartość | Źródło |
|---|---|---|
| System | Microsoft Windows NT 10.0.26200.0 (25H2) | `[System.Environment]::OSVersion`, rejestr `DisplayVersion` |

## Unity Editor (zainstalowany)

| Element | Wartość | Źródło |
|---|---|---|
| Wersja edytora | **6000.6.2f1** (770e33f6875c) | `C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe -version` |
| Instalacja | Unity Hub → `C:\Program Files\Unity\Hub\Editor\6000.6.2f1\` | przegląd katalogu Hub na dysku |

Projekt gry: `unity/FootballRL/` (zgodnie z `docs/01_ARCHITEKTURA.md`).

## Python (lokalnie)

| Element | Wartość | Uwagi |
|---|---|---|
| Domyślny `python` w PATH | **brak** | Komenda `python` nie jest dostępna bez launchera |
| Python 3.12.3 | `C:\Users\huber\AppData\Local\Programs\Python\Python312\python.exe` | `py -0p` |
| Python 3.13.5 (uv) | `C:\Users\huber\AppData\Roaming\uv\python\cpython-3.13.5-windows-x86_64-none\python.exe` | `py -0p` |
| Pakiet `numpy` (3.12) | 2.4.6 | `py -3.12 -m pip list` |
| Pakiety `mlagents` / `torch` | **nie zainstalowane** | brak wpisów w `pip list` na 3.12 |

Trener ML-Agents **nie był instalowany** w G0.

## Zgodność Unity ↔ ML-Agents (planowana, niezweryfikowana w edytorze)

Integracja pakietu `com.unity.ml-agents` **nie została wykonana** w tym etapie. Poniższy zestaw jest **planowany** na podstawie aktualnej dokumentacji Unity (stan na datę sprawdzenia).

| Składnik | Wersja planowana | Status w projekcie |
|---|---|---|
| Unity Editor | 6000.6.2f1 (zainstalowana) | używana do projektu |
| Pakiet C# `com.unity.ml-agents` | **4.1.0** (zgodny z Unity 6000.0+) | **nie dodany** do `Packages/manifest.json` |
| Python `mlagents` (PyPI) | **1.1.0** (para do 4.1.0) | nie zainstalowany |
| Python `mlagents-envs` | **1.1.0** (zależność `mlagents`) | nie zainstalowany |
| Wersja Pythona dla ML-Agents 4.1 | **3.10.1–3.10.12** (zalecane 3.10.12) | lokalnie jest 3.12/3.13 — **wymaga osobnego venv/conda przed treningiem** |

**Źródła oficjalne (sprawdzone 2026-10-13):**

- Unity Manual — pakiet ML Agents dla edytora 6000.0: [com.unity.ml-agents](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ml-agents.html) (wersja **4.1.0** released dla Unity 6000.0).
- Instalacja toolkitu 4.1: [Installation (ML Agents 4.1)](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.1/manual/Installation.html) — Python 3.10.12, `pip install mlagents==1.1.0`.
- PyPI `mlagents` 1.1.0: [pypi.org/project/mlagents](https://pypi.org/project/mlagents/) — `python_requires` 3.10.1–3.10.12.
- Historia wydań: [GitHub releases ML-Agents](https://github.com/Unity-Technologies/ml-agents/releases).

## Wejście (Input) — wybór G0

| Element | Wartość | Uzasadnienie |
|---|---|---|
| System wejścia | **Input System Package (nowy)** — `activeInputHandler: 1` | Oficjalnie wydany dla Unity **6000.6** pakiet `com.unity.inputsystem` **1.20.1** ([dokumentacja 6000.6](https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.inputsystem.html)). Spójność z kierunkiem ML-Agents 4.x; stary `UnityEngine.Input` nie jest aktywny. |
| Pakiet w manifeście | `com.unity.inputsystem` **1.20.1** | `unity/FootballRL/Packages/manifest.json` |

Wersja **1.14.2** powodowała błędy kompilacji (CS0619) na Unity 6000.6.2f1; podniesiono do **1.20.1**.

## Fizyka

| Parametr | Wartość startowa |
|---|---|
| Fixed Timestep | **0,02 s** (50 Hz) | `ProjectSettings/TimeManager.asset` / `Time.fixedDeltaTime` ustawiane w narzędziu G0 |

## Jak otworzyć projekt Unity

1. **Unity Hub** → **Open** → wskaż folder:  
   `D:\projekty\Football-RL\unity\FootballRL`
2. Użyj edytora **6000.6.2f1** (lub tej samej linii 6000.6.x, jeśli zaktualizujesz — wtedy zaktualizuj ten dokument).
3. Po pierwszym otwarciu (opcjonalnie, idempotentne): menu **FootballRL → G0 → Bootstrap ManualPlayground** — odtwarza minimalną scenę i ustawienia bez duplikowania `Ground`, jeśli scena już istnieje.
4. Otwórz scenę: **`Assets/_Project/Scenes/ManualPlayground.unity`** (jest też w **Build Settings**).
5. Naciśnij **Play**, aby zobaczyć kamerę, światło kierunkowe i płaszczyznę `Ground`.

### Czego jeszcze nie ma (poza G0)

- Areny, zawodnika, piłki, bramek, skryptów gameplay (G1–G6).
- Pakietu `com.unity.ml-agents` i kodu agenta RL.
- Skonfigurowanego środowiska Python pod trening (`mlagents-learn`).

### Logi z weryfikacji batchmode (G0)

- Utworzenie projektu: `unity/g0-createProject.log`
- Bootstrap sceny i ustawień: `unity/g0-bootstrap.log`
