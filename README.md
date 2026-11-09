# UniBoard

## Uruchomienie bazy danych

Wymagania: .NET 8 SDK, SQL Server LocalDB oraz narzędzie `dotnet-ef`:

    dotnet tool install --global dotnet-ef --version 8.0.*

Po sklonowaniu repozytorium i po każdym pull'u z nowymi migracjami:

    dotnet ef database update

Connection string domyślnie wskazuje na `(localdb)\MSSQLLocalDB`, baza: `UniBoard`.

## Konto administratora (user-secrets)

Dane administratora nie są w repozytorium. Każdy ustawia je lokalnie, w folderze projektu (tam, gdzie leży `UniBoard.csproj`):

    dotnet user-secrets init
    dotnet user-secrets set "Admin:Email" "admin@uniboard.local"
    dotnet user-secrets set "Admin:Password" "TwojeHaslo1!"

Hasło musi spełniać wymagania Identity (min. 6 znaków, wielka i mała litera, cyfra, znak specjalny).

Role (`Administrator`, `User`) i konto administratora tworzą się automatycznie przy starcie aplikacji.
Jeśli sekretów brak, aplikacja działa, ale konto administratora nie zostanie utworzone (ostrzeżenie w logu).
Strona dostępna tylko dla administratora: `/Admin`.
