# UniBoard - raport z testów Sprintu 1 (zadanie #10)

Zakres: zadania #1-#9 (repozytorium, SQL Server + EF Core, model danych, rejestracja/logowanie, role,
layout, formularz ogłoszenia, lista kart, strona szczegółów).

## 1. Metoda

| Metoda | Co obejmuje |
|---|---|
| Przegląd kodu (code review) | cały kod aplikacji: Models, Data, Pages, Program.cs, layout, README |
| Testy jednostkowe (xUnit) | walidacja modeli, logika PageModel-i (Index, Details, Create), DbSeeder, konfiguracja EF Core |
| Testy integracyjne (xUnit + `WebApplicationFactory`) | prawdziwe żądania HTTP do aplikacji: dostęp anonim / użytkownik / admin, formularz, lista, szczegóły |
| Testy manualne | scenariusze z tabeli w pkt 4 (do wykonania w przeglądarce) |

Baza w testach automatycznych: **SQLite w pamięci** (nie wymaga LocalDB, egzekwuje klucze obce i unikalne indeksy).
Testy NIE dotykają bazy deweloperskiej `UniBoard` w LocalDB.

> Uwaga: testy automatyczne są oznaczone `[Trait("Category", "KnownBug")]`, jeśli opisują znany, jeszcze
> nienaprawiony błąd. Takie testy są **celowo czerwone**, dopóki błąd nie zostanie poprawiony.

## 2. Znalezione błędy

| ID | Waga | Gdzie | Krótko | Właściciel zadania |
|---|---|---|---|---|
| BUG-01 | **Krytyczny** | `Pages/Announcements/Details.cshtml` (linia z `Substring(0, 1)`) | Szczegóły ogłoszenia kończą się błędem 500, jeśli autor nie ma nazwiska - a tak jest dla każdego konta założonego przez formularz rejestracji | Task 9 (Dominika) |
| BUG-02 | Wysoki | `Details.cshtml` (blok "Kontakt") | Pokazywany jest e-mail **konta** autora zamiast kontaktu z ogłoszenia; pole `Contact` nigdzie się nie wyświetla, a prywatny e-mail konta jest publicznie widoczny | Task 9 (Dominika) |
| BUG-03 | Niski | `Details.cshtml` (blok ceny) | Cena `0` to "Za darmo / Oddam" na liście, ale "0 zł" na stronie szczegółów | Task 9 (Dominika) |
| BUG-04 | Średni | `Pages/Shared/_Layout.cshtml` | Linki "Moje ogłoszenia", "Ulubione", "Kontakt" prowadzą do stron, które nie istnieją (404 / pusty href) | Task 6 (Dominika) |
| BUG-05 | Niski | `Pages/Announcements/Create.cshtml.cs` | Walidacja długości działa przed `Trim()`: tytuł `" ab "` przechodzi, a do bazy trafia `"ab"` (2 znaki < minimum 3) | Task 7 (Mamchenko) |
| BUG-06 | Niski (do potwierdzenia) | `Models/Announcement.cs`, `Create.cshtml.cs` | `[Range(0, 100000)]` ma granice typu `int`, a `Price` to `decimal` - ceny ułamkowe tuż poza zakresem (np. `-0,40`) mogą przejść walidację | Task 3 / 7 |

### Opis i propozycje poprawek

**BUG-01** - kroki: zarejestruj nowe konto (`/Identity/Account/Register`) -> dodaj ogłoszenie -> kliknij kartę na liście.
Oczekiwane: strona szczegółów. Faktyczne: błąd 500 (`ArgumentOutOfRangeException`, bo `"".Substring(0,1)`).
Przyczyna: formularz rejestracji nie zbiera imienia/nazwiska (domyślnie `""`), a widok zakłada, że nazwisko istnieje.
Poprawka (minimum): w widoku `@IndexModel.AuthorName(Model.Announcement.Author)` albo
`@(string.IsNullOrEmpty(last) ? "" : last[..1] + ".")`. Docelowo: dodać pola Imię/Nazwisko do rejestracji (są `[Required]` w modelu).

**BUG-02** - poprawka: `mailto:@Model.Announcement.Contact` i `@Model.Announcement.Contact` zamiast `Author?.Email`.

**BUG-03** - poprawka: warunek `Price.HasValue && Price.Value > 0` (jak w `Index.cshtml`).

**BUG-04** - poprawka: dodać puste strony-zaślepki albo ukryć linki do Sprintu 2.

**BUG-05** - poprawka: w `OnPostAsync` przyciąć pola przed walidacją (`Input.Title = Input.Title.Trim()`, potem `ModelState.Clear(); TryValidateModel(Input);`).

**BUG-06** - poprawka: `[Range(typeof(decimal), "0", "100000")]`. Test `BUG06_...` może okazać się zielony, jeśli
zachowanie `RangeAttribute` jest inne niż zakładamy - wtedy błąd usuwamy z raportu.

### Uwagi (nie błędy, do decyzji zespołu)

