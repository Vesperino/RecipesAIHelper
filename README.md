# Recipe AI Helper

Aplikacja .NET 9 do automatycznego przetwarzania przepisów kulinarnych z plików PDF przy użyciu AI (OpenAI GPT i Google Gemini) oraz zarządzania planami posiłków.

## Funkcjonalności

### 🤖 Multi-Provider AI System
- **OpenAI GPT**: Wsparcie dla GPT-4o, GPT-5 Mini z Vision API
- **Google Gemini**: Gemini 2.5-flash z bezpośrednim przetwarzaniem PDF
- **Dynamiczne przełączanie**: Wybór providera przez priorytet w bazie danych
- **Wspólne prompty**: Jednolity system promptów dla wszystkich providerów

### 📄 Ekstrakcja przepisów z PDF
- **Dwie ścieżki przetwarzania**:
  - Direct PDF upload (Gemini) - niższe koszty, szybsze przetwarzanie
  - PDF → Images → Vision API (OpenAI & Gemini) - wysoka jakość OCR przy 1200 DPI
- **Chunking z overlapem**: Inteligentne dzielenie dużych PDF (120+ stron) na mniejsze fragmenty po 3 strony z 1-stronicowym overlapem
- **Ochrona przed utratą danych**: Overlap zapewnia, że przepisy rozłożone na 2 strony nie zostaną pominięte
- **Multi-variant nutrition data**: Ekstrakcja wielu wariantów wartości odżywczych (np. "całość", "porcja", "1/2 porcji")
- **Upload przez UI**: Możliwość uploadowania plików PDF bezpośrednio przez interfejs webowy

### 🖼️ Generowanie obrazów AI
- **OpenAI**: DALL-E 2, DALL-E 3, GPT Image 1, GPT Image 1 Mini
- **Google Gemini**: Imagen 4.0 Ultra
- **Batch generation**: Automatyczne generowanie obrazów dla wszystkich przepisów
- **UI w zakładce Settings**: Konfiguracja providerów, wybór modeli, auto-save ustawień

### 📊 Zarządzanie przepisami
- **Przeglądarka bazy danych**: Podgląd, edycja i usuwanie przepisów
- **Baza danych SQLite**: Przechowywanie wszystkich przepisów z pełnymi informacjami makroskładnikowymi
- **Nutrition variants**: Wyświetlanie wielu wariantów wartości odżywczych w modalu przepisu
- **Servings tracking**: Śledzenie liczby porcji dla każdego przepisu

### 🍽️ Planowanie posiłków
- **Losowanie posiłków**: Generowanie losowych planów posiłków na dzień (śniadanie, obiad, kolacja, deser)
- **Planer tygodniowy**: Tworzenie jadłospisu na cały tydzień
- **Lista zakupów**: Automatyczne generowanie i agregacja składników z automatycznym skalowaniem
- **Integracja z Todoist**: Eksport listy zakupów bezpośrednio do Todoist
- **Wydruk jadłospisu**: Możliwość wydruku tygodniowego planu posiłków

## Wymagania

- .NET 9.0 SDK
- **Klucz API AI Provider** (co najmniej jeden):
  - OpenAI (zalecany model: gpt-4o-mini lub gpt-5-mini-2025-08-07)
  - Google Gemini (zalecany model: gemini-2.5-flash)
- (Opcjonalnie) Klucz API OpenAI lub Google dla generowania obrazów
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

3. Skonfiguruj klucze API:

Skopiuj plik `appsettings.example.json` do `appsettings.json` i uzupełnij:

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-5-mini-2025-08-07"
  },
  "Settings": {
    "PdfSourceDirectory": "path/to/pdfs",
    "DatabasePath": "recipes.db"
  }
}
```

**Uwaga**: Klucze API mogą być również konfigurowane przez interfejs WWW w zakładce ⚙️ Ustawienia. Ustawienia są przechowywane w bazie danych SQLite.

## Użycie

### Tryb webowy (domyślny - ZALECANY)

Uruchom aplikację w trybie webowym:
```bash
dotnet run
```

Aplikacja uruchomi się na:
- **http://localhost:5000**
- https://localhost:5001

Otwórz przeglądarkę i przejdź do `http://localhost:5000` aby używać pełnego interfejsu webowego.

