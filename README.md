<img width="1487" height="1047" alt="image" src="https://github.com/user-attachments/assets/5646047c-d55d-4b38-9215-8fb612fb9c6c" /># Recipe AI Helper

Kompleksowa aplikacja .NET 9 do automatycznego zarządzania przepisami kulinarnymi z wykorzystaniem AI (OpenAI GPT i Google Gemini). System oferuje pełną automatyzację od ekstrakcji przepisów z PDF, przez tworzenie jadłospisów, aż po generowanie list zakupowych i inteligentne skalowanie porcji.

## 🎯 Główne funkcjonalności

### 🤖 Multi-Provider AI System
- **OpenAI GPT**: Wsparcie dla GPT-4o, GPT-5 Mini z Vision API
- **Google Gemini**: Gemini 2.5-flash z bezpośrednim przetwarzaniem PDF i Imagen 4.0 dla obrazów
- **Dynamiczne przełączanie**: Wybór providera przez priorytet w bazie danych
- **Wspólne prompty**: Jednolity system promptów dla wszystkich providerów (PromptBuilder)
- **Rate limiting**: Konfigurowalne opóźnienia zapobiegające blokadom API
- **Retry mechanizm**: Automatyczne ponowne próby z eksponencjalnym backoffem (Polly)

### 📄 Ekstrakcja przepisów z PDF i obrazów
<img width="1407" height="1031" alt="image" src="https://github.com/user-attachments/assets/25d3a839-2adb-48fa-9fa1-44493a3ebf78" />

#### Dwie ścieżki przetwarzania:
1. **Direct PDF upload** (Gemini) - niższe koszty, szybsze przetwarzanie
   - Bezpośrednie wysyłanie PDF jako base64 do Gemini API
   - Idealne dla plików do 100 stron

2. **PDF → Images → Vision API** (OpenAI & Gemini)
   - Rendering PDF przy 1200 DPI dla wysokiej jakości OCR
   - Skalowanie do 2560px wysokości
   - Doskonałe dla skomplikowanych layoutów i małych czcionek

#### Inteligentne chunking:
- **Chunking z overlapem**: Dzielenie dużych PDF (120+ stron) na fragmenty po 3 strony z 1-stronicowym overlapem
- **Ochrona przed utratą danych**: Overlap zapewnia, że przepisy rozłożone na 2 strony nie zostaną pominięte
- **Markery stron**: `=== STRONA {N} ===`, `--- KONIEC STRONY ---`, `=== STRONA Z POPRZEDNIEGO CHUNKA ===`
- **Konfigurowalne parametry**: Liczba stron na chunk (domyślnie 3) i overlap (domyślnie 1)

#### Mechanizmy ochrony jakości:

**1. Sprawdzanie duplikatów:**
<img width="604" height="168" alt="image" src="https://github.com/user-attachments/assets/de3f9405-f73e-4ae4-aaf2-632cd63a567f" />
<img width="429" height="267" alt="image" src="https://github.com/user-attachments/assets/5da37a16-2b97-4f1b-a8d4-4c3b594f71cc" />
- **Exact match**: Case-insensitive porównanie nazw w bazie
- **Fuzzy matching**: Levenshtein distance similarity >80%
- **AI context**: Ostatnie 10 przepisów przekazywane do AI jako kontekst
- **Per-file tracking**: Lista przetworzonych nazw zapobiega duplikatom w chunkach

**2. Multi-variant nutrition data:**
- **Automatyczna ekstrakcja**: AI wyciąga WSZYSTKIE rzędy z tabel wartości odżywczych
- **Przykład wariantów**: "całość" (1200 kcal), "porcja" (300 kcal), "1/2 porcji" (150 kcal)
- **Storage**: Przechowywane jako JSON array w kolumnie `NutritionVariantsJson`
- **Display**: UI pokazuje główne wartości + expandable variants section
- **Servings tracking**: Opcjonalne pole "Liczba porcji: X"

**3. Upload przez UI:**
- **Folder source**: Wybór plików z lokalnego folderu (PDF)
- **Manual upload**: Drag & drop lub wybór plików z dysku (PDF, JPG, PNG)
- **Status tracking**: Informacja o już przetworzonych plikach
- **Real-time progress**: Progress bar z procentowym postępem i szczegółami

**4. Walidacja i error handling:**
- Sprawdzanie kompletności danych (nazwa, składniki, instrukcje)
- Weryfikacja wartości odżywczych
- Szczegółowe logowanie każdego kroku
- Raportowanie błędów bez przerywania procesu
- Kontynuacja przetwarzania pomimo błędów pojedynczych plików

### 🖼️ Generowanie obrazów AI

#### Wsparcie dla wielu providerów:
- **OpenAI**: DALL-E 2, DALL-E 3, GPT Image 1, GPT Image 1 Mini
  - Format: PNG, rozdzielczość: 1024x1024
  - Parametr `quality` tylko dla DALL-E 3+ (automatyczna kompatybilność)
- **Google Gemini**: Imagen 4.0 Ultra
  - Format: JPEG, rozdzielczość: 1024x1024

#### Zaawansowane funkcje:
- **Batch generation**: Automatyczne generowanie dla wszystkich przepisów bez obrazów
- **Single generation**: Generowanie dla pojedynczego przepisu
- **Auto-save modeli**: Automatyczne zapisywanie wyboru modelu bez kliknięcia "Zapisz"
- **Masked API keys**: Backend nie aktualizuje kluczy jeśli wartość to `"***"`
- **Test generation**: Testowanie konfiguracji przed pełnym procesem
- **Polish prompts**: Dedykowane prompty w języku polskim dla przepisów kulinarnych
- **Konfiguracja przez UI**: Pełne zarządzanie w zakładce ⚙️ Ustawienia

### 📊 Baza przepisów

#### Funkcje przeglądania:
- **Wyszukiwanie tekstowe**: Szybkie wyszukiwanie po nazwie, opisie, typie posiłku
- **Zaawansowane filtrowanie**:
  - Typ posiłku: Śniadanie, Deser, Obiad, Kolacja, Napój
  - Zakresy wartości odżywczych:
    - Kalorie: 0-3000 kcal
    - Białko: 0-200g
    - Węglowodany: 0-300g
    - Tłuszcze: 0-150g
- **Sortowanie przepisów**:
  - Po nazwie (A-Z / Z-A)
  - Po wartościach odżywczych (kalorie, białko, węglowodany, tłuszcze)
  - Przełącznik kierunku: ⬆ Rosnąco / ⬇ Malejąco