- Strony Rejestracja/Logowanie są po angielsku (domyślne szablony Identity), reszta aplikacji po polsku.
- Przyciski "Do ulubionych" i "Zgłoś" na stronie szczegółów nic nie robią (zaślepki).
- Pole ceny ma `step="1"` - nie da się wpisać groszy, choć baza przechowuje `decimal(10,2)`.
- Szczegóły ogłoszenia są publiczne (także dla niezalogowanych) razem z adresem e-mail - warto świadomie zdecydować, czy tak ma być.
- Data na liście jest w czasie lokalnym, na stronie szczegółów w UTC.
- `DbSeeder` uruchamia się przy starcie - jeśli migracje nie zostały zastosowane (`dotnet ef database update`), aplikacja nie wystartuje.
- Sprawdzone OK: `bin/`, `obj/`, `.vs/` nie są w repozytorium; dane administratora nie są w repozytorium (user-secrets).

## 3. Pokrycie testami automatycznymi

| Plik | Zakres |
|---|---|
| `Unit/ModelValidationTests.cs` | granice długości, cena, e-mail, kategoria, wymagane pola, ochrona przed overpostingiem |
| `Unit/IndexModelTests.cs` | 9 kart na stronę, liczba stron, korekta numeru strony, sortowanie, `Shorten`, `AuthorName` |
| `Unit/DetailsModelTests.cs` | 404 dla braku/nieznanego id, załadowanie kategorii i autora |
| `Unit/CreateModelTests.cs` | zapis ogłoszenia, autor i data z serwera, trim, błędna kategoria, brak użytkownika, `Challenge` |
| `Unit/DbSeederTests.cs` | role, konto admina z sekretów, idempotentność, słabe hasło, brak sekretów, kategorie startowe |
| `Unit/DbContextTests.cs` | relacje i `Restrict`, unikalna nazwa kategorii, indeksy, `decimal(10,2)` |
| `Integration/AccessControlTests.cs` | strony publiczne, przekierowanie do logowania, `/Admin` (anonim / User / Administrator), menu |
| `Integration/AnnouncementPagesTests.cs` | stronicowanie na prawdziwym HTML, szczegóły, XSS, pełny przepływ dodawania, antiforgery, nawigacja |

## 4. Testy manualne (do wykonania przed zamknięciem sprintu)

Przygotowanie: `dotnet ef database update`, ustawić user-secrets administratora (README), uruchomić aplikację.
Kolumna "Wynik": OK / BŁĄD (numer issue).

| ID | Zadanie | Kroki | Oczekiwany wynik | Wynik |
|---|---|---|---|---|
| TC-01 | #4 | Zarejestruj konto z poprawnym e-mailem i hasłem `Test123!` | Konto utworzone, użytkownik zalogowany, przekierowanie na `/` | |
| TC-02 | #4 | Rejestracja z hasłem `abc` | Komunikat o niespełnionych wymaganiach hasła | |
| TC-03 | #4 | Rejestracja z już użytym e-mailem | Komunikat, że adres jest zajęty | |
| TC-04 | #4 | Wyloguj się, zaloguj ponownie | Wylogowanie i logowanie działają | |
| TC-05 | #4 | Logowanie ze złym hasłem | Komunikat o nieudanym logowaniu | |
| TC-06 | #5 | Zaloguj jako zwykły użytkownik, wejdź na `/Admin` | Odmowa dostępu (Access denied) | |
| TC-07 | #5 | Wejdź na `/Admin` niezalogowany | Przekierowanie do logowania | |
| TC-08 | #5 | Zaloguj jako administrator (dane z user-secrets), wejdź na `/Admin` | Panel administratora widoczny, link w menu użytkownika | |
| TC-09 | #7 | Zalogowany: "+ Dodaj ogłoszenie", wypełnij poprawnie | Przekierowanie na listę, zielony komunikat, ogłoszenie na liście | |
| TC-10 | #7 | Wyślij pusty formularz | Komunikaty przy polach, nic nie zapisano | |
| TC-11 | #7 | Tytuł 2 znaki / 101 znaków; opis 2001 znaków; zły e-mail; cena -5 / 100001 | Odpowiednie komunikaty walidacji | |
| TC-12 | #7 | Cena pusta | Ogłoszenie zapisane, na liście "Za darmo / Oddam" | |
| TC-13 | #7 | Niezalogowany: wejdź na `/Announcements/Create` | Przekierowanie do logowania | |
| TC-14 | #8 | Dodaj 10 ogłoszeń | Pierwsza strona 9 kart, druga 1; paginacja działa; najnowsze na górze | |
| TC-15 | #8 | `/?strona=0`, `/?strona=999`, `/?strona=abc` | Strona się wyświetla (bez błędu 500) | |
| TC-16 | #9 | Kliknij kartę ogłoszenia (autor: konto z rejestracji) | Strona szczegółów (**tu wystąpi BUG-01**) | |
| TC-17 | #9 | `/Announcements/Details/99999` | Strona 404 | |
| TC-18 | #9 | Sprawdź pole "Kontakt" | Pokazany kontakt z ogłoszenia (**BUG-02**) | |
| TC-19 | #6 | Zmniejsz okno do szerokości telefonu | Menu zwija się do hamburgera, karty w jednej kolumnie | |
| TC-20 | #6 | Kliknij wszystkie linki w menu i stopce | Żaden nie prowadzi do 404 (**BUG-04**) | |
| TC-21 | #2 | Na czystej maszynie: klon repo, `dotnet ef database update`, start aplikacji | Baza utworzona, aplikacja startuje | |