### Tryb konsolowy (opcjonalny)

Jeśli chcesz używać trybu konsolowego:
```bash
dotnet run --console
```

Menu aplikacji:
1. **Process PDFs and extract recipes** - Przetwarzaj pliki PDF z katalogu i wyciągaj przepisy
2. **Get random meal suggestions** - Otrzymaj losowe sugestie posiłków na dziś
3. **View all recipes** - Wyświetl wszystkie przepisy w bazie danych
4. **Exit** - Wyjście z aplikacji

### Interfejs WWW (http://localhost:5000)

Aplikacja oferuje pełny interfejs webowy z następującymi zakładkami:

1. **📋 Przetwarzanie PDF**:
   - Wybór plików z folderu lub upload własnych
   - Przetwarzanie z real-time progress bar
   - Automatyczne wykrywanie duplikatów

2. **📚 Baza przepisów**:
   - Wyszukiwanie i filtrowanie przepisów
   - Edycja wartości odżywczych i nutrition variants
   - Usuwanie niepotrzebnych przepisów
   - Generowanie obrazów dla przepisów

3. **🍽️ Planer posiłków**:
   - Generowanie dziennego planu (śniadanie, obiad, kolacja, deser)
   - Tworzenie tygodniowego jadłospisu
   - Automatyczna lista zakupów z agregacją składników
   - Eksport do Todoist
   - Drukowanie planu

4. **⚙️ Ustawienia**:
   - **AI Providers**: Zarządzanie providerami (OpenAI/Gemini), priorytety, modele
   - **Image Generation**: Konfiguracja DALL-E/Imagen, auto-save modeli, test generation
   - Wszystkie klucze API przechowywane bezpiecznie w bazie danych

## Struktura projektu

```
RecipesAIHelper/
├── Controllers/                     # ASP.NET Core Web API
│   ├── AIProvidersController.cs    # Zarządzanie providerami AI
│   ├── ProcessingController.cs     # Przetwarzanie PDF
│   ├── RecipesController.cs        # CRUD przepisów
│   ├── ImagesController.cs         # Generowanie obrazów
│   ├── ImageSettingsController.cs  # Konfiguracja image generation
│   ├── MealPlansController.cs      # Planowanie posiłków
│   ├── FileUploadController.cs     # Upload plików
│   └── PrintController.cs          # Drukowanie planów
├── Models/
│   ├── Recipe.cs                   # Model przepisu z nutrition variants
│   ├── RecipeExtractionResult.cs  # Wyniki ekstrakcji AI
│   ├── AIProvider.cs               # Model providera AI
│   ├── MealPlan.cs                 # Model planu posiłków
│   └── StreamingProgress.cs        # Progress tracking
├── Services/
│   ├── IAIService.cs               # Interface dla AI services
│   ├── OpenAIService.cs            # OpenAI GPT integration
│   ├── GeminiService.cs            # Google Gemini integration
│   ├── AIServiceFactory.cs         # Factory pattern dla providerów
│   ├── IImageGenerationService.cs  # Interface dla image generation
│   ├── OpenAIImageGenerationService.cs  # DALL-E integration
│   ├── GeminiImageGenerationService.cs  # Imagen integration
│   ├── ImageGenerationServiceFactory.cs # Factory dla obrazów
│   ├── PdfProcessorService.cs      # Chunking i overlap
│   ├── PdfImageService.cs          # PDF → Images (1200 DPI)
│   ├── PdfDirectService.cs         # Direct PDF → Base64
│   ├── PromptBuilder.cs            # Wspólne prompty
│   └── ShoppingListService.cs      # Agregacja listy zakupów
├── Data/
│   └── RecipeDbContext.cs          # SQLite z migracjami
├── wwwroot/
│   ├── index.html                  # SPA (Alpine.js + Tailwind)
│   ├── app.js                      # Frontend logic
│   └── images/                     # Wygenerowane obrazy (gitignored)
├── Program.cs                       # ASP.NET Core setup
└── appsettings.json                # Konfiguracja (nie w repo)
```