- **Aktywne filtry**: Podsumowanie zastosowanych filtrów w UI
- **Quick reset**: Przycisk "Wyczyść wszystkie" do resetowania filtrów i sortowania

#### Zarządzanie przepisami:
- **Edycja przepisów**: Modyfikacja wartości odżywczych, składników, instrukcji
- **Nutrition variants**: Wyświetlanie i edycja wielu wariantów wartości odżywczych
- **Usuwanie przepisów**: Kasowanie niepotrzebnych pozycji
- **Dodawanie ręczne**: Tworzenie przepisów bez PDF (formularz)
- **Generowanie obrazów**: Dodawanie AI-wygenerowanych zdjęć do przepisów
- **Statystyki**: Liczba przepisów, przepisy bez zdjęć, itp.

#### SQLite Database:
- **Hybrid JSON storage**: Kolumny JSON z computed properties
- **Migration pattern**: Sprawdzanie istnienia kolumn przed ALTER TABLE
- **Recipe model**: Pełne dane przepisu z makroskładnikami
- **Nutrition variants**: JSON array z wieloma wariantami wartości odżywczych

### 🍽️ Planowanie posiłków (Meal Planner)

#### Tworzenie jadłospisów:
- **Planer tygodniowy**: Tworzenie planów na dowolną liczbę dni (1-31)
- **Data range**: Określenie daty rozpoczęcia i zakończenia planu
- **Zarządzanie planami**: Lista wszystkich planów z możliwością edycji i usuwania
- **Status aktywności**: Oznaczanie aktywnych/nieaktywnych planów

#### Auto-generowanie przepisów:

**Tryb standardowy**:
- **Losowanie posiłków**: Automatyczny wybór przepisów z bazy
- **Kategorie**: Śniadanie, Obiad, Kolacja, Deser, Napój
- **Konfiguracja**: Liczba przepisów per dzień per kategoria
- **Sprawdzanie duplikatów**: Unikanie powtórzeń w planie
- **Missing recipes handling**: Raportowanie brakujących kategorii

**Tryb optymalizacji kalorycznej**:
- **Cel kaloryczny**: Określenie docelowej kaloryczności na dzień (np. 2000 kcal)
- **Margines tolerancji**: ±X kcal (domyślnie ±200 kcal)
- **Inteligentny dobór**: AI wybiera przepisy najbliższe celowi dla każdej kategorii
- **Tracking unikalności**: System śledzi użyte przepisy across all days
- **Top 3 randomization**: Wybór z 3 najbliższych matchów dla variety
- **Fallback mechanism**: Jeśli brak w zakresie, wybór losowy z kategorii

#### Zarządzanie wieloma osobami w planie:

**Podstawowe funkcje**:
- **Dodawanie osób**: Maksymalnie 5 osób per plan
- **Unikalne nazwy**: Sprawdzanie duplikatów nazw w planie
- **Cele kaloryczne**: Indywidualne cele dla każdej osoby (1000-5000 kcal/dzień)
- **Edycja i usuwanie**: Pełne CRUD operations dla osób
- **Sort order**: Automatyczne porządkowanie listy osób

**Inteligentne skalowanie porcji**:

**Per-Day Intelligent Scaling**:
- **Cel**: Każda osoba dostaje dokładnie swoje docelowe kalorie dziennie
- **Tolerancja ±50 kcal**: Jeśli suma dnia jest w tym zakresie, bez skalowania
- **Per-person day factors**: Indywidualny współczynnik skalowania dla każdej osoby per dzień
- **Formula**: `dayScalingFactor = targetCalories / dailyCaloriesSum`
- **Przykład**:
  - Dzień ma 2000 kcal (suma bazowa)
  - Osoba A: 1800 kcal → współczynnik 0.9 (wszystko -10%)
  - Osoba B: 2200 kcal → współczynnik 1.1 (wszystko +10%)

**AI-powered ingredient scaling**:
- **RecipeScalingService**: Używa Gemini AI do inteligentnego skalowania składników
- **Kontekst posiłku**: Różne strategie dla śniadań, obiadów, kolacji
- **Intelligent rounding**: Zaokrąglanie do praktycznych jednostek (np. 1/2 jajka → 1 jajko)
- **Fallback mechanism**: Jeśli AI zawiedzie, użycie bazowych składników
- **2-second rate limit**: Opóźnienie między wywołaniami AI

**Obsługa deserów**:
- **DessertPlanningService**: Dedykowany serwis dla deserów
- **Równe porcje**: Każda osoba dostaje tę samą porcję deseru
- **Multi-day spreading**: Rozłożenie deseru na kilka dni (np. ciasto na 3 dni)
- **Portion calculation**: Obliczanie kalorii per porcja
- **Explanation**: AI generuje wyjaśnienie planu deseru

**Automatic scaling triggers**:
1. **Auto-scale podczas auto-generate**: Jeśli plan ma osoby, automatyczne skalowanie po wygenerowaniu przepisów
2. **Manual scaling**: Przycisk "Skaluj przepisy" dla ręcznego przeskalowania całego planu
3. **Skip scaling option**: Parametr `skipScaling=true` pomija auto-skalowanie
4. **Per-entry scaling**: Skalowanie pojedynczego przepisu dla wszystkich osób

**Wyświetlanie przeskalowanych przepisów**:
- **Per-person view**: UI pokazuje składniki dla każdej osoby
- **Scaling factors**: Współczynniki skalowania (np. 0.85x, 1.0x, 1.15x)
- **Nutrition info**: Przeliczone makroskładniki per osoba
- **Daily sums**: Suma kalorii per osoba per dzień
- **Color coding**: Wizualne oznaczenie różnych porcji

#### Ręczne zarządzanie:
- **Drag & drop**: Przeciąganie przepisów między dniami i kategoriami
- **Dodawanie ręczne**: Wybór przepisów z bazy i przypisanie do dnia
- **Usuwanie przepisów**: Kasowanie pojedynczych entry z planu
- **Edycja kolejności**: Zmiana order przepisów w dniu

### 🛒 Listy zakupów

#### Generowanie list:
- **Automatyczna agregacja**: Zbieranie składników ze wszystkich przepisów w planie
- **Kategoryzacja**: Automatyczne grupowanie po kategoriach:
  - 🥬 Warzywa
  - 🍎 Owoce
  - 🍖 Mięso
  - 🥛 Nabiał
  - 🍞 Pieczywo
  - 🧂 Przyprawy
  - 🍫 Słodycze
  - 🥤 Napoje
  - 📦 Inne
