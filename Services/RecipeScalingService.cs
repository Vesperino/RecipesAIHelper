using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mscc.GenerativeAI;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Service for scaling recipe ingredients using AI
/// </summary>
public class RecipeScalingService
{
    private readonly GoogleAI _genAi;
    private readonly GenerativeModel _model;

    public RecipeScalingService(string apiKey, string modelName = "gemini-2.5-flash")
    {
        _genAi = new GoogleAI(apiKey);
        _model = _genAi.GenerativeModel(model: modelName);
        _model.Timeout = TimeSpan.FromMinutes(2);

        Console.WriteLine($"✅ RecipeScalingService zainicjalizowany ({modelName})");
    }

    /// <summary>
    /// Scale recipe ingredients by a given factor using AI
    /// </summary>
    public async Task<List<string>> ScaleRecipeIngredientsAsync(
        Recipe baseRecipe,
        double scalingFactor,
        MealType mealType)
    {
        try
        {
            Console.WriteLine($"📊 Skalowanie składników przepisu '{baseRecipe.Name}' (współczynnik: {scalingFactor:F2})...");

            var prompt = BuildScalingPrompt(baseRecipe, scalingFactor, mealType);
            var response = await _model.GenerateContent(prompt);
            var responseText = response?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(responseText))
            {
                Console.WriteLine("❌ Pusta odpowiedź od AI");
                return new List<string>();
            }

            // Remove markdown code blocks
            var jsonResponse = responseText
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            // Parse JSON response
            var result = JsonSerializer.Deserialize<ScalingResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.ScaledIngredients == null || result.ScaledIngredients.Count == 0)
            {
                Console.WriteLine("❌ AI nie zwróciło przeskalowanych składników");
                return new List<string>();
            }

            Console.WriteLine($"✅ Przeskalowano {result.ScaledIngredients.Count} składników");
            return result.ScaledIngredients;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd skalowania składników: {ex.Message}");
            return new List<string>();
        }
    }

    private string BuildScalingPrompt(Recipe baseRecipe, double scalingFactor, MealType mealType)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("Jesteś asystentem kuchennym. Przeskaluj składniki przepisu według podanego współczynnika.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**PRZEPIS BAZOWY:**");
        promptBuilder.AppendLine($"Nazwa: {baseRecipe.Name}");
        promptBuilder.AppendLine($"Typ posiłku: {mealType}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**SKŁADNIKI BAZOWE:**");
        promptBuilder.AppendLine(baseRecipe.Ingredients);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"**WSPÓŁCZYNNIK SKALOWANIA:** {scalingFactor:F2} ({(scalingFactor > 1 ? "+" : "")}{(scalingFactor - 1) * 100:F0}%)");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**ZASADY:**");
        promptBuilder.AppendLine($"1. **Mnóż każdą ilość przez {scalingFactor:F2}**");
        promptBuilder.AppendLine("2. **Zaokrąglij do praktycznych wartości**:");
        promptBuilder.AppendLine("   - Dla składników >100g: zaokrąglij do 5g lub 10g (np. 127g → 130g)");
        promptBuilder.AppendLine("   - Dla składników <100g: zaokrąglij do 1g lub 5g (np. 23g → 25g)");
        promptBuilder.AppendLine("   - Dla płynów: zaokrąglij do 5ml lub 10ml");
        promptBuilder.AppendLine("   - Dla sztuk: zaokrąglij do 0.5 lub całości (np. 1.3 cebuli → 1.5 cebuli)");
        promptBuilder.AppendLine("3. **Zachowaj jednostki miary** z oryginału");
        promptBuilder.AppendLine("4. **Dla \"do smaku\" / \"opcjonalnie\"**: pozostaw bez zmian");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**FORMAT ODPOWIEDZI:**");
        promptBuilder.AppendLine("Zwróć JSON:");
        promptBuilder.AppendLine(@"{
  ""scaledIngredients"": [
    ""pierwsza linia składnika"",
    ""druga linia składnika"",
    ...
  ]
}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**PRZYKŁAD:**");
        promptBuilder.AppendLine("Bazowe: \"200g kurczaka\"");
        promptBuilder.AppendLine($"Współczynnik: {scalingFactor:F2}");
        promptBuilder.AppendLine($"Wynik: \"{(int)Math.Round(200 * scalingFactor / 5.0) * 5}g kurczaka\" (200 * {scalingFactor:F2} = {200 * scalingFactor:F1} → zaokrąglone)");

        return promptBuilder.ToString();
    }
}

/// <summary>
/// Response model for scaling API
/// </summary>
public class ScalingResponse
{
    [JsonPropertyName("scaledIngredients")]
    public List<string> ScaledIngredients { get; set; } = new();
}
