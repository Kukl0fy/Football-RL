# Odbiór i testy

Celem testów jest wykrycie błędów, które mogłyby zostać pomylone z problemem RL. Nie testujemy każdego gettera. Testy czysto logiczne wykonuj jako EditMode, fizykę i reset jako PlayMode, sterowanie i czytelność jako próbę ręczną.

## Obowiązkowe przypadki

| ID | Próba | Oczekiwany wynik |
|---|---|---|
| T01 | Ruch prosto i po przekątnej w pustej arenie | Ta sama maksymalna szybkość pozioma |
| T02 | Obrót 90° i ruch do przodu | Ruch względem gracza, nie świata |
| T03 | Przestawienie FPS renderowania | Porównywalny ruch przez tę samą liczbę kroków fizyki |
| T04 | Komenda zerowa | Brak kopnięcia i zadanej prędkości |
| T05 | Ta sama dodatnia komenda i ten sam commandId przez wiele kroków | Dokładnie jeden impuls |
| T06 | Nowa próba podczas cooldownu | Brak impulsu i brak oczekującego strzału po jego upływie |
| T07 | Piłka za graczem, za ścianą, poza zasięgiem, za wysoko | Brak kopnięcia w każdym przypadku |
| T08 | Dwa poziomy siły, identyczny start piłki | Większy impuls daje większą zmianę prędkości |
| T09 | Kąt 0° i 45°, identyczny start | Pierwszy impuls bez składowej Y, drugi z dodatnią Y |
| T10 | Piłka przechodzi prawidłowo przez bramkę | Jeden Goal i właściwa drużyna zdobywająca |
| T11 | Piłka nad poprzeczką lub obok słupka | Brak gola |
| T12 | Szybki przelot i powrót/pozostanie w bramce | Wykryty poprawny gol, bez podwójnego naliczania |
| T13 | Timeout i ręczny reset | Poprawne, odróżnialne powody zakończenia |
| T14 | Reset w trakcie ruchu i cooldownu | Zerowe prędkości/komendy/cooldown, nowy zegar i EpisodeId |
| T15 | Odczyt po Goal, przed resetem | Dostępny stan końcowy, bez przeskoku do nowego startu |
| T16 | Ten sam seed i konfiguracja resetu | Te same pozycje startowe, nie obietnica identycznej całej fizyki |
| T17 | Wiele losowych resetów i niemożliwy spawn | Poprawne starty albo czytelny błąd po limicie prób |
| T18 | Dwie areny, reset tylko jednej | Druga zachowuje stan, wynik i własne komendy |
| T19 | Przesunięta i obrócona arena | Dane lokalne oraz działanie bramek nadal poprawne |
| T20 | BallOut i piłka lecąca w stronę sąsiedniej areny | Koniec rundy, brak oddziaływania na drugą arenę |
| T21 | Strzał ręczny, lob, odbicie, pchanie do bramki | Wszystkie legalne zachowania działają |
| T22 | Uruchomienie bez pakietu ML-Agents | Gra działa i kompiluje się |

T03 może być krótkim pomiarem ręcznym z zapisanym wynikiem. Nie wymuszaj bitowej identyczności symulacji. W testach trajektorii używaj jawnych tolerancji.

## Minimalna automatyzacja

Obowiązkowo zautomatyzuj: normalizację ruchu, mapowanie kopnięcia, deduplikację commandId, reset stanów, jednokrotne zakończenie oraz niezależność dwóch aren. Pozostałe przypadki mogą początkowo mieć udokumentowaną próbę ręczną; gola przy szybkim przelocie warto objąć PlayMode, bo regresja byłaby trudna do wykrycia podczas treningu.

## Raport G6

Dla każdego ID wpisz PASS / FAIL / NOT RUN, wersję Unity, datę i krótki dowód. Pokaż instrukcję uruchomienia sceny i sterowania. Dodaj wynik próby builda lub powód braku próby. Nie utożsamiaj braku błędów w analizie statycznej z działającym buildem.

## Kontrola przed treningiem

Właściciel potrafi: ręcznie wykonać zadanie; odczytać poprawny stan; odtworzyć start; zakończyć i zresetować epizod; rozpoznać, czy kopnięcie było możliwe. Jeżeli którykolwiek punkt nie działa, najpierw napraw środowisko.
