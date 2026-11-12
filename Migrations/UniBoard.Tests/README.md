# UniBoard.Tests

Testy automatyczne (xUnit, .NET 8) dla aplikacji UniBoard.

## Uruchomienie

Z katalogu głównego repozytorium (tam, gdzie leży `UniBoard.sln`):

    dotnet test UniBoard.Tests/UniBoard.Tests.csproj

Tylko "zielone" testy (bez znanych błędów):

    dotnet test UniBoard.Tests/UniBoard.Tests.csproj --filter "Category!=KnownBug"

Tylko testy znanych błędów (powinny być czerwone, dopóki błędy nie zostaną naprawione):

    dotnet test UniBoard.Tests/UniBoard.Tests.csproj --filter "Category=KnownBug"

W Visual Studio: Test -> Test Explorer -> Run All Tests.

Nie jest potrzebny LocalDB ani konto administratora - testy używają SQLite w pamięci.

## Struktura

    Helpers/       baza SQLite w pamięci, kontener DI z Identity, generatory danych testowych
    Unit/          testy jednostkowe (modele, PageModel-e, DbSeeder, DbContext)
    Integration/   testy HTTP na uruchomionej w pamięci aplikacji (WebApplicationFactory)
    TEST_REPORT.md raport: znalezione błędy + scenariusze testów manualnych

## Konwencja

- Test znanego błędu: nazwa zaczyna się od `BUGxx_` i ma `[Trait("Category", "KnownBug")]`.
- Po naprawie błędu: usuń atrybut `Trait`, test zostaje jako zwykły test regresji.