- **AI-powered aggregation**: ShoppingListService używa AI do inteligentnego sumowania
- **Unit normalization**: Konwersja jednostek (np. 500ml + 0.5l = 1l)
- **Smart deduplication**: Łączenie podobnych składników

#### Obsługa przeskalowanych składników:
- **Multi-person support**: Jeśli plan ma osoby, używa przeskalowanych składników
- **Person headers**: `=== [Imię] (porcja przeskalowana) ===` dla kontekstu AI
- **Aggregation per entry**: Suma składników dla wszystkich osób per przepis
- **Fallback**: Użycie bazowych składników jeśli brak przeskalowanych
- **Status indicator**: UI pokazuje czy lista używa przeskalowanych składników

#### Wyświetlanie i eksport:
- **Modal view**: Wyświetlanie listy w modalnym oknie
- **Print-friendly**: Style CSS optymalizowane do druku
- **Export do Todoist**: Automatyczne tworzenie projektu z sekcjami i zadaniami
- **Recipe count**: Liczba przepisów w planie
- **Item count**: Liczba pozycji na liście
- **Persons info**: Lista osób i ich cele kaloryczne
- **Generation timestamp**: Data i czas wygenerowania

#### Zapisywanie w bazie:
- **Database persistence**: Lista zapisywana w tabeli ShoppingLists
- **JSON storage**: Składniki jako JSON array
- **One list per plan**: Każdy plan ma max 1 aktywną listę (overwrite)
- **Regeneration**: Możliwość ponownego wygenerowania listy

### 🔗 Integracja z Todoist

#### Konfiguracja:
- **API key management**: Bezpieczne przechowywanie klucza w bazie Settings
- **Test connection**: Sprawdzanie poprawności klucza przed eksportem
- **UI w zakładce Settings**: Sekcja "📋 Integracja z Todoist"

#### Eksport listy zakupów:
- **Automatyczne tworzenie projektu**: `🛒 [Nazwa planu] (DD.MM - DD.MM)`
- **Sekcje z emoji**: Kategorie jako sekcje w Todoist
- **Zadania per składnik**: `[Nazwa] - [Ilość]` jako osobne zadania
- **Organizational structure**: Zadania przypisane do odpowiednich sekcji
- **Date range**: Automatyczne obliczanie zakresu dat z planu

#### Przykładowa struktura:
```
🛒 Plan na styczeń (01.01 - 07.01)
  🥬 Warzywa
    ☐ Pomidor - 500g
    ☐ Cebula - 3 szt
  🍖 Mięso
    ☐ Pierś z kurczaka - 1kg
  🥛 Nabiał
    ☐ Mleko - 1l
```

### ⚙️ Ustawienia i zarządzanie

#### Zarządzanie kluczami API:
- **OpenAI API Key**: Klucz dla GPT i DALL-E
- **Google Gemini API Key**: Klucz dla Gemini i Imagen
- **Todoist API Key**: Klucz dla eksportu list zakupów
- **Database storage**: Wszystkie klucze przechowywane w SQLite (Settings table)
- **Masked display**: Klucze wyświetlane jako `***` w UI
- **Separate save buttons**: Osobne przyciski dla kluczy i modeli

#### AI Providers Management:
- **Lista providerów**: OpenAI, Google Gemini
- **Priority system**: Wyższy priorytet = preferowany provider
- **Active/Inactive toggle**: Włączanie/wyłączanie providerów
- **Model selection**: Dropdown z dostępnymi modelami per provider
- **Real-time switching**: Zmiana providera bez restartu aplikacji
- **Header display**: Aktywny provider i model widoczny w headerze

#### Image Generation Settings:
- **Provider cards**: OpenAI i Google z osobnymi kartami
- **Model dropdowns**: Wybór modelu per provider
- **Auto-save models**: Automatyczne zapisywanie przy zmianie modelu
- **API key inputs**: Osobne pola dla kluczy każdego providera
- **Test generation**: Przycisk testowy przed pełnym procesem
- **Status indicators**: ✓ Zapisano automatycznie przez 2 sekundy

#### Todoist Integration:
- **API key input**: Pole do wklejenia klucza
- **Save button**: Zapisywanie klucza w bazie
- **Test connection**: Weryfikacja klucza przed eksportem
- **Status feedback**: Informacja o sukcesie/błędzie

#### Folder Management:
- **PDF Source Directory**: Konfiguracja folderu z plikami PDF
- **Change folder**: Dynamiczna zmiana folderu bez restartu
- **Path validation**: Sprawdzanie poprawności ścieżki
- **Status message**: Feedback o sukcesie/błędzie zmiany

#### ⚙️ Zaawansowane ustawienia techniczne (Settings):

**Processing Configuration**:
- **PagesPerChunk**: Liczba stron PDF per chunk (domyślnie: 3)
  - Mniejsza wartość = więcej chunków, mniejsze konteksty
  - Większa wartość = mniej chunków, możliwe timeouty
  - Zalecane: 3-5 stron dla optymalnego balansu

- **OverlapPages**: Liczba stron overlapa między chunkami (domyślnie: 1)
  - Zapobiega utracie przepisów na granicach stron
  - Overlap = 0: brak overlapa (ryzyko utraty danych)
  - Overlap = 1: jedna strona kontekstu (zalecane)
  - Overlap = 2: dwie strony kontekstu (dla bardzo rozłożonych przepisów)

**Rate Limiting & Performance**:
- **DelayBetweenChunksMs**: Opóźnienie między chunkami w milisekundach (domyślnie: 3000ms)
  - Zapobiega blokadom API przez zbyt częste requesty
  - 3000ms (3s) = zalecane minimum
  - 5000ms (5s) = bezpieczne dla dużych plików
  - 1000ms (1s) = ryzykowne, możliwe rate limits

**Duplicate Detection**:
- **CheckDuplicates**: Sprawdzanie duplikatów przed zapisem (domyślnie: true)
  - true = wykrywanie i pomijanie duplikatów (zalecane)
  - false = zapisywanie wszystkich przepisów (może tworzyć duplikaty)

- **RecentRecipesContext**: Liczba ostatnich przepisów wysyłanych do AI jako kontekst (domyślnie: 10)
  - AI otrzymuje listę ostatnich N przepisów aby uniknąć duplikatów
  - 10 = dobry balans między kontekstem a rozmiarem promptu
  - 20 = więcej kontekstu, większe prompty
  - 0 = brak kontekstu, możliwe więcej duplikatów

