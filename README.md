# Recipe AI Helper

Aplikacja .NET 9 do automatycznego przetwarzania przepisów kulinarnych z plików PDF przy użyciu OpenAI API i zarządzania planami posiłków.

## Funkcjonalności

- **Ekstrakcja przepisów z PDF**: Automatyczne przetwarzanie plików PDF i wyciąganie przepisów, składników oraz wartości odżywczych przy użyciu OpenAI API
  - **Konfigurowalny model AI**: Możliwość wyboru modelu OpenAI (domyślnie: gpt-5-nano-2025-08-07 z 400k context window)
  - **Chunking z overlapem**: Inteligentne dzielenie dużych PDF (120+ stron) na mniejsze fragmenty po 10 stron z 1-stronicowym overlapem
  - **Ochrona przed utratą danych**: Overlap zapewnia, że przepisy rozłożone na 2 strony nie zostaną pominięte
  - **Format YAML**: Przepisy wyciągane są w ustrukturyzowanym formacie YAML dla lepszej dokładności
- **Zarządzanie plikami PDF**: Wybór konkretnych plików do przetworzenia z poziomu interfejsu WWW
- **Przeglądarka bazy danych**: Podgląd, edycja i usuwanie przepisów z bazy danych
- **Baza danych SQLite**: Przechowywanie wszystkich przepisów z pełnymi informacjami makroskładnikowymi
- **Losowanie posiłków**: Generowanie losowych planów posiłków na dzień (śniadanie, obiad, kolacja)
- **Planer tygodniowy**: Tworzenie jadłospisu na cały tydzień
- **Lista zakupów**: Automatyczne generowanie listy zakupów na podstawie wybranego planu
- **Integracja z Todoist**: Eksport listy zakupów bezpośrednio do Todoist
- **Wydruk jadłospisu**: Możliwość wydruku tygodniowego planu posiłków

## Wymagania

