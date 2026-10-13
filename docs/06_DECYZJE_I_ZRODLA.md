# Decyzje i źródła

## Ustalone decyzje v1

| Decyzja | Powód |
|---|---|
| Gra niezależna od ML-Agents | Właściciel chce sam wykonać integrację i rozumieć uczenie |
| Unity Physics, prosta grafika | Czas ma iść w RL, nie budowę silnika |
| Ruch po podłożu, piłka 3D | Stabilny zawodnik i możliwość późniejszych lobów |
| Pięć akcji ciągłych | Prosty punkt startu dla własnego PPO |
| Próba kopnięcia raz na nowy commandId | Brak przypadkowego powielania impulsów |
| Pchanie jest legalne | Jawne i spójne reguły gry |
| Jeden gol kończy rundę | Prostszy pierwszy epizod i reset |
| Ostatni stan zachowany do jawnego resetu | Poprawne przejścia w treningu |
| Surowy snapshot oddzielony od obserwacji | Właściciel sam projektuje wejście sieci |
| 2 vs 2 i self-play poza pierwszym odbiorem | Ograniczenie liczby jednocześnie rozwiązywanych problemów |

## Decyzje otwarte, ale nieblokujące dokumentacji

- Dokładna wersja Unity/ML-Agents/Python: G0, na podstawie dostępnego środowiska i aktualnego zgodnego wydania.
- Parametry ruchu, tarcia, odbić i strzału: strojenie G2–G4 i zapis końcowych wartości.
- Rozmiar sieci, nagrody, obserwacje i hiperparametry PPO: właściciel, po działającej grze.
- Kiedy rozpocząć własne PPO: po pierwszych działających treningach, zależnie od postępów nauki.
- Docelowa liczba aren: po pomiarze wydajności na komputerze użytkownika.

## Czego nie obiecuje ta architektura

Nie gwarantuje czasu treningu, nauczenia wszystkich zachowań ani bitowej deterministyczności fizyki. Nie jest wykonanym projektem Unity. Nie wymaga jednej sieci na zawsze ani nie gwarantuje transferu wag między różnymi schematami obserwacji.

## Źródła

Podstawą zakresu jest przekazany plan Football RL i ustalenia z rozmowy. Architektura oraz szczegółowe kontrakty są propozycją projektową przygotowaną dla tego projektu.

Oficjalne źródła techniczne sprawdzone przy analizie planu 13.10.2026; implementator powinien wybrać dokumentację odpowiadającą przypiętemu wydaniu, ponieważ gałąź develop może się zmieniać:

- Trening i `mlagents-learn`: https://github.com/Unity-Technologies/ml-agents/blob/develop/com.unity.ml-agents/Documentation~/Training-ML-Agents.md
- Projektowanie agentów, obserwacji i decyzji: https://github.com/Unity-Technologies/ml-agents/blob/develop/com.unity.ml-agents/Documentation~/Learning-Environment-Design-Agents.md
- Python LLAPI, DecisionSteps, TerminalSteps i przerwania: https://github.com/Unity-Technologies/ml-agents/blob/develop/com.unity.ml-agents/Documentation~/Python-LLAPI.md
- Zgodne zestawy pakietów i wydania: https://github.com/Unity-Technologies/ml-agents/releases

Nazwy metod `SubmitCommand`, `CaptureSnapshot`, `ResetEpisode` oraz modułów gry w tym pakiecie są proponowanym API projektu. Nie pochodzą z gotowego pakietu Unity.