**Database Configuration**:
- **DatabasePath**: Ścieżka do pliku SQLite (domyślnie: "recipes.db")
  - Relatywna lub absolutna ścieżka
  - Automatyczne tworzenie przy pierwszym uruchomieniu
  - Migracje automatyczne przy zmianach schematu

**Konfiguracja przez appsettings.json**:
```json
{
  "Settings": {
    "PdfSourceDirectory": "C:\\Users\\YourName\\Downloads\\Recipes",
    "DatabasePath": "recipes.db",
    "PagesPerChunk": 3,
    "OverlapPages": 1,
    "DelayBetweenChunksMs": 3000,
    "CheckDuplicates": true,
    "RecentRecipesContext": 10
  }
}
```

**Konfiguracja przez API** (SettingsController):
```http
PUT /api/settings
Content-Type: application/json

{
  "settings": {
    "pagesPerChunk": 5,
    "overlapPages": 2,
    "delayBetweenChunksMs": 5000,
    "checkDuplicates": true,
    "recentRecipesContext": 20
  }
}
```

**Best practices dla różnych scenariuszy**:

| Scenariusz | PagesPerChunk | Overlap | Delay | CheckDuplicates |
|------------|---------------|---------|-------|-----------------|
| **Małe PDF (<20 stron)** | 10 | 0 | 2000ms | true |
| **Średnie PDF (20-100 stron)** | 3 | 1 | 3000ms | true |
| **Duże PDF (100+ stron)** | 3 | 1 | 5000ms | true |
| **Bardzo duże PDF (200+ stron)** | 2 | 1 | 5000ms | true |
| **Testy/Debug** | 1 | 0 | 1000ms | false |
| **Produkcja** | 3 | 1 | 3000ms | true |

### 📂 Zarządzanie plikami źródłowymi

**Lista plików z przepisami**:
- **Grupowanie per plik**: Każdy plik PDF pokazany z liczbą wydobytych przepisów
- **Rozwijana lista przepisów**: Kliknij na plik aby zobaczyć wszystkie przepisy z niego
- **Statystyki per plik**: Nazwa pliku + liczba przepisów (np. "Ksiazka.pdf - 45 przepisów")

**Akcje per plik**:
1. **Wyświetl przepisy**: Rozwiń/zwiń listę przepisów z tego pliku
2. **Usuń przepisy**: Skasuj wszystkie przepisy z wybranego pliku (z potwierdzeniem)
3. **Regeneruj**: Ponowne przetworzenie pliku z aktualnym AI:
   - **Use case**: Gdy AI źle wyciągnął przepisy lub pominął część
   - **Proces**:
     1. Usuwa wszystkie stare przepisy z tego pliku
     2. Ponownie przetwarza oryginalny plik PDF
     3. Wyciąga przepisy z aktualnym AI providerem i modelem
     4. Zapisuje nowe przepisy do bazy
   - **Raportowanie**: Pokazuje ile usunięto, ile zapisano, ile pominięto (duplikaty)
   - **Inteligentne chunking**: Używa aktualnych ustawień (pages per chunk, overlap)
   - **Progress tracking**: Real-time feedback podczas regeneracji

**Zastosowania regeneracji**:
- Testowanie nowych modeli AI na znanych plikach
- Poprawa jakości ekstrakcji po zmianie promptów
- Naprawienie błędnie wydobytych przepisów
- Porównanie wyników między różnymi providerami
- Dostrajanie parametrów przetwarzania

**Proces regeneracji**:
1. Wybór pliku z listy
2. Usunięcie starych przepisów z tego pliku
3. Ponowne przetworzenie PDF z aktualnym AI providerem
4. Zapis nowych przepisów do bazy
5. Raport z wynikami (liczba usuniętych/zapisanych/pominiętych)

**Szczegóły wyświetlania przepisów per plik**:
- **Nazwa przepisu**: Pełna nazwa każdego przepisu
- **Typ posiłku**: Kategoria (Śniadanie, Obiad, etc.)
- **Wartości odżywcze**: Kalorie, P/C/F per przepis
- **Link do przepisu**: Możliwość otwarcia pełnego przepisu w modal
- **Usuwanie pojedyncze**: Usunięcie konkretnego przepisu (pozostałe zostają)

## 📋 Wymagania

- **.NET 9.0 SDK**: https://dotnet.microsoft.com/download
- **Klucz API AI Provider** (co najmniej jeden):
  - OpenAI: https://platform.openai.com/api-keys (zalecany: gpt-4o-mini, gpt-5-mini-2025-08-07)
  - Google Gemini: https://aistudio.google.com/app/apikey (zalecany: gemini-2.5-flash)
- **(Opcjonalnie) Klucz API Image Generation**:
  - OpenAI dla DALL-E/GPT Image
  - Google dla Imagen 4.0 Ultra
- **(Opcjonalnie) Klucz API Todoist**: https://app.todoist.com/app/settings/integrations/developer

## 🚀 Instalacja

### 1. Klonowanie repozytorium
```bash
git clone https://github.com/Vesperino/RecipesAIHelper.git
cd RecipesAIHelper
```

### 2. Przywrócenie pakietów NuGet
```bash
dotnet restore
```

### 3. Konfiguracja kluczy API

**Opcja A: Przez plik konfiguracyjny** (opcjonalnie)