- .NET 9.0 SDK
- Klucz API OpenAI (zalecany model: gpt-5-nano-2025-08-07)
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
    "ApiKey": "TWOJ_KLUCZ_API_OPENAI",
    "Model": "gpt-5-nano-2025-08-07"
  },
  "Settings": {
    "PdfSourceDirectory": "C:\\Users\\Karolina\\Downloads\\Dieta",
    "DatabasePath": "recipes.db",
    "PagesPerChunk": 10,
    "OverlapPages": 1
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
2. **Skonfiguruj model AI** (opcjonalnie):
   - Wybierz model z listy (gpt-5-nano-2025-08-07, gpt-4o, gpt-4-turbo, gpt-4)
   - Lub wpisz własny model OpenAI
   - Zapisz konfigurację
3. **Zarządzaj plikami PDF**:
   - Załaduj listę plików PDF z folderu
   - Zaznacz pliki do przetworzenia
   - Uruchom przetwarzanie
4. **Przeglądaj bazę danych**:
   - Wyszukuj przepisy
   - Edytuj wartości odżywcze
   - Usuń niepotrzebne przepisy
5. **Planuj posiłki**:
   - Generuj dzienny plan posiłków
   - Twórz tygodniowy jadłospis
   - Generuj listę zakupów
   - Eksportuj do Todoist
   - Drukuj plan

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

## Modele OpenAI

Aplikacja domyślnie wykorzystuje **gpt-5-nano-2025-08-07**, najnowszy model OpenAI ze względu na:
- **Ogromny context window**: 400,000 tokenów (możliwość przetwarzania ~30-40 stron PDF na raz)
- **Duży output**: 128,000 max output tokens
- **Wsparcie reasoning tokens**: Lepsza jakość ekstrakcji
- Doskonałe możliwości przetwarzania dokumentów
- Wyciąganie strukturizowanych danych w formacie YAML
- Zrozumienie kontekstu kulinarnego (polskie przepisy)
- Możliwość estymacji wartości odżywczych

### Dostępne modele:

| Model | Context Window | Max Output | Zalecane strony PDF |
|-------|---------------|------------|---------------------|
| **gpt-5-nano-2025-08-07** (domyślny) | 400,000 | 128,000 | 30-40 stron |
| gpt-4o | 128,000 | 16,384 | 10-15 stron |
| gpt-4-turbo | 128,000 | 4,096 | 10-15 stron |
| gpt-4 | 8,192 | 4,096 | 5-8 stron |

### Konfiguracja modelu:

**W pliku konfiguracyjnym** (`appsettings.json`):
```json
{
  "OpenAI": {
    "Model": "gpt-5-nano-2025-08-07"
  }
}
```

**W interfejsie WWW**:
- Przejdź do sekcji "Konfiguracja Modelu AI"
- Wybierz model z listy lub wpisz własny
- Kliknij "Zapisz konfigurację"

### Strategia przetwarzania PDF:
- **Małe PDF-y (≤30 stron)**: Przetwarzane jako całość
- **Duże PDF-y (>30 stron)**: Dzielone na chunki po 30 stron z 2-stronicowym overlapem
- **Bardzo duże PDF-y (100+ stron)**: Zalecane chunki 30-40 stron dla gpt-5-nano
- **145-stronicowy PDF**: Przetwarzany w ~5 chunkach (145÷30 ≈ 5)

### Mechanizmy ochrony jakości:

**1. Sprawdzanie duplikatów:**
- Dokładne dopasowanie nazw (case-insensitive)
- Fuzzy matching (podobieństwo >80%)
- Kontekst ostatnich 10 przepisów przekazywany do AI

**2. Rate limiting:**
- Konfigurowalne opóźnienie między chunkami (domyślnie 3000ms)
- Zapobiega blokadom API
- Zalecane 3-5 sekund dla dużych PDF

**3. Walidacja:**
- Sprawdzanie kompletności danych (nazwa, składniki, instrukcje)
- Weryfikacja wartości odżywczych
- Szczegółowe logowanie każdego kroku
- Raportowanie błędów bez przerywania procesu

**4. Progress tracking:**
- Podział na chunki z procentowym postępem
- Podsumowanie dla każdego pliku
- Finalne statystyki przetwarzania

## Przykład przetwarzania 145-stronicowego PDF

Dla pliku `Fit-Dania-z-Restauracji-bkrfac_69121080a1362_e.pdf` (145 stron):

```
================================================================================
ROZPOCZĘCIE PRZETWARZANIA PDF
================================================================================
Folder: C:\Users\Karolina\Downloads\Dieta
Chunking: 30 stron per chunk, 2 stron overlap
Rate limiting: 3000ms opóźnienia między chunkami
Sprawdzanie duplikatów: TAK
================================================================================

📄 Znaleziono 1 plików PDF

================================================================================
📋 Przetwarzanie: Fit-Dania-z-Restauracji-bkrfac_69121080a1362_e.pdf
================================================================================
📊 PDF podzielony na 5 chunków

[Chunk 1/5] Strony 1-30
  Rozmiar tekstu: 45230 znaków
  Kontekst: 10 ostatnich przepisów w bazie
  ⏳ Wysyłanie do OpenAI (gpt-5-nano-2025-08-07)...
  ✅ Otrzymano 15 przepisów (czas: 8.3s)
    ✅ Zapisano: Pizza Margherita FIT (Obiad) - 380 kcal
    ✅ Zapisano: Burger z kurczaka (Obiad) - 450 kcal
    ...
  📈 Postęp pliku: 20%

  ⏸️  Oczekiwanie 3000ms przed następnym chunkiem...

[Chunk 2/5] Strony 29-58
  ...

✅ Zakończono plik: Fit-Dania-z-Restauracji-bkrfac_69121080a1362_e.pdf
   Chunków przetworzonych: 5
   Przepisów wyekstrahowanych: 73
   Przepisów zapisanych: 68
   Duplikatów pominiętych: 5
────────────────────────────────────────────────────────────────────────────────

================================================================================
🎉 PRZETWARZANIE ZAKOŃCZONE
================================================================================
📁 Plików przetworzonych: 1
📦 Chunków przetworzonych: 5
📋 Przepisów wyekstrahowanych: 73
✅ Przepisów zapisanych: 68
⏭️  Duplikatów pominiętych: 5
❌ Błędów: 0
📊 Obecna liczba przepisów w bazie: 68
================================================================================
```

## Konfiguracja zaawansowana

### Dostosowanie dla różnych rozmiarów PDF:

| Rozmiar PDF | Pages Per Chunk | Overlap | Delay (ms) | Model |
|-------------|----------------|---------|------------|-------|
| < 30 stron | 30 | 1 | 2000 | gpt-4o |
| 30-100 stron | 30 | 2 | 3000 | gpt-5-nano |
| 100-200 stron | 35 | 2 | 3000 | gpt-5-nano |
| > 200 stron | 40 | 3 | 4000 | gpt-5-nano |

### Przykładowa konfiguracja dla 145-stronicowego PDF:

```json
{
  "Settings": {
    "PagesPerChunk": 30,
    "OverlapPages": 2,
    "DelayBetweenChunksMs": 3000,
    "CheckDuplicates": true,
    "RecentRecipesContext": 10
  }
}
```

## Kategorie posiłków

Aplikacja obsługuje następujące typy posiłków:
- **Sniadanie** - Śniadania
- **Obiad** - Obiady
- **Kolacja** - Kolacje
- **Deser** - Desery
- **Napoj** - Napoje

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
