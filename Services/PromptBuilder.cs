using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Wspólny budowniczy promptów dla wszystkich AI (OpenAI, Gemini, etc.)
/// </summary>
public static class PromptBuilder
{
    /// <summary>
    /// Główny prompt dla ekstrakcji przepisów z obrazów (chunki PDF)
    /// </summary>
    public static string BuildImageExtractionPrompt(List<Recipe>? recentRecipes = null, List<string>? alreadyProcessedInPdf = null)
    {
        var recentRecipesContext = "";
        if (recentRecipes != null && recentRecipes.Count > 0)
        {
            recentRecipesContext = "\n\n❌ **NIE EKSTRAKTUJ PONOWNIE** tych przepisów (już są w bazie):\n";
            foreach (var recipe in recentRecipes)
            {
                recentRecipesContext += $"  - {recipe.Name}\n";
            }
        }

        var alreadyProcessedContext = "";
        if (alreadyProcessedInPdf != null && alreadyProcessedInPdf.Count > 0)
        {
            alreadyProcessedContext = "\n\n⚠️ **NIE EKSTRAKTUJ PONOWNIE** tych przepisów (już w tym PDFie):\n";
            foreach (var recipeName in alreadyProcessedInPdf)
            {
                alreadyProcessedContext += $"  - {recipeName}\n";
            }
        }

        return $@"Jesteś ekspertem w analizie przepisów kulinarnych z książek kucharskich.

## ZADANIE
Ekstraktuj przepisy z obrazów stron PDF. Każdy przepis MUSI mieć:
- Nazwę
- Składniki z ilościami
- Instrukcje krok po kroku
- Wartości odżywcze z tabeli

## WARTOŚCI ODŻYWCZE - WAŻNE!

Tabele mają różne wiersze:
- ""całość"" - dla całego dania
- ""porcja"" lub ""na porcję"" - dla jednej porcji
- ""½ porcji"" lub ""1/2 porcji"" - dla połowy
- Przypisy (*, **) - dodatkowe warianty

### INSTRUKCJA:

1. **Podstawowe wartości** (calories, protein, carbohydrates, fat):
   - Użyj wiersza ""porcja"" lub ""na porcję""
   - Jeśli nie ma, użyj pierwszego wiersza

2. **nutritionVariants** - EKSTRAKTUJ WSZYSTKIE WIERSZE:
   ```json
   ""nutritionVariants"": [
     {{""label"": ""całość"", ""calories"": 366, ""protein"": 10.0, ""carbohydrates"": 76.0, ""fat"": 2.0, ""notes"": null}},
     {{""label"": ""na porcję"", ""calories"": 92, ""protein"": 3.0, ""carbohydrates"": 19.0, ""fat"": 0.0, ""notes"": ""Same chlebki, cztery porcje""}},
     {{""label"": ""z dodatkami"", ""calories"": 300, ""protein"": 16.0, ""carbohydrates"": 39.0, ""fat"": 7.0, ""notes"": ""Chlebki z wędliną, oliwą i sosem czosnkowym""}}
   ]
   ```

3. **servings** - szukaj tekstu ""Liczba porcji: X""

### PRZYKŁAD Z OBRAZU:
```
Tabela:
| całość       | kcal: 366 | B: 10 | W: 76 | T: 2  |
| na porcję*   | kcal: 92  | B: 3  | W: 19 | T: 0  |
| z dodatkami**| kcal: 300 | B: 16 | W: 39 | T: 7  |

Liczba porcji: 4 lub 2 po złożeniu
* Same chlebki, cztery porcje.
** Chlebki z wędliną, oliwą i sosem czosnkowym, dwie porcje po złożeniu.
```

→ Zapisz:
```json
{{
  ""calories"": 92,
  ""protein"": 3.0,
  ""carbohydrates"": 19.0,
  ""fat"": 0.0,
  ""servings"": 4,
  ""nutritionVariants"": [
    {{""label"": ""całość"", ""calories"": 366, ""protein"": 10.0, ""carbohydrates"": 76.0, ""fat"": 2.0, ""notes"": null}},
    {{""label"": ""na porcję"", ""calories"": 92, ""protein"": 3.0, ""carbohydrates"": 19.0, ""fat"": 0.0, ""notes"": ""Same chlebki, cztery porcje""}},
    {{""label"": ""z dodatkami"", ""calories"": 300, ""protein"": 16.0, ""carbohydrates"": 39.0, ""fat"": 7.0, ""notes"": ""Chlebki z wędliną, oliwą i sosem czosnkowym, dwie porcje po złożeniu""}}
  ]
}}
```

## INSTRUKCJE PRZYGOTOWANIA

Formatuj instrukcje tak, aby KAŻDY KROK był w NOWEJ LINII:
```
""instructions"": ""1. W wysokiej misce mieszamy mąkę z przyprawami i ciepłą wodą.\n2. Posypujemy blat mąką i wałkujemy ciasto.\n3. Smażymy na patelni z dwóch stron.""
```

## DODATKI

Jeśli przepis zawiera dodatki (sosy, dipsy, garnitury, np. ""podawaj z sosem..."", ""opcjonalnie...""):
- Dołącz je do składników
- Uwzględnij w instrukcjach

## DUPLIKATY{alreadyProcessedContext}{recentRecipesContext}

## WYMAGANE POLA

- `name`: nazwa przepisu
- `description`: krótki opis (1-2 zdania)
- `ingredients`: lista składników Z ILOŚCIAMI
- `instructions`: kroki przygotowania (każdy krok w nowej linii, separator \n)
- `calories`: kalorie NA PORCJĘ (int)
- `protein`: białko w gramach (double)
- `carbohydrates`: węglowodany w gramach (double)
- `fat`: tłuszcze w gramach (double)
- `mealType`: ""Sniadanie"", ""Obiad"", ""Kolacja"", ""Deser"", lub ""Napoj""
- `servings`: liczba porcji (int, nullable)
- `nutritionVariants`: WSZYSTKIE wiersze z tabeli

## FORMAT ODPOWIEDZI

Zwróć TYLKO JSON (bez markdown, bez ```json):

{{
  ""recipes"": [
    {{
      ""name"": ""Chlebki Czosnkowe"",
      ""description"": ""Domowe chlebki czosnkowe jako zamiennik pieczywa"",
      ""ingredients"": [""100g mąki"", ""60ml wody"", ""2 łyżeczki czosnku""],
      ""instructions"": ""1. Mieszamy mąkę z wodą i przyprawami.\n2. Wałkujemy ciasto.\n3. Smażymy na patelni."",
      ""calories"": 92,
      ""protein"": 3.0,
      ""carbohydrates"": 19.0,
      ""fat"": 0.0,
      ""mealType"": ""Sniadanie"",
      ""servings"": 4,
      ""nutritionVariants"": [
        {{""label"": ""całość"", ""calories"": 366, ""protein"": 10.0, ""carbohydrates"": 76.0, ""fat"": 2.0, ""notes"": null}},
        {{""label"": ""na porcję"", ""calories"": 92, ""protein"": 3.0, ""carbohydrates"": 19.0, ""fat"": 0.0, ""notes"": ""Same chlebki, cztery porcje""}},
        {{""label"": ""z dodatkami"", ""calories"": 300, ""protein"": 16.0, ""carbohydrates"": 39.0, ""fat"": 7.0, ""notes"": ""Chlebki z wędliną""}}
      ]
    }}
  ]
}}

## ZASADY
- ❌ NIE dodawaj jednostek do wartości (450kcal → 450)
- ❌ NIE używaj przecinka w liczbach (12,5 → 12.5)
- ❌ NIE pomijaj ilości w składnikach
- ✅ ZAWSZE ekstraktuj WSZYSTKIE wiersze z tabeli do nutritionVariants
- ✅ KAŻDY krok instrukcji w nowej linii (separator \n)";
    }

    /// <summary>
    /// Prompt dla bezpośredniej analizy PDF (cały dokument naraz)
    /// </summary>
    public static string BuildPdfExtractionPrompt(List<Recipe>? recentRecipes = null)
    {
        var recentRecipesContext = "";
        if (recentRecipes != null && recentRecipes.Count > 0)
        {
            recentRecipesContext = "\n\n❌ **NIE EKSTRAKTUJ PONOWNIE** tych przepisów (już są w bazie):\n";
            foreach (var recipe in recentRecipes)
            {
                recentRecipesContext += $"  - {recipe.Name}\n";
            }
        }

        return $@"Jesteś ekspertem w analizie przepisów kulinarnych z książek kucharskich.

## ZADANIE
Przeanalizuj CAŁY PDF i ekstraktuj wszystkie przepisy. Każdy przepis MUSI mieć:
- Nazwę
- Składniki z ilościami
- Instrukcje krok po kroku
- Wartości odżywcze z tabeli

## WARTOŚCI ODŻYWCZE - WAŻNE!

Tabele mają różne wiersze:
- ""całość"" - dla całego dania
- ""porcja"" lub ""na porcję"" - dla jednej porcji
- ""½ porcji"" lub ""1/2 porcji"" - dla połowy
- Przypisy (*, **) - dodatkowe warianty

### INSTRUKCJA:

1. **Podstawowe wartości** (calories, protein, carbohydrates, fat):
   - Użyj wiersza ""porcja"" lub ""na porcję""
   - Jeśli nie ma, użyj pierwszego wiersza

2. **nutritionVariants** - EKSTRAKTUJ WSZYSTKIE WIERSZE:
   ```json
   ""nutritionVariants"": [
     {{""label"": ""całość"", ""calories"": 366, ""protein"": 10.0, ""carbohydrates"": 76.0, ""fat"": 2.0, ""notes"": null}},
     {{""label"": ""na porcję"", ""calories"": 92, ""protein"": 3.0, ""carbohydrates"": 19.0, ""fat"": 0.0, ""notes"": ""Same chlebki""}},
     {{""label"": ""z dodatkami"", ""calories"": 300, ""protein"": 16.0, ""carbohydrates"": 39.0, ""fat"": 7.0, ""notes"": ""Z wędliną""}}
   ]
   ```

3. **servings** - szukaj tekstu ""Liczba porcji: X""

## INSTRUKCJE PRZYGOTOWANIA

Formatuj instrukcje tak, aby KAŻDY KROK był w NOWEJ LINII:
```
""instructions"": ""1. Mieszamy składniki.\n2. Formujemy ciasto.\n3. Pieczemy.""
```

## DODATKI

Jeśli przepis zawiera dodatki (sosy, dipsy, garnitury, np. ""podawaj z sosem..."", ""opcjonalnie...""):
- Dołącz je do składników
- Uwzględnij w instrukcjach

## DUPLIKATY{recentRecipesContext}

## WYMAGANE POLA

- `name`: nazwa przepisu
- `description`: krótki opis (1-2 zdania)
- `ingredients`: lista składników Z ILOŚCIAMI
- `instructions`: kroki przygotowania (każdy krok w nowej linii, separator \n)
- `calories`: kalorie NA PORCJĘ (int)
- `protein`: białko w gramach (double)
- `carbohydrates`: węglowodany w gramach (double)
- `fat`: tłuszcze w gramach (double)
- `mealType`: ""Sniadanie"", ""Obiad"", ""Kolacja"", ""Deser"", lub ""Napoj""
- `servings`: liczba porcji (int, nullable)
- `nutritionVariants`: WSZYSTKIE wiersze z tabeli

## FORMAT ODPOWIEDZI

Zwróć TYLKO JSON (bez markdown, bez ```json):

{{
  ""recipes"": [
    {{
      ""name"": ""Nazwa przepisu"",
      ""description"": ""Krótki opis"",
      ""ingredients"": [""200g mąki"", ""100ml wody""],
      ""instructions"": ""1. Pierwszy krok.\n2. Drugi krok.\n3. Trzeci krok."",
      ""calories"": 250,
      ""protein"": 10.0,
      ""carbohydrates"": 30.0,
      ""fat"": 5.0,
      ""mealType"": ""Obiad"",
      ""servings"": 2,
      ""nutritionVariants"": [
        {{""label"": ""całość"", ""calories"": 500, ""protein"": 20.0, ""carbohydrates"": 60.0, ""fat"": 10.0, ""notes"": null}},
        {{""label"": ""na porcję"", ""calories"": 250, ""protein"": 10.0, ""carbohydrates"": 30.0, ""fat"": 5.0, ""notes"": null}}
      ]
    }}
  ]
}}

## ZASADY
- ❌ NIE dodawaj jednostek do wartości (450kcal → 450)
- ❌ NIE używaj przecinka w liczbach (12,5 → 12.5)
- ❌ NIE pomijaj ilości w składnikach
- ✅ ZAWSZE ekstraktuj WSZYSTKIE wiersze z tabeli do nutritionVariants
- ✅ KAŻDY krok instrukcji w nowej linii (separator \n)";
    }

    /// <summary>
    /// Buduje pełny prompt użytkownika dla obrazów
    /// </summary>
    public static string BuildImageUserPrompt(int startPage, int endPage, int imageCount)
    {
        return $"📄 To są strony {startPage}-{endPage} z książki kucharskiej ({imageCount} obrazów).\n\n" +
               $"Przeanalizuj każdą stronę i wyekstraktuj WSZYSTKIE przepisy.\n" +
               $"WAŻNE: Ekstraktuj WSZYSTKIE wiersze z tabel wartości odżywczych do pola nutritionVariants!";
    }

    /// <summary>
    /// Buduje pełny prompt użytkownika dla PDF
    /// </summary>
    public static string BuildPdfUserPrompt(string fileName)
    {
        return $"📄 Oto PDF z książki kucharskiej: {fileName}\n\n" +
               $"Przeanalizuj CAŁY dokument i wyekstraktuj WSZYSTKIE przepisy.\n" +
               $"WAŻNE: Ekstraktuj WSZYSTKIE wiersze z tabel wartości odżywczych do pola nutritionVariants!";
    }
}