Skopiuj plik `appsettings.example.json` do `appsettings.json` i uzupełnij:

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-5-mini-2025-08-07"
  },
  "Settings": {
    "PdfSourceDirectory": "C:\\Users\\YourName\\Downloads\\Recipes",
    "DatabasePath": "recipes.db"
  }
}
```

**Opcja B: Przez interfejs WWW** (zalecane)

Klucze API mogą być konfigurowane przez interfejs WWW w zakładce **⚙️ Ustawienia**. Wszystkie ustawienia są przechowywane w bazie danych SQLite.

### 4. Uruchomienie aplikacji

```bash
dotnet run
```

Aplikacja uruchomi się na:
- **http://localhost:5000** (HTTP) - otwórz w przeglądarce
- https://localhost:5001 (HTTPS)

## 💻 Użycie

### Tryb webowy (domyślny - ZALECANY)

Uruchom aplikację:
```bash
dotnet run
```

Otwórz przeglądarkę i przejdź do **http://localhost:5000**

### Tryb konsolowy (opcjonalny)

```bash
dotnet run --console
```

Menu aplikacji konsolowej:
1. **Process PDFs and extract recipes** - Przetwarzaj pliki PDF z katalogu i wyciągaj przepisy
2. **Get random meal suggestions** - Otrzymaj losowe sugestie posiłków na dziś
3. **View all recipes** - Wyświetl wszystkie przepisy w bazie danych
4. **Exit** - Wyjście z aplikacji

## 📑 Interfejs WWW

### Zakładki:

#### 1. 📄 Przetwarzanie
- **📁 Wybór Folderu z PDFami**: Konfiguracja źródłowego folderu
- **📤 Upload Ręczny**: Przeciągnij lub wybierz pliki (PDF, JPG, PNG)
- **📄 Zarządzanie Plikami**: Lista plików z folderu z checkboxami
- **Funkcje**:
  - Załaduj Pliki
  - Zaznacz Wszystkie / Odznacz Wszystkie
  - Przetwórz Zaznaczone
- **Progress tracking**: Real-time progress bar z procentami
- **Status przetworzonych**: Informacja o już przetworzonych plikach

#### 2. 🗃️ Baza Przepisów
- **Wyszukiwanie**: Szybkie wyszukiwanie po tekście
- **Zaawansowane filtry**:
  - Typ posiłku (multi-select buttons)
  - Zakresy wartości odżywczych (slidery)
- **Sortowanie**: Po nazwie lub wartościach odżywczych z przełącznikiem kierunku
- **Akcje**:
  - Dodaj przepis (ręczny formularz)
  - Edytuj przepis (modal z pełnym formularzem)
  - Usuń przepis
  - Generuj obraz AI (per przepis)
  - Generuj wszystkie brakujące obrazy (batch)
- **Statystyki**: Liczba przepisów, przepisy bez zdjęć
- **Legenda makroskładników**: P = Białko, C = Węglowodany, F = Tłuszcze

#### 3. 📅 Jadłospis (Meal Planner)
- **Lista planów**: Wszystkie jadłospisy z datami i statusem
- **Tworzenie planu**:
  - Nazwa planu
  - Data rozpoczęcia i zakończenia
  - Liczba dni (1-31)
- **Zarządzanie osobami**:
  - Dodaj osobę (nazwa, cel kaloryczny)
  - Edytuj cele kaloryczne
  - Usuń osobę
  - Max 5 osób per plan
- **Auto-generowanie**:
  - Wybór kategorii (Śniadanie, Obiad, Kolacja, Deser, Napój)
  - Liczba przepisów per dzień
  - Tryb optymalizacji kalorycznej (cel ± margines)
  - Skip scaling option
- **Akcje per plan**:
  - Edytuj plan
  - Auto-generuj przepisy
  - Skaluj przepisy (manual trigger)
  - Generuj listę zakupów
  - Eksportuj do Todoist
  - Drukuj jadłospis
  - Usuń plan
- **Widok kalendarza**: Dni tygodnia z przepisami per kategoria
- **Drag & drop**: Przeciąganie przepisów między dniami
- **Dodawanie ręczne**: Wybór przepisu z bazy i przypisanie
- **Nutrition summary**: Suma kalorii per dzień per osoba

#### 4. 📂 Pliki Źródłowe
- **Lista plików z przepisami**: Wszystkie przetworzone pliki PDF
- **Grupowanie**: Pliki pogrupowane z liczbą przepisów
- **Rozwijana lista**: Kliknij aby zobaczyć przepisy z każdego pliku
- **Szczegóły per plik**:
  - Nazwa pliku PDF
  - Liczba wydobytych przepisów
  - Lista przepisów (nazwa, typ, kalorie, P/C/F)
- **Akcje per plik**:
  - Wyświetl przepisy: Rozwiń/zwiń listę przepisów
  - Usuń przepisy: Usuń wszystkie przepisy z pliku
  - Regeneruj: Ponownie przetworz plik z aktualnym AI
- **Akcje per przepis**:
  - Podgląd: Otwórz pełny przepis w modal
  - Usuń: Usuń pojedynczy przepis
- **Workflow regeneracji**:
  1. Wybierz plik → kliknij "Regeneruj"
  2. Potwierdź regenerację (ostrzeżenie o usunięciu)
  3. System usuwa stare przepisy
  4. Ponownie przetwarza PDF
  5. Zapisuje nowe przepisy
  6. Pokazuje raport (usunięte/zapisane/pominięte)

#### 5. ⚙️ Ustawienia
- **Klucze API**:
  - OpenAI API Key (GPT + DALL-E)
  - Google Gemini API Key (Gemini + Imagen)
  - Todoist API Key
  - Przyciski: Zapisz Klucze API, Testuj Połączenie
- **AI Providers**:
  - Lista providerów z priorytetami
  - Toggle aktywności
  - Wybór modeli (dropdowns)
  - Status w headerze
- **Image Generation**:
  - Karty per provider (OpenAI, Google)
  - Wybór modeli (DALL-E 2/3, GPT Image, Imagen)
  - Auto-save przy zmianie modelu
  - Test generation
- **Todoist Integration**:
  - Pole klucza API
  - Zapisz Klucz API
  - Testuj Połączenie
- **Folder Management**:
  - PDF Source Directory
  - Zmień Folder

## 🏗️ Struktura projektu

```
RecipesAIHelper/
├── Controllers/                          # ASP.NET Core Web API
│   ├── AIProvidersController.cs         # Zarządzanie providerami AI (GET, PUT, toggle)
│   ├── ProcessingController.cs          # Przetwarzanie PDF (process-selected-files, upload)
│   ├── RecipesController.cs             # CRUD przepisów (GET, POST, PUT, DELETE)
│   ├── ImagesController.cs              # Generowanie obrazów (generate/{id}, generate-all-missing)
│   ├── ImageSettingsController.cs       # Konfiguracja image generation (GET, PUT, switch-provider, test)
│   ├── MealPlansController.cs           # Planowanie posiłków (plans, days, entries, persons, auto-generate, scale)
│   ├── TodoistController.cs             # Eksport do Todoist (export-shopping-list, test-connection)
│   ├── FileUploadController.cs          # Upload plików (upload)
│   ├── PrintController.cs               # Drukowanie planów (print-meal-plan)
│   ├── SettingsController.cs            # Ustawienia globalne (GET, PUT) + zaawansowane parametry
│   ├── SourceFilesController.cs         # Zarządzanie plikami źródłowymi (GET, DELETE, regenerate)
│   ├── FilesController.cs               # Listowanie plików z folderu (GET)
│   └── AIModelSettingsController.cs     # Ustawienia modeli AI (GET, PUT)
├── Models/
│   ├── Recipe.cs                        # Model przepisu z nutrition variants (hybrid JSON storage)
│   ├── RecipeExtractionResult.cs       # Wyniki ekstrakcji AI
│   ├── AIProvider.cs                    # Model providera AI (name, model, priority, isActive)
│   ├── MealPlan.cs                      # Model planu posiłków (name, dates, isActive)
│   ├── MealPlanDay.cs                   # Model dnia w planie (dayOfWeek, date)
│   ├── MealPlanEntry.cs                 # Model entry (recipeId, mealType, order)
│   ├── MealPlanPerson.cs                # Model osoby (name, targetCalories, sortOrder)
│   ├── MealPlanRecipe.cs                # Model przeskalowanego przepisu (scalingFactor, scaledIngredients, scaledNutrition)
│   ├── ShoppingList.cs                  # Model listy zakupów (mealPlanId, itemsJson, generatedAt)
│   ├── DessertPlan.cs                   # Model planu deseru (totalPortions, portionCalories, daysToSpread)
│   ├── NutritionVariant.cs              # Model wariantu wartości odżywczych
│   └── StreamingProgress.cs             # Progress tracking dla przetwarzania
├── Services/
│   ├── IAIService.cs                    # Interface dla AI services (ExtractRecipesFromImages, SupportsDirectPDF)
│   ├── OpenAIService.cs                 # OpenAI GPT integration (Vision API)
│   ├── GeminiService.cs                 # Google Gemini integration (Direct PDF + Vision)
│   ├── AIServiceFactory.cs              # Factory pattern dla providerów (GetActiveProvider, CreateService)
│   ├── IImageGenerationService.cs       # Interface dla image generation (GenerateImageAsync)
│   ├── OpenAIImageGenerationService.cs  # DALL-E integration (DALL-E 2/3, GPT Image)
│   ├── GeminiImageGenerationService.cs  # Imagen integration (Imagen 4.0 Ultra)
│   ├── ImageGenerationServiceFactory.cs # Factory dla obrazów (GetActiveService)
│   ├── PdfProcessorService.cs           # Chunking i overlap logic (ProcessPdfAsync)
│   ├── PdfImageService.cs               # PDF → Images (1200 DPI rendering, scaling to 2560px)
│   ├── PdfDirectService.cs              # Direct PDF → Base64 (for Gemini)
│   ├── PromptBuilder.cs                 # Wspólne prompty (BuildRecipeExtractionPrompt, nutrition variants)
│   ├── RecipeScalingService.cs          # AI-powered ingredient scaling (ScaleRecipeIngredientsAsync)
│   ├── DessertPlanningService.cs        # Dessert planning logic (PlanDessertAsync)
│   ├── ShoppingListService.cs           # Agregacja listy zakupów (GenerateShoppingListAsync)
│   └── TodoistService.cs                # Todoist API integration (CreateProjectWithSections)
├── Data/
│   └── RecipeDbContext.cs               # SQLite z migracjami (recipes, plans, persons, shopping lists)
├── wwwroot/
│   ├── index.html                       # SPA (Alpine.js + Tailwind CSS)
│   ├── app.js                           # Frontend logic (Alpine data, methods)
│   └── images/                          # Wygenerowane obrazy (gitignored)
├── Program.cs                            # ASP.NET Core setup (DI, routing, CORS)
├── appsettings.json                     # Konfiguracja (nie w repo)
├── appsettings.example.json             # Przykładowa konfiguracja
├── CLAUDE.md                            # Instrukcje dla Claude Code
└── README.md                            # Ten plik
```

## 🧠 Architektura AI

### Multi-Provider Support

| Provider | Model | Context Window | Typ przetwarzania | Zalecany do | Koszt |
|----------|-------|---------------|-------------------|-------------|-------|
| **Google Gemini** | gemini-2.5-flash | ~1M tokens | Direct PDF | Duże pliki, niskie koszty | Niski |
| **OpenAI** | gpt-4o-mini | 128K tokens | Vision API (images) | Wysoka jakość OCR | Średni |
| **OpenAI** | gpt-5-mini-2025-08-07 | 400K tokens | Vision API (images) | Bardzo duże konteksty | Wyższy |

### Strategia przetwarzania PDF

| Rozmiar PDF | Chunking | Overlap | Provider | Metoda | Czas przetwarzania |
|-------------|----------|---------|----------|--------|-------------------|
| < 20 stron | Bez | - | Gemini | Direct PDF | Szybki (1-2 min) |
| 20-100 stron | 3 strony | 1 strona | Gemini | Direct PDF | Średni (5-10 min) |
| 100+ stron | 3 strony | 1 strona | Gemini/OpenAI | Direct/Images | Długi (20+ min) |

### Rate Limiting

- **Opóźnienie między chunkami**: Domyślnie 3000ms (konfigurowalne)
- **Semaphore locking**: 1 request at a time
- **Exponential backoff**: 2^attempt sekund + jitter (Polly retry policy)
- **Max retries**: 3 próby per chunk
- **Timeout**: 5 minut per request (OpenAI), 2 minuty (Gemini)

### Prompt Engineering

**PromptBuilder.cs** zapewnia:
- **Structured JSON output**: Wszystkie prompty zwracają JSON
- **Multi-variant extraction**: Instrukcje dla wyciągania wszystkich wariantów nutrition
- **Context passing**: Ostatnie 10 przepisów jako kontekst dla unikania duplikatów
- **Polish language**: Wszystkie prompty w języku polskim
- **Example-based learning**: Przykłady w promptach dla lepszej jakości

## 🔧 Konfiguracja providerów

### W interfejsie WWW (⚙️ Ustawienia → AI Providers):
1. **Dodaj klucz API**: Wklej klucz OpenAI lub Gemini
2. **Wybierz model**: Dropdown z dostępnymi modelami
3. **Ustaw priorytet**: Wyższy = preferowany (1-100)
4. **Aktywuj/deaktywuj**: Toggle dla włączenia/wyłączenia
5. **Testuj**: Sprawdź czy provider działa

### W bazie danych (AIProviders table):
```sql
INSERT INTO AIProviders (Name, ApiKey, Model, Priority, IsActive)
VALUES ('Gemini', 'your-key', 'gemini-2.5-flash', 10, 1);

