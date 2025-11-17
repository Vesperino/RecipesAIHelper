using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Polly;
using Polly.Retry;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Service for scaling recipe ingredients using OpenAI (GPT models)
/// </summary>
public class OpenAIRecipeScalingService : IRecipeScalingService
{
    private readonly ChatClient _chatClient;
    private readonly string _modelName;
    private readonly AsyncRetryPolicy _retryPolicy;
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1); // Rate limiting

    public OpenAIRecipeScalingService(string apiKey, string modelName = "gpt-5-mini-2025-08-07")
    {
        _modelName = modelName;

        // Create client with extended timeout
        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromMinutes(2)
        };
        _chatClient = new ChatClient(modelName, new ApiKeyCredential(apiKey), clientOptions);

        // Retry policy: 3 attempts with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"   ⚠️ Retry {retryCount}/3 po {timeSpan.TotalSeconds:F1}s: {exception.Message}");
                });

        Console.WriteLine($"✅ OpenAIRecipeScalingService zainicjalizowany ({modelName})");
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

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("Jesteś ekspertem od skalowania przepisów kulinarnych. Odpowiadaj TYLKO w formacie JSON, bez dodatkowego tekstu."),
                    new UserChatMessage(prompt)
                };

                var chatCompletion = await _chatClient.CompleteChatAsync(messages);
                var responseText = chatCompletion.Value.Content[0].Text.Trim();

                // Debug logging - ZAWSZE pokazuj pełną odpowiedź
                if (string.IsNullOrEmpty(responseText))
                {
                    Console.WriteLine("   🔍 DEBUG: Pusta odpowiedź od AI");
                    throw new Exception("Empty AI response");
                }

                Console.WriteLine($"   🔍 DEBUG: Odpowiedź AI ({responseText.Length} znaków)");
                Console.WriteLine($"   🔍 DEBUG: PEŁNA SUROWA ODPOWIEDŹ:");
                Console.WriteLine("   " + new string('─', 60));
                Console.WriteLine(responseText);
                Console.WriteLine("   " + new string('─', 60));

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

        promptBuilder.AppendLine($"Przeskaluj składniki przepisu przez współczynnik {scalingFactor:F2}.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**DANE:**");
        promptBuilder.AppendLine($"Przepis: {baseRecipe.Name} ({mealType})");
        promptBuilder.AppendLine($"Współczynnik: {scalingFactor:F2} ({(scalingFactor > 1 ? "+" : "")}{(scalingFactor - 1) * 100:F0}%)");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**SKŁADNIKI BAZOWE:**");
        promptBuilder.AppendLine(baseRecipe.Ingredients);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**ZASADY:**");
        promptBuilder.AppendLine($"1. Pomnóż każdą ilość przez {scalingFactor:F2}");
        promptBuilder.AppendLine("2. Zaokrąglij do praktycznych wartości:");
        promptBuilder.AppendLine("   - >100g → do 5g lub 10g (127g → 130g)");
        promptBuilder.AppendLine("   - <100g → do 1g lub 5g (23g → 25g)");
        promptBuilder.AppendLine("   - Płyny → do 5ml lub 10ml");
        promptBuilder.AppendLine("   - Sztuki → do 0.5 lub całości (1.3 → 1.5)");
        promptBuilder.AppendLine("3. Zachowaj jednostki z oryginału");
        promptBuilder.AppendLine("4. \"Do smaku\"/\"opcjonalnie\" → bez zmian");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**FORMAT JSON (TYLKO JSON, BEZ TEKSTU):**");
        promptBuilder.AppendLine(@"{
  ""scaledIngredients"": [
    ""linia 1"",
    ""linia 2""
  ]
}");

        return promptBuilder.ToString();
    }
}
