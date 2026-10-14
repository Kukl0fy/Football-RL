# Kontrakty i mechanika v1

To specyfikacja do zaimplementowania. Wielkości startowe mogą zostać dostrojone w G2–G4, a wartości po odbiorze należy zapisać. Interfejs komend ma pozostać stabilny.

## 1. Świat i reguły

Unity: Y do góry, ruch gracza na XZ. Lokalne +Z gracza to przód, +X to prawo. Arena może być przesunięta i obrócona wokół Y; dane scenariusza są w układzie areny. Skala transformu areny = 1.

Start: pole 20 × 12 jednostek; przyjmujemy 1 jednostkę = 1 metr. Bramki szerokości 3 m, wysokości 2 m, głębokości 1 m na przeciwległych krótkich bokach. Piłka promienia 0,22 m; gracz około 0,8 × 1 × 0,8 m. To propozycje do sprawdzenia, nie fizyka prawdziwego futbolu.

Ściany odbijają piłkę, ale pozostawiają rzeczywisty otwór bramki. Nie dodajemy niewidzialnego sufitu. Piłka może opuścić arenę po lobie; wtedy następuje BallOut. Pole dozwolone obejmuje wnętrza bramek. Dodaj osobną dolną granicę awaryjną na wypadek upadku przez podłoże.

Pchanie piłki jest legalne. Gol z pchnięcia liczy się jak gol z kopnięcia. Jeśli właściciel będzie chciał ćwiczyć wyłącznie kopnięcia, rozwiązuje to osobny scenariusz i pomiar, nie ukryta zmiana zasad meczu.

## 2. PlayerCommand — wspólne wejście

| Pole | Zakres | Znaczenie |
|---|---|---|
| `Forward` | −1…1 | tył/przód względem aktualnej orientacji |
| `Strafe` | −1…1 | lewo/prawo |
| `Turn` | −1…1 | obrót wokół osi Y; dodatni w prawo |
| `Kick` | −1…1 | brak kopnięcia do progu; powyżej progu siła |
| `Loft` | −1…1 | kąt: `27.5 * (Loft + 1)` stopni, czyli 0…55° |

To pięć liczb, wspólnych dla klawiatury i przyszłej polityki ciągłej. Ruch ogranicz przez ClampMagnitude do długości 1, aby po przekątnej nie był szybszy.

Próg kopnięcia t = 0,2. Dla Kick ≤ t brak próby. Powyżej t: `power = (Kick-t)/(1-t)`; impuls = `maxImpulse * power`. Nie dodajemy automatycznego minimalnego silnego strzału. Parametr maxImpulse stroimy; startowo 8 N·s dla piłki 0,45 kg.

Komenda zerowa nie rusza i nie kopie. Loft = 0 oznacza 27,5°, ale sam nie powoduje kopnięcia. W ręcznym trybie startowy wybrany kąt wynosi 0° (Loft = −1).

## 3. Dostarczenie i czas życia komendy

Projektowane publiczne wejście: `SubmitCommand(playerId, command, commandId)`. `commandId` to rosnący numer decyzji dla gracza w danym epizodzie. Nie myl go z numerem klatki renderowania.

- Ruch i obrót z ostatniej komendy utrzymują się do następnej komendy lub resetu.
- Przy każdej nowej decyzji agenta zwiększaj commandId, nawet gdy akcja jest identyczna jak poprzednia.
- Każdy nowy commandId daje najwyżej jedną próbę kopnięcia, na pierwszym obsługującym go kroku fizyki.
- Ponowne przekazanie tego samego commandId (ta sama już wysłana decyzja) nie powtarza kopnięcia.
- Nieudana próba (poza zasięgiem/cooldown) jest zużyta; nie czeka na dogodny moment.
- Kolejne decyzje z dodatnią komendą Kick mogą kopnąć ponownie, gdy cooldown minie.
- Nowa komenda przed krokiem fizyki zastępuje poprzednią nieobsłużoną. Kontrakt zakłada najwyżej jedną decyzję RL na krok fizyki; klawiatura zatrzaskuje pojedyncze naciśnięcie do następnego kroku.
- Reset zeruje komendy i numerację. Dane ze starego epizodu są odrzucane przez koordynatora (EpisodeId).

Ta reguła zapobiega wielokrotnemu impulsowi, gdy adapter powtarza tę samą akcję pomiędzy decyzjami sieci.

## 4. Ruch i kopnięcie

Gracz: dynamiczny Rigidbody, grawitacja, zablokowane przechyły wokół X/Z. Chodzi po płaskim podłożu. Kontrolowany ruch z ograniczoną szybkością dochodzenia do prędkości docelowej; startowo 4 m/s, przyspieszenie 20 m/s², obrót 180°/s. Zachowaj pionową składową fizyki. Unikaj bezpośredniego teleportowania przez Transform.

Piłka: dynamiczny Rigidbody, grawitacja, tarcie i odbicia przez materiały fizyczne, odpowiednie wykrywanie szybkich kolizji. Bez podkręcania/Magnusa w v1. Kąt kopnięcia dotyczy impulsu, nie gwarantowanego kąta całego toru już poruszającej się piłki.

