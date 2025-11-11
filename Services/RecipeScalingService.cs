using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mscc.GenerativeAI;
using Polly;
using Polly.Retry;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Service for scaling recipe ingredients using AI
/// </summary>
public class RecipeScalingService
{
    private readonly GoogleAI _genAi;
    private readonly GenerativeModel _model;
    private readonly AsyncRetryPolicy _retryPolicy;
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1); // Rate limiting

    public RecipeScalingService(string apiKey, string modelName = "gemini-2.5-flash")
    {
        _genAi = new GoogleAI(apiKey);
        _model = _genAi.GenerativeModel(model: modelName);
        _model.Timeout = TimeSpan.FromMinutes(2);

        // Retry policy: 3 attempts with exponential backoff + jitter
        _retryPolicy = Policy
            .Handle<Exception>(ex =>
                ex.Message.Contains("503") ||
                ex.Message.Contains("overloaded") ||
                ex.Message.Contains("UNAVAILABLE") ||
                ex.Message.Contains("RESOURCE_EXHAUSTED"))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"   ⚠️ Retry {retryCount}/3 po {timeSpan.TotalSeconds:F1}s: {exception.Message}");
                });

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
        // Rate limiting: wait for semaphore + 2s delay
        await _rateLimiter.WaitAsync();
        try
        {
            await Task.Delay(2000); // 2 second delay between AI calls

            Console.WriteLine($"📊 Skalowanie składników przepisu '{baseRecipe.Name}' (współczynnik: {scalingFactor:F2})...");

            var result = await _retryPolicy.ExecuteAsync(async () =>
            {
                var prompt = BuildScalingPrompt(baseRecipe, scalingFactor, mealType);
                var response = await _model.GenerateContent(prompt);
                var responseText = response?.Text?.Trim() ?? "";

                // Debug logging
                if (string.IsNullOrEmpty(responseText))
                {
                    Console.WriteLine("   🔍 DEBUG: Pusta odpowiedź od AI");
                    throw new Exception("Empty AI response");
                }

                Console.WriteLine($"   🔍 DEBUG: Odpowiedź AI ({responseText.Length} znaków)");
                if (responseText.Length < 500)
                {
                    Console.WriteLine($"   🔍 DEBUG: Surowa odpowiedź: {responseText}");
                }

                // Remove markdown code blocks
                var jsonResponse = responseText
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                // Parse JSON response
                ScalingResponse? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<ScalingResponse>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"   🔍 DEBUG: Błąd parsowania JSON: {jsonEx.Message}");
                    Console.WriteLine($"   🔍 DEBUG: Próbowano parsować: {jsonResponse.Substring(0, Math.Min(200, jsonResponse.Length))}...");

                    // Save to file for debugging
                    var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"scaling_error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(debugPath, $"Recipe: {baseRecipe.Name}\nFactor: {scalingFactor}\n\nResponse:\n{responseText}");
                    Console.WriteLine($"   🔍 DEBUG: Zapisano pełną odpowiedź do: {debugPath}");

                    throw new Exception($"JSON parse error: {jsonEx.Message}");
                }

                if (parsed?.ScaledIngredients == null || parsed.ScaledIngredients.Count == 0)
                {
                    Console.WriteLine("   🔍 DEBUG: AI zwróciło poprawny JSON, ale brak składników");
                    throw new Exception("No ingredients in AI response");
                }

                return parsed;
            });

            Console.WriteLine($"✅ Przeskalowano {result.ScaledIngredients.Count} składników");
            return result.ScaledIngredients;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd skalowania składników (po wszystkich retry): {ex.Message}");
            return new List<string>();
        }
        finally
        {
            _rateLimiter.Release();
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