INSERT INTO AIProviders (Name, ApiKey, Model, Priority, IsActive)
VALUES ('OpenAI', 'sk-...', 'gpt-4o-mini', 5, 1);
```

### Runtime switching:
- **AIServiceFactory.GetActiveProvider()**: Automatyczny wybór na podstawie priorytetu
- **Brak restartu**: Zmiana providera bez restartu aplikacji
- **Header display**: Aktywny provider wyświetlany w headerze

## 📊 Kategorie posiłków

Aplikacja obsługuje następujące typy posiłków (MealType enum):

| Enum Value | Nazwa | Opis |
|------------|-------|------|
| **0** | Sniadanie | Śniadania |
| **1** | Deser | Desery (specjalna logika skalowania) |
| **2** | Obiad | Obiady |
| **3** | Kolacja | Kolacje |
| **4** | Napoj | Napoje |

## 🥗 Wartości odżywcze

### Multi-Variant Nutrition Data

Każdy przepis może mieć **wiele wariantów** wartości odżywczych:

#### Przykłady wariantów:
- **"całość"**: 1200 kcal, 60g białka, 150g węglowodanów, 40g tłuszczów
- **"porcja"**: 300 kcal, 15g białka, 37.5g węglowodanów, 10g tłuszczów
- **"1/2 porcji"**: 150 kcal, 7.5g białka, 18.75g węglowodanów, 5g tłuszczów

#### Ekstrakcja:
- **AI extraction**: Prompt instruuje AI aby wyciągnął WSZYSTKIE rzędy z tabeli wartości odżywczych
- **PromptBuilder logic**: Linie ~265-556 zawierają logikę ekstrakcji nutrition variants
- **Automatic parsing**: AI zwraca JSON array z wariantami

#### Storage:
- **Database column**: `NutritionVariantsJson TEXT` (JSON string)
- **Computed property**: `NutritionVariants` getter/setter z auto-serialization
- **[JsonIgnore]**: `NutritionVariantsJson` nie jest serializowany do API responses
- **API response**: `NutritionVariants` automatycznie included

#### Display:
- **Main values**: Pierwszy wariant lub główne wartości wyświetlane na karcie
- **Expandable section**: UI pokazuje wszystkie warianty w modal view
- **Edit support**: Możliwość edycji wariantów w formularzu

### Makroskładniki (dla każdego wariantu)

- **Kalorie**: kcal (0-3000 typowy zakres)
- **Białko**: g (0-200 typowy zakres)
- **Węglowodany**: g (0-300 typowy zakres)
- **Tłuszcze**: g (0-150 typowy zakres)

## 🔗 Integracja z Todoist

### Konfiguracja API key

1. Przejdź do **⚙️ Ustawienia** w interfejsie WWW
2. Znajdź sekcję **"📋 Integracja z Todoist"**
3. Uzyskaj klucz API: [Todoist Developer Settings](https://app.todoist.com/app/settings/integrations/developer)
4. Wklej klucz w pole "Todoist API Key"
5. Kliknij **"Zapisz Klucz API"**
6. (Opcjonalnie) Kliknij **"Testuj Połączenie"**

### Eksport listy zakupów

1. Przejdź do **📅 Jadłospis**
2. Wybierz plan posiłków
3. Kliknij **"Generuj listę zakupów"** (jeśli jeszcze nie została wygenerowana)
4. W oknie z listą kliknij **"Export do Todoist"**
5. Lista zostanie automatycznie utworzona jako nowy projekt w Todoist

### Struktura projektu w Todoist

**Nazwa projektu**: `🛒 [Nazwa planu] (DD.MM - DD.MM)`

**Sekcje z emoji**:
- 🥬 Warzywa
- 🍎 Owoce
- 🍖 Mięso
- 🥛 Nabiał
- 🍞 Pieczywo
- 🧂 Przyprawy
- 🍫 Słodycze
- 🥤 Napoje
- 📦 Inne

**Zadania**: `[Nazwa] - [Ilość]` w odpowiednich sekcjach

**Przykład**:
```
🛒 Plan na styczeń (01.01 - 07.01)
  🥬 Warzywa
    ☐ Pomidor - 500g
    ☐ Cebula - 3 szt
  🍖 Mięso
    ☐ Pierś z kurczaka - 1kg
    ☐ Wołowina - 500g
  🥛 Nabiał
    ☐ Mleko - 1l
    ☐ Ser żółty - 200g