Kopnięcie możliwe, gdy środek piłki jest do 1,1 m od punktu kopnięcia, w stożku ±45° względem przodu gracza, na wysokości środka ≤0,5 m nad podłożem, a cooldown wynosi zero. Sprawdzaj też brak przeszkody pomiędzy punktem kopnięcia a piłką. Punkt kopnięcia skonfiguruj jawnie na prefabie; wyklucz własny collider przy teście przesłonięcia.

Kierunek poziomy impulsu = przód gracza. Nie ma automatycznego celowania w bramkę ani w kolegę. Kierunek 3D = `forward*cos(angle) + up*sin(angle)`. Pojedynczy AddForce w trybie Impulse w środku masy piłki. Cooldown 0,25 s zaczyna się tylko po udanym kopnięciu. Udane kopnięcie emituje zdarzenie z graczem, siłą i kątem.

Dostępność kopania/kąta jest parametrem scenariusza. W nawigacji można wyłączyć kopanie, a w pierwszych strzałach wymusić kąt 0° — to jawna konfiguracja, zapisana w eksperymencie.

## 5. Gol i koniec

Gol następuje po przejściu całej piłki przez płaszczyznę linii bramkowej od strony boiska, wewnątrz otworu pomiędzy słupkami i pod poprzeczką. Promień piłki jest uwzględniony. Bramka ma jawny kierunek i identyfikator broniącej drużyny.

Nie wystarczy samo `OnTriggerEnter`. Przy szybkim locie potrzebne jest sprawdzenie przejścia między poprzednią i aktualną pozycją oraz zgodności z otworem. Po starcie/resetowaniu detektor zapamiętuje nową pozycję bez naliczania gola. Jeden gol kończy rundę w v1; wynik całego wielorundowego meczu to przyszła osobna warstwa.

Zdarzenie `RoundEnded` zawiera EpisodeId, powód, ewentualną drużynę zdobywającą gola, czas symulacji i końcowy snapshot. Brak gola oznacza brak scoringTeam, nie fikcyjną drużynę 0. Samobój rozstrzyga geometria bramki; ostatni kontakt jest statystyką, nie warunkiem gola.

## 6. Snapshot — stan surowy, jeszcze nie obserwacje RL

`CaptureSnapshot()` zwraca spójny odczyt bez zmiany gry, w ustalonym punkcie cyklu. Dane świata przelicza na lokalny układ areny. Nie normalizuje ich pod sieć.

| Część | Pola |
|---|---|
| Arena | contractVersion, EpisodeId, tick, elapsedSeconds, remainingSeconds, phase, wymiary |
| Piłka | present, pozycja XYZ, prędkość XYZ, prędkość kątowa XYZ |
| Gracz | PlayerId, TeamId, pozycja, forward, prędkość, prędkość obrotu Y, cooldownRemaining, canKickNow |
| Bramki | TeamId broniącego, środek linii, kierunek, szerokość, wysokość |
| Cel ćwiczenia | present, pozycja, promień sukcesu |
| Przeszkody | stabilne ID, pozycja, rozmiar, orientacja; lista może być pusta |
| Koniec rundy | opcjonalny powód oraz scoringTeam |

Gracze i przeszkody mają stabilny porządek według ID. `canKickNow` wynika z tych samych warunków co rzeczywiste kopanie. Cooldown może być dodatkowo udostępniony jako 0…1, ale surowe sekundy pozostają dostępne.

Snapshot może zawierać więcej informacji niż późniejsza obserwacja agenta. Dopiero właściciel wybiera, co agent widzi i jak to normalizuje. Pełny stan nie zobowiązuje do dania agentowi informacji o całym boisku.

## 7. Reset

`ResetEpisode(resetRequest)` przyjmuje seed, scenariusz i opcjonalny dokładny stan startowy. Kontroler sprawdza poprawność przed zmianą świata: obiekty w granicach, bez nakładania, poprawne ID, skończone liczby. Liczba prób losowania jest ograniczona; niepowodzenie daje czytelny błąd, nigdy nieskończoną pętlę.

Sekwencja: zablokuj komendy, ustaw pozycje i obroty, wyzeruj prędkości liniowe/kątowe i cooldowny, usuń poprzednie zdarzenia i kontakty logiczne, zresetuj detektory i zegar, nadaj nowe EpisodeId, zsynchronizuj stan fizyki w razie potrzeby, udostępnij stan startowy i przejdź do Running. Reset nie wykonuje starej komendy w nowym epizodzie.

Seed daje powtarzalne losowanie startu. Nie obiecujemy idealnie identycznej trajektorii fizyki między różnymi systemami i wersjami Unity.

## 8. Sterowanie ręczne

W/S przód/tył, A/D strafowanie, Q/E obrót, Spacja pojedyncze kopnięcie, strzałki góra/dół kąt, lewo/prawo siła, R reset. HUD pokazuje aktualne wartości. Utrata fokusu zeruje ruch i kasuje oczekujące kopnięcie.

Nie rezerwujemy przycisku na skok. Kamera jest prosta, pokazuje całą arenę. Ozdobna grafika nie jest kryterium odbioru.
