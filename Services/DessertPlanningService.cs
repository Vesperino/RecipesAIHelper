using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mscc.GenerativeAI;
using Polly;
using Polly.Retry;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Service for planning dessert portions across multiple days using AI
/// </summary>
public class DessertPlanningService
{
    private readonly GoogleAI _genAi;
    private readonly GenerativeModel _model;
    private readonly AsyncRetryPolicy _retryPolicy;
    private static readonly SemaphoreSlim _rateLimiter = new(1, 1);

    public DessertPlanningService(string apiKey, string modelName = "gemini-2.5-flash")
    {
        _genAi = new GoogleAI(apiKey);
        _model = _genAi.GenerativeModel(model: modelName);
        _model.Timeout = TimeSpan.FromMinutes(2);

        // Retry policy with exponential backoff
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

        Console.WriteLine($"✅ DessertPlanningService zainicjalizowany ({modelName})");
    }

    /// <summary>
    /// Plan how to distribute a dessert across multiple persons and days
    /// </summary>
    public async Task<DessertPlan> PlanDessertAsync(
        Recipe dessert,
        List<MealPlanPerson> persons,
        int maxDays = 7)
    {
        // Rate limiting
        await _rateLimiter.WaitAsync();
        try
        {
            await Task.Delay(2000); // 2 second delay between AI calls

            Console.WriteLine($"🍰 Planowanie deseru '{dessert.Name}' dla {persons.Count} osób...");

            var plan = await _retryPolicy.ExecuteAsync(async () =>
            {
                var prompt = BuildDessertPlanningPrompt(dessert, persons, maxDays);
                var response = await _model.GenerateContent(prompt);
                var responseText = response?.Text?.Trim() ?? "";

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
                DessertPlan? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<DessertPlan>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"   🔍 DEBUG: Błąd parsowania JSON: {jsonEx.Message}");
                    Console.WriteLine($"   🔍 DEBUG: Próbowano parsować: {jsonResponse.Substring(0, Math.Min(200, jsonResponse.Length))}...");

                    // Save to file for debugging
                    var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"dessert_error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(debugPath, $"Dessert: {dessert.Name}\nPersons: {persons.Count}\n\nResponse:\n{responseText}");
                    Console.WriteLine($"   🔍 DEBUG: Zapisano pełną odpowiedź do: {debugPath}");

                    throw new Exception($"JSON parse error: {jsonEx.Message}");
                }

                if (parsed == null)
                {
                    Console.WriteLine("   🔍 DEBUG: AI zwróciło null po deserializacji");
                    throw new Exception("Null dessert plan from AI");
                }

                return parsed;
            });

            Console.WriteLine($"✅ Plan deseru: {plan.TotalPortions} porcji, {plan.DaysToSpread} dni");
            return plan;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd planowania deseru (po wszystkich retry): {ex.Message}");
            return GetDefaultDessertPlan(dessert, persons.Count);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private string BuildDessertPlanningPrompt(Recipe dessert, List<MealPlanPerson> persons, int maxDays)
    {
        var nutritionInfo = dessert.NutritionVariants != null && dessert.NutritionVariants.Count > 0
            ? string.Join("\n", dessert.NutritionVariants.Select(v => $"  - {v.Label}: {v.Calories} kcal"))
            : $"  - Pojedyncza porcja: {dessert.Calories} kcal";

        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("Zaplanuj rozłożenie deseru dla grupy osób na kilka dni.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**DANE:**");
        promptBuilder.AppendLine($"Deser: {dessert.Name}");
        promptBuilder.AppendLine("Warianty odżywcze:");
        promptBuilder.AppendLine(nutritionInfo);
        if (dessert.Servings.HasValue)
        {
            promptBuilder.AppendLine($"Liczba porcji: {dessert.Servings}");
        }
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"Osoby ({persons.Count}):");
        foreach (var person in persons)
        {
            promptBuilder.AppendLine($"  - {person.Name}: {person.TargetCalories} kcal/dzień");
        }
        promptBuilder.AppendLine($"Budżet na desery: ~200-300 kcal/osoba/dzień");
        promptBuilder.AppendLine($"Max dni do rozłożenia: {maxDays}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**ZASADY:**");
        promptBuilder.AppendLine("- Każda osoba dostaje TĘ SAMĄ wielkość porcji");
        promptBuilder.AppendLine("- Priorytet: nie zostawiać resztek (pełne porcje)");
        promptBuilder.AppendLine("- Jeśli deser >600 kcal całość → rozłóż na kilka dni");
        promptBuilder.AppendLine("- Jeśli deser <400 kcal całość → można 1 dzień");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**FORMAT JSON (TYLKO JSON, BEZ TEKSTU):**");
        promptBuilder.AppendLine(@"{
  ""totalPortions"": 4,
  ""portionCalories"": 256.5,
  ""portionsPerPerson"": 1.0,
  ""daysToSpread"": 2,
  ""portionsPerDay"": 2,
  ""explanation"": ""Krótkie uzasadnienie planu""
}");

        return promptBuilder.ToString();
    }

    private DessertPlan GetDefaultDessertPlan(Recipe dessert, int personsCount)
    {
        // Simple fallback logic
        var totalPortions = dessert.Servings ?? 1;
        var portionCalories = dessert.Calories;
        var daysToSpread = (int)Math.Ceiling((double)totalPortions / personsCount);

        return new DessertPlan
        {
            TotalPortions = totalPortions,
            PortionCalories = portionCalories,
            PortionsPerPerson = 1.0,
            DaysToSpread = Math.Min(daysToSpread, 7),
            PortionsPerDay = personsCount,
            Explanation = $"Default plan: {totalPortions} porcji dla {personsCount} osób = {daysToSpread} dni"
        };
    }
}

/// <summary>
/// Dessert planning result from AI
/// </summary>
public class DessertPlan
{
    [JsonPropertyName("totalPortions")]
    public int TotalPortions { get; set; }

    [JsonPropertyName("portionCalories")]
    public double PortionCalories { get; set; }

    [JsonPropertyName("portionsPerPerson")]
    public double PortionsPerPerson { get; set; }

    [JsonPropertyName("daysToSpread")]
    public int DaysToSpread { get; set; }

    [JsonPropertyName("portionsPerDay")]
    public int PortionsPerDay { get; set; }

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;
}
