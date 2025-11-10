# Recipe AI Helper

Aplikacja .NET 9 do automatycznego przetwarzania przepisów kulinarnych z plików PDF przy użyciu OpenAI GPT-4o i zarządzania planami posiłków.

## Funkcjonalności

- **Ekstrakcja przepisów z PDF**: Automatyczne przetwarzanie plików PDF i wyciąganie przepisów, składników oraz wartości odżywczych przy użyciu OpenAI GPT-4o
- **Baza danych SQLite**: Przechowywanie wszystkich przepisów z pełnymi informacjami makroskładnikowymi
- **Losowanie posiłków**: Generowanie losowych planów posiłków na dzień (śniadanie, obiad, kolacja)
- **Planer tygodniowy**: Tworzenie jadłospisu na cały tydzień
- **Lista zakupów**: Automatyczne generowanie listy zakupów na podstawie wybranego planu
- **Integracja z Todoist**: Eksport listy zakupów bezpośrednio do Todoist
- **Wydruk jadłospisu**: Możliwość wydruku tygodniowego planu posiłków

## Wymagania

- .NET 9.0 SDK
- Klucz API OpenAI (zalecany model: GPT-4o)
- (Opcjonalnie) Klucz API Todoist do eksportu list zakupów

## Instalacja

1. Sklonuj repozytorium:
```bash
git clone https://github.com/Vesperino/RecipesAIHelper.git
cd RecipesAIHelper
```

2. Przywróć pakiety NuGet:
```bash
dotnet restore
```

3. Skonfiguruj klucz API OpenAI:

Skopiuj plik `appsettings.example.json` do `appsettings.json` i uzupełnij:

```json
{
  "OpenAI": {
    "ApiKey": "TWOJ_KLUCZ_API_OPENAI"
  },
  "Todoist": {
    "ApiKey": "TWOJ_KLUCZ_API_TODOIST"
  },
  "Settings": {
    "PdfSourceDirectory": "C:\\Users\\Karolina\\Downloads\\Dieta",
    "DatabasePath": "recipes.db"
  }
}
```

Alternatywnie, ustaw zmienną środowiskową:
```bash
set OPENAI_API_KEY=twój_klucz_api
```

## Użycie

### Aplikacja konsolowa

Uruchom aplikację:
```bash
dotnet run
```

Menu aplikacji:
1. **Process PDFs and extract recipes** - Przetwarzaj pliki PDF z katalogu i wyciągaj przepisy
2. **Get random meal suggestions** - Otrzymaj losowe sugestie posiłków na dziś
3. **View all recipes** - Wyświetl wszystkie przepisy w bazie danych
4. **Exit** - Wyjście z aplikacji

### Interfejs WWW

1. Otwórz plik `wwwroot/index.html` w przeglądarce
2. Użyj przycisków do:
   - Generowania dziennego planu posiłków
   - Tworzenia tygodniowego jadłospisu
   - Generowania listy zakupów
   - Eksportu do Todoist
   - Wydruku planu

## Struktura projektu

```
RecipesAIHelper/
├── Models/
│   ├── Recipe.cs                    # Model przepisu
│   └── RecipeExtractionResult.cs   # Model wyników ekstrakcji z OpenAI
├── Services/
│   ├── PdfProcessorService.cs      # Obsługa plików PDF
│   └── OpenAIService.cs            # Integracja z OpenAI API
├── Data/
│   └── RecipeDbContext.cs          # Obsługa bazy danych SQLite
├── wwwroot/
│   ├── index.html                  # Interfejs WWW
│   ├── styles.css                  # Style CSS
│   └── app.js                      # Logika JavaScript
├── Program.cs                       # Główna aplikacja konsolowa
└── appsettings.json                # Konfiguracja (nie w repo)
```

## Model OpenAI

Aplikacja wykorzystuje **GPT-4o** (gpt-4o), który został wybrany ze względu na:
- Doskonałe możliwości przetwarzania dokumentów
- Wyciąganie strukturyzowanych danych w formacie JSON
- Zrozumienie kontekstu kulinarnego
- Możliwość estymacji wartości odżywczych

## Kategorie posiłków

Aplikacja obsługuje następujące typy posiłków:
- Breakfast (Śniadanie)
- Lunch (Obiad)
- Dinner (Kolacja)
- Dessert (Deser)
- Snack (Przekąska)
- Appetizer (Przystawka)

## Makroskładniki

Dla każdego przepisu przechowywane są:
- Kalorie (kcal)
- Białko (g)
- Węglowodany (g)
- Tłuszcze (g)

## Integracja z Todoist

Aby eksportować listę zakupów do Todoist:
1. Uzyskaj klucz API z https://todoist.com/prefs/integrations
2. Wprowadź go w interfejsie WWW podczas eksportu
3. Lista zostanie dodana do Twojego Todoist

## Licencja

MIT License

## Autor

Vesperino

## Wsparcie

W razie problemów, utwórz issue na GitHubie:
https://github.com/Vesperino/RecipesAIHelper/issues
