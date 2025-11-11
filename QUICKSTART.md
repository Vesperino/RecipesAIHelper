# Szybki Start - Recipe AI Helper

## Kroki instalacji

### 1. Konfiguracja API Key OpenAI

Skopiuj plik konfiguracyjny:
```bash
copy appsettings.example.json appsettings.json
```

Edytuj `appsettings.json` i wstaw swój klucz API OpenAI:
```json
{
  "OpenAI": {
    "ApiKey": "sk-proj-TWOJ_KLUCZ_API"
  }
}
```

**Jak uzyskać klucz API OpenAI:**
1. Wejdź na https://platform.openai.com/api-keys
2. Zaloguj się lub utwórz konto
3. Kliknij "Create new secret key"
4. Skopiuj klucz (zaczyna się od `sk-`)

### 2. Uruchomienie aplikacji

```bash
dotnet run
```

### 3. Przetwarzanie PDF

1. Umieść pliki PDF z przepisami w katalogu: `C:\Users\Karolina\Downloads\Dieta`
   (lub zmień ścieżkę w `appsettings.json`)

2. W menu wybierz opcję `1` - Process PDFs and extract recipes

3. Aplikacja przetworzy wszystkie pliki PDF i zapisze przepisy do bazy danych

### 4. Używanie interfejsu webowego

1. Otwórz plik `wwwroot/index.html` w przeglądarce

2. Funkcje dostępne:
   - **Generate Random Daily Plan** - losowy plan na dziś
   - **Generate Weekly Plan** - plan na cały tydzień
   - **Generate from Weekly Plan** - lista zakupów
   - **Export to Todoist** - wyeksportuj listę do Todoist
   - **Print Weekly Plan** - wydrukuj plan tygodniowy

## Rozwiązywanie problemów

### Problem: "OpenAI API key not configured"
**Rozwiązanie:** Upewnij się, że plik `appsettings.json` istnieje i zawiera prawidłowy klucz API

### Problem: "No PDF files found"
**Rozwiązanie:** Sprawdź czy ścieżka w `appsettings.json` jest prawidłowa i czy folder zawiera pliki PDF

### Problem: "Not enough recipes in database"
**Rozwiązanie:** Najpierw przetwórz pliki PDF używając opcji 1 w menu

## Koszty API OpenAI

Model **GPT-4o** kosztuje:
- Input: $2.50 / 1M tokenów
- Output: $10.00 / 1M tokenów

Przykładowo:
- Przetworzenie 1 PDF (10 stron) ≈ 5,000 tokenów = ~$0.025
- 100 PDF-ów ≈ $2.50

## Wsparcie

Problemy? Utwórz issue na GitHub:
https://github.com/Vesperino/RecipesAIHelper/issues