## Architektura AI

### Multi-Provider Support

Aplikacja obsługuje wiele providerów AI z automatycznym wyborem na podstawie priorytetów:

| Provider | Model | Context Window | Typ przetwarzania | Zalecany do |
|----------|-------|---------------|-------------------|-------------|
| **Google Gemini** | gemini-2.5-flash | ~1M tokens | Direct PDF | Duże pliki, niskie koszty |
| **OpenAI** | gpt-4o-mini | 128K tokens | Vision API (images) | Wysoka jakość OCR |
| **OpenAI** | gpt-5-mini-2025-08-07 | 400K tokens | Vision API (images) | Bardzo duże konteksty |

### Konfiguracja providerów

**W interfejsie WWW** (⚙️ Ustawienia → AI Providers):
- Dodawaj/edytuj klucze API
- Ustaw priorytety (wyższy = preferowany)
- Aktywuj/deaktywuj providerów
- Wybieraj modele z dropdown

**W bazie danych** (`AIProviders` table):
- Wszystkie ustawienia przechowywane w SQLite
- Runtime switching między providerami
- Wspólne prompty przez `PromptBuilder.cs`

### Strategia przetwarzania PDF

| Rozmiar PDF | Chunking | Overlap | Provider | Metoda |
|-------------|----------|---------|----------|--------|
| < 20 stron | Bez | - | Gemini | Direct PDF |
| 20-100 stron | 3 strony | 1 strona | Gemini | Direct PDF |
| 100+ stron | 3 strony | 1 strona | Gemini/OpenAI | Direct/Images |

**Overlapping chunks**: Zapobiega utracie przepisów na granicach stron

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
- Real-time progress bar w interfejsie WWW
- Podział na chunki z procentowym postępem
- Podsumowanie dla każdego pliku
- Finalne statystyki przetwarzania

## Generowanie obrazów

### Dostępne providery

| Provider | Modele | Format | Rozdzielczość |
|----------|--------|--------|---------------|
| **OpenAI** | DALL-E 2, DALL-E 3, GPT Image 1/Mini | PNG | 1024x1024 |
| **Google** | Imagen 4.0 Ultra | JPEG | 1024x1024 |

### Funkcjonalności
- Auto-save modeli przy zmianie w UI
- Maskowanie kluczy API (backend not updates if `***`)
- Test generation przed pełnym procesem
- Batch generation dla wszystkich przepisów bez obrazów
- Automatyczna kompatybilność parametrów (quality tylko dla DALL-E 3+)

## Kategorie posiłków

Aplikacja obsługuje następujące typy posiłków:
- **Sniadanie** - Śniadania
- **Obiad** - Obiady
- **Kolacja** - Kolacje
- **Deser** - Desery
- **Napoj** - Napoje

## Wartości odżywcze

### Multi-Variant Nutrition Data
Każdy przepis może mieć **wiele wariantów** wartości odżywczych:
- **Przykład**: "całość" (1200 kcal), "porcja" (300 kcal), "1/2 porcji" (150 kcal)
- **Ekstrakcja**: AI automatycznie wyciąga wszystkie rzędy z tabel wartości odżywczych
- **Storage**: Przechowywane jako JSON array w `NutritionVariantsJson`
- **Display**: UI pokazuje główne wartości + expandable variants section

### Makroskładniki (dla każdego wariantu)
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