```

## 🔐 Bezpieczeństwo

### API Keys Storage
- **SQLite database**: Wszystkie klucze w tabeli Settings
- **Masked display**: UI pokazuje `***` zamiast rzeczywistych kluczy
- **Backend masking logic**: Klucze nie są aktualizowane jeśli wartość to `"***"`
- **No appsettings.json commit**: Plik konfiguracyjny w .gitignore

### Database
- **Local storage**: SQLite file w katalogu aplikacji
- **No cloud sync**: Wszystkie dane lokalne
- **Migration pattern**: Bezpieczne ALTER TABLE z PRAGMA check

## 📈 Performance

### Optimization techniques:
- **Chunking**: Dzielenie dużych PDF zapobiega timeout
- **Rate limiting**: Semaphore + delays zapobiegają blokadom API
- **Retry policies**: Polly exponential backoff dla reliability
- **Progress tracking**: Real-time feedback dla użytkownika
- **Image caching**: Wygenerowane obrazy cached lokalnie

### Typical processing times:
- **Ekstrakcja 1 przepisu**: ~5-10 sekund
- **PDF 20 stron**: ~2-5 minut
- **PDF 100 stron**: ~15-30 minut (z chunkingiem)
- **Generowanie obrazu**: ~10-20 sekund per przepis
- **Lista zakupów**: ~10-30 sekund (zależnie od liczby składników)
- **Skalowanie przepisu**: ~5-10 sekund per osoba

## 🛠️ Development

### Building
```bash
dotnet build
```

### Running tests
```bash
dotnet test
```

### Database migrations
Aplikacja automatycznie tworzy/migruje bazę danych przy pierwszym uruchomieniu. Migration logic w `RecipeDbContext.cs`.

### Adding new AI providers
1. Implement `IAIService` interface
2. Add to `AIServiceFactory`
3. Create database entry w `AIProviders` table
4. Update UI w settings tab

## 🐛 Troubleshooting

### "No active AI provider configured"
- Sprawdź czy masz aktywny provider w Settings → AI Providers
- Sprawdź czy klucz API jest poprawny
- Użyj przycisku "Test Connection"

### "Failed to extract recipes"
- Sprawdź logi w konsoli (szczegółowe informacje)
- Sprawdź czy PDF nie jest zaszyfrowany
- Spróbuj innego providera (OpenAI vs Gemini)
- Zmniejsz pagesPerChunk jeśli timeout

### "Timeout podczas przetwarzania"
- Zwiększ timeout w service constructors
- Zmniejsz liczbę stron per chunk
- Zwiększ delay między chunkami (rate limiting)

### "Brak przeskalowanych składników"
- Sprawdź czy plan ma dodane osoby
- Sprawdź czy przepisy zostały przeskalowane (przycisk "Skaluj przepisy")
- Sprawdź logi skalowania w konsoli

### "AI źle wyciągnął przepisy" lub "Brakuje przepisów"
- Użyj funkcji regeneracji (Pliki Źródłowe → wybierz plik → Regeneruj)
- Sprawdź czy używasz najlepszego providera/modelu
- Zwiększ overlap pages (ustawienia) jeśli przepisy są rozłożone na wiele stron
- Sprawdź logi przetwarzania - może być timeout lub błąd parsowania
- Przetestuj inny provider (Gemini vs OpenAI) - różne modele mają różne mocne strony

### "Jak przetestować nowy model na starych plikach?"
1. Przejdź do Ustawienia → AI Providers
2. Zmień model lub providera
3. Przejdź do Pliki Źródłowe
4. Wybierz plik który chcesz przetestować
5. Kliknij "Regeneruj"
6. Porównaj wyniki (liczba przepisów, jakość ekstrakcji)

## 📝 Licencja

MIT License

## 👤 Autor

**Vesperino**

- GitHub: [@Vesperino](https://github.com/Vesperino)

## 🤝 Wsparcie

W razie problemów lub pytań:
1. Sprawdź sekcję **Troubleshooting** powyżej
2. Przejrzyj logi w konsoli aplikacji
3. Utwórz issue na GitHubie: https://github.com/Vesperino/RecipesAIHelper/issues

## 🎓 Roadmap (Przyszłe funkcje)

- [ ] Import przepisów z URL (scraping)
- [ ] Export jadłospisów do PDF
- [ ] Nutrition analytics (wykresy, statystyki)
- [ ] Mobile app (React Native / Flutter)
- [ ] Cloud sync (opcjonalne)
- [ ] Multi-language support (EN, DE, FR)
- [ ] Recipe recommendations based on preferences
- [ ] Shopping list optimization (cost, store location)
- [ ] Meal prep suggestions (batch cooking)
- [ ] Leftover management

## 📚 Dokumentacja techniczna

### Key design patterns:
- **Factory Pattern**: AIServiceFactory, ImageGenerationServiceFactory
- **Repository Pattern**: RecipeDbContext
- **Service Layer**: Wszystkie services w Services/
- **Hybrid JSON Storage**: Computed properties z JSON columns
- **Retry Pattern**: Polly policies dla resilience
- **Rate Limiting**: Semaphore + delays

### Database schema highlights:
- **Recipes**: Main table z hybrid JSON storage
- **AIProviders**: Multi-provider configuration
- **MealPlans**: Jadłospisy z days, entries, persons
- **MealPlanRecipes**: Przeskalowane przepisy per osoba
- **ShoppingLists**: Agregowane listy zakupów
- **Settings**: Key-value store dla konfiguracji
- **SourceFiles**: Historia przetworzonych plików

### API endpoints summary:
- `/api/recipes`: CRUD przepisów (GET, POST, PUT, DELETE)
- `/api/processing`: Przetwarzanie PDF/images
  - POST `/api/processing/process-selected-files`: Przetwarzanie wybranych plików z folderu
  - POST `/api/processing/upload`: Upload i przetwarzanie plików
- `/api/aiproviders`: Zarządzanie providerami
  - GET `/api/aiproviders`: Lista providerów z priorytetami
  - PUT `/api/aiproviders/{id}`: Aktualizacja konfiguracji providera
  - POST `/api/aiproviders/{id}/toggle`: Włącz/wyłącz providera
- `/api/mealplans`: Planowanie posiłków
  - GET `/api/mealplans`: Lista wszystkich planów
  - GET `/api/mealplans/{id}`: Szczegóły planu z dniami i przepisami
  - POST `/api/mealplans`: Tworzenie nowego planu
  - PUT `/api/mealplans/{id}`: Aktualizacja planu
  - DELETE `/api/mealplans/{id}`: Usunięcie planu
- `/api/mealplans/{id}/persons`: Zarządzanie osobami
  - GET `/api/mealplans/{id}/persons`: Lista osób w planie
  - POST `/api/mealplans/{id}/persons`: Dodanie osoby
  - PUT `/api/mealplans/{id}/persons/{personId}`: Aktualizacja osoby
  - DELETE `/api/mealplans/{id}/persons/{personId}`: Usunięcie osoby
- `/api/mealplans/{id}/auto-generate`: Auto-generowanie przepisów (POST)
- `/api/mealplans/{id}/scale-recipes`: Skalowanie przepisów dla osób (POST)
- `/api/mealplans/{id}/shopping-list`: Listy zakupów
  - GET: Pobierz zapisaną listę
  - POST: Wygeneruj nową listę
- `/api/images`: Generowanie obrazów
  - POST `/api/images/generate/{recipeId}`: Generuj obraz dla przepisu
  - POST `/api/images/generate-all-missing`: Batch generowanie dla wszystkich bez obrazów
- `/api/imagesettings`: Konfiguracja image generation
  - GET: Pobierz ustawienia (klucze maskowane)
  - PUT: Aktualizuj ustawienia
  - POST `/api/imagesettings/switch-provider`: Przełącz providera
  - POST `/api/imagesettings/test`: Test generowania
- `/api/todoist`: Export do Todoist
  - POST `/api/todoist/export-shopping-list`: Export listy zakupów
  - GET `/api/todoist/test-connection`: Test połączenia z Todoist
- `/api/sourcefiles`: Zarządzanie plikami źródłowymi
  - GET `/api/sourcefiles`: Lista plików z liczbą przepisów
  - GET `/api/sourcefiles/{fileName}/recipes`: Przepisy z konkretnego pliku
  - DELETE `/api/sourcefiles/{fileName}/recipes`: Usuń przepisy z pliku
  - POST `/api/sourcefiles/{fileName}/regenerate`: Regeneruj przepisy z pliku
- `/api/settings`: Ustawienia globalne (GET, PUT)
  - Zaawansowane parametry: chunking, overlap, delay, duplicates check

---

**Dziękujemy za korzystanie z Recipe AI Helper!** 🎉

Jeśli aplikacja Ci pomogła, rozważ dodanie ⭐ na GitHubie!
