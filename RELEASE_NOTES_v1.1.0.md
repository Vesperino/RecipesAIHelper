# RecipesAIHelper v1.1.0 - Shopping List AI & DoNotScale Flag

## Nowe funkcje

### 🛒 Multi-Provider Shopping List Generation
- **OpenAI Shopping List Service**: Generowanie list zakupów z wykorzystaniem GPT-4o/GPT-5
- **Gemini Shopping List Service**: Alternatywny provider używający Google Gemini
- **Factory Pattern**: Automatyczny wybór providera na podstawie konfiguracji w Settings
- **Enhanced Prompts**: Szczegółowe kategorie produktów (warzywa, owoce, nabiał, mięso, ryby, przyprawy, etc.)
- Automatyczne grupowanie składników według kategorii
- Obsługa jednostek miary i konwersji

### 🚫 DoNotScale Flag for Recipes
- Nowa flaga `DoNotScale` w modelu Recipe
- Przepisy oznaczone jako DoNotScale NIE są automatycznie skalowane podczas planowania posiłków
- Przydatne dla:
  - Suplementów diety
  - Shake'ów proteinowych
  - Przepisów o stałych porcjach
- UI wskazuje które przepisy mają wyłączone skalowanie
- Logika skalowania automatycznie pomija te przepisy

### 🎨 UI/UX Improvements
- **Kompaktowe filtry**: Zoptymalizowany układ filtrów przepisów
- **Lepsze wskaźniki**: Wyraźne oznaczenia DoNotScale w liście przepisów
- Poprawiona responsywność interfejsu
- Bardziej intuicyjna nawigacja

## Zmiany techniczne

### Refactoring AI Services
- **RecipeScalingServiceFactory**: Centralna fabryka dla serwisów skalowania
  - `OpenAIRecipeScalingService`
  - `GeminiRecipeScalingService`
- **ShoppingListServiceFactory**: Centralna fabryka dla list zakupów
  - `OpenAIShoppingListService`
  - `GeminiShoppingListService`
- Interfejsy `IRecipeScalingService` i `IShoppingListService` dla łatwego rozszerzania

### Database Changes
- Nowa kolumna `DoNotScale` (INTEGER) w tabeli Recipes
- Nowy model `ShoppingListModels.cs` dla strukturyzacji danych

### Code Cleanup
- Usunięcie przestarzałego `DessertPlanningService.cs`
- Rename `RecipeScalingService.cs` → `GeminiRecipeScalingService.cs`
- Rename `ShoppingListService.cs` → `GeminiShoppingListService.cs`
- Aktualizacja dependency injection w `Program.cs`

## Poprawki błędów
- Fix: Przepisy z flagą DoNotScale nie są skalowane podczas auto-generowania planów
- Fix: Poprawione zarządzanie AIModelSettings dla shopping list services

## Statystyki
- **+1932 wierszy** dodanych
- **-731 wierszy** usuniętych
- **22 pliki** zmodyfikowane
- **4 nowe serwisy** AI

---

📦 **Instalacja**: Pobierz RecipesAIHelper-Release.zip i uruchom RecipesAIHelper.exe
⚙️ **Konfiguracja**: Skonfiguruj klucze API w zakładce Settings
📖 **Dokumentacja**: Zobacz README-RELEASE.txt dla szczegółowych instrukcji
