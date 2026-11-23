# ⚽ FoosBot — Teams MVP (.NET 10, C#)

Prototyp bota do firmowych piłkarzyków. Jedna kolejka na konwersację Teams, jedna fizyczna gra na raz.

## Zasady (ważne)

1. `+1` zapisuje zalogowanego użytkownika do kolejki; `-1` wypisuje **wyłącznie jego samego**.
2. Cztery osoby to **gotowy skład, ale nie automatyczne GO**.
3. `GO!` musi kliknąć jedna z pierwszej czwórki; serwer przed zmianą stanu sprawdza: czy stół wolny i czy nadal jest 4+ oczekujących.
4. Pierwsza czwórka przechodzi do `Playing`. Pozostali czekają w `Waiting` (również 4+ kolejnych).
5. **Następny skład nigdy nie uruchomi meczu podczas trwającej gry**, nawet klikając starą kartę z odblokowanym GO.
6. **Zakończ mecz** może kliknąć tylko członek aktualnie grającego składu. Czyści `Playing`, zachowuje kolejkę i NIE startuje następnego meczu.
7. `-1` jest możliwe także przy 4/4 (do rozpoczęcia gry). W trakcie meczu `-1` nie usuwa aktywnych graczy; można wypisać się z kolejki na następną grę.

## W zestawie

- `src/FoosBot/` — ASP.NET Core bot, Microsoft.Teams.Apps **2.2.0**, Adaptive Card 1.5, maszyna stanów;
- `tests/FoosBot.Tests/` — testy (w tym równoczesne GO, blokada kolejnej czwórki, -1, koniec);
- `teams-manifest/` — szablon manifestu Teams + dwie ikony;
- `demo/foosbot-demo.html` — samodzielna symulacja w przeglądarce (BEZ Teams, BEZ backendu).

## Szybki test lokalny w przeglądarce

Otwórz `demo/foosbot-demo.html` w Edge/Chrome. Wybierz gracza, kliknij +1. Możesz też użyć **„Scenariusz 2 składów”** i sprawdzić, że drugi GO jest niemożliwy, aż pierwszy zakończy grę. Demo nie łączy się z Teams i jest osobną symulacją UI.

## Testy i uruchomienie .NET

Wymaga **.NET 10 SDK** i Visual Studio z obsługą .NET 10 (lub terminala):

```powershell
dotnet restore FoosBot.slnx
dotnet test FoosBot.slnx
dotnet run --project src/FoosBot/FoosBot.csproj --launch-profile FoosBot-LocalPlayground-ONLY
```

Endpoint `GET http://localhost:3978/health` → status `ok`. Endpoint bota obsługuje Teams SDK.

**Uwaga o uwierzytelnianiu**: profil `FoosBot-LocalPlayground-ONLY` celowo włącza `AzureAd__DangerouslyAllowUnauthenticatedRequests=true` **WYŁĄCZNIE dla lokalnego playgroundu**. Tego profilu ani ustawienia nie wolno udostępniać publicznie ani używać podczas integracji z rzeczywistym Teams. Lokalny test interakcji z Teams można wykonać przez Microsoft 365 Agents Playground (w wersji obsługującej Teams SDK).

## Teams — czynności integracyjne

1. Zarejestruj bota / aplikację Entra w testowym tenantcie Microsoft 365. Uzyskaj **Tenant ID**, **Client ID** i sekret klienta (albo inną obsługiwaną metodę auth).
2. Ustaw w **User Secrets** lub bezpiecznych zmiennych środowiskowych `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__ClientCredentials__0__SourceType=ClientSecret`, `AzureAd__ClientCredentials__0__ClientSecret`. Nigdy nie wpisuj sekretu do manifestu lub repozytorium.
3. Uruchom usługę pod **publicznym HTTPS** (np. Azure App Service). Nie włączaj `DangerouslyAllowUnauthenticatedRequests` na publicznym hoście. Skonfiguruj w Azure Bot/Teams channel adres komunikatów bota dla hostowanego endpointu, zgodnie z aktualną instrukcją używanego portalu (w Teams SDK typowo `/api/messages`).
4. W `teams-manifest/manifest.template.json` zamień oba GUID-y zerowe na **Client ID** odpowiedniej rejestracji bota; `id` może być osobnym GUID Teams app, jeśli używasz osobnej identyfikacji pakietu. Uzupełnij metadane dewelopera i adresy polityk prywatności/regulaminu własnymi działającymi HTTPS URL.
5. Z katalogu `teams-manifest` spakuj **manifest jako `manifest.json`** wraz z `color.png`, `outline.png` do **osobnego ZIP** (bez katalogu nadrzędnego), a następnie przekaż przez Teams Developer Portal / Upload custom app. Organizacja może wymagać zgody administratora na sideload i instalację.
6. Dodaj aplikację do kanału lub czatu. Wywołaj `@FoosBot status`, następnie korzystaj z Adaptive Card; bot akceptuje także `+1`, `-1`, `go`, `koniec`, `status` w wiadomości skierowanej do niego.

> Manifest jest **szablonem**, a nie gotowym pakietem do zaimportowania: wymagane są prawidłowe identyfikatory i konfiguracja Teams w tenantcie.

## Ważne ograniczenia MVP

- **In-memory**: restart procesu kasuje aktywny mecz i kolejkę. Wiele replik aplikacji może mieć rozbieżne stany (lock obejmuje tylko jeden proces). Do produkcji: relacyjna baza danych/Redis + transakcyjne blokady/wersjonowanie.
- Po każdej akcji bot wysyła nową kartę statusu. Starsze karty mogą być klikalne, lecz ich akcje są ponownie weryfikowane na serwerze. W wersji produkcyjnej warto **edytować jedną przypiętą kartę** zamiast generować nowe wiadomości.
- Identyfikacja graczy opiera się na **Teams sender ID**, nie na imieniu w karcie (zapis cudzej osoby niemożliwy). Ochrona transportu zależy od poprawnej konfiguracji uwierzytelniania Teams SDK.
- Jeden stół na konwersację; przy kilku stołach użyj dodatkowego `tableId`.
- Integracja nie jest tu uruchomiona w rzeczywistym tenantcie Microsoft 365; solucja wymaga kompilacji i testu w Twoim środowisku Windows/.NET. Nie ma gwarancji wdrożenia bez dostosowania konfiguracji portalu.

## Źródła implementacji i deploymentu

- Microsoft, Teams SDK for .NET (wersja 2.2.0, 5.11.2026): https://learn.microsoft.com/en-us/dotnet/teams-sdk-dotnet/
- Microsoft Teams SDK 2.1+ — migracja / API: https://learn.microsoft.com/en-us/microsoftteams/platform/teams-sdk/migrations/v2-dotnet
- Microsoft, Adaptive Card actions (Action.Execute): https://learn.microsoft.com/en-us/microsoftteams/platform/task-modules-and-cards/cards/cards-actions
- Microsoft Teams SDK Bot Cards .NET sample: https://github.com/OfficeDev/Microsoft-Teams-Samples/tree/main/samples/TeamsSDK/bot-cards/dotnet/bot-cards
