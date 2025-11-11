using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mscc.GenerativeAI;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Service for planning dessert portions across multiple days using AI
/// </summary>
public class DessertPlanningService
{
    private readonly GoogleAI _genAi;
    private readonly GenerativeModel _model;

    public DessertPlanningService(string apiKey, string modelName = "gemini-2.5-flash")
    {
        _genAi = new GoogleAI(apiKey);
        _model = _genAi.GenerativeModel(model: modelName);
        _model.Timeout = TimeSpan.FromMinutes(2);

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
        try
        {
            Console.WriteLine($"🍰 Planowanie deseru '{dessert.Name}' dla {persons.Count} osób...");

            var prompt = BuildDessertPlanningPrompt(dessert, persons, maxDays);
            var response = await _model.GenerateContent(prompt);
            var responseText = response?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(responseText))
            {
                Console.WriteLine("❌ Pusta odpowiedź od AI");
                return GetDefaultDessertPlan(dessert, persons.Count);
            }

            // Remove markdown code blocks
            var jsonResponse = responseText
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            // Parse JSON response
            var plan = JsonSerializer.Deserialize<DessertPlan>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (plan == null)
            {
                Console.WriteLine("❌ AI nie zwróciło planu deseru");
                return GetDefaultDessertPlan(dessert, persons.Count);
            }

            Console.WriteLine($"✅ Plan deseru: {plan.TotalPortions} porcji, {plan.DaysToSpread} dni");
            return plan;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd planowania deseru: {ex.Message}");
            return GetDefaultDessertPlan(dessert, persons.Count);
        }
    }

    private string BuildDessertPlanningPrompt(Recipe dessert, List<MealPlanPerson> persons, int maxDays)
    {
        var nutritionInfo = dessert.NutritionVariants != null && dessert.NutritionVariants.Count > 0
            ? string.Join("\n", dessert.NutritionVariants.Select(v => $"  - {v.Label}: {v.Calories} kcal"))
            : $"  - Pojedyncza porcja: {dessert.Calories} kcal";

        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("Jesteś asystentem dietetycznym. Zaplanuj jak rozłożyć deser dla grupy osób.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**DESER:**");
        promptBuilder.AppendLine($"Nazwa: {dessert.Name}");
        promptBuilder.AppendLine("Warianty odżywcze:");
        promptBuilder.AppendLine(nutritionInfo);
        if (dessert.Servings.HasValue)
        {
            promptBuilder.AppendLine($"Liczba porcji (jeśli podana): {dessert.Servings}");
        }
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**OSOBY W PLANIE:**");
        foreach (var person in persons)
        {
            promptBuilder.AppendLine($"  - {person.Name}: {person.TargetCalories} kcal/dzień");
        }
        promptBuilder.AppendLine($"Liczba osób: {persons.Count}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**BUDŻET DZIENNY NA DESERY:** ~12% dziennych kalorii (ok. 200-300 kcal/osoba)");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**ZADANIE:**");
        promptBuilder.AppendLine("1. Przeanalizuj warianty odżywcze deseru");
        promptBuilder.AppendLine("2. Określ ile porcji ma cały przepis (jeśli nie podano, oblicz z kalorii)");
        promptBuilder.AppendLine($"3. Oblicz ile porcji potrzeba dziennie dla {persons.Count} osób");
        promptBuilder.AppendLine($"4. Zaplanuj na ile dni wystarczy (max {maxDays} dni)");
        promptBuilder.AppendLine("5. **WAŻNE**: Każda osoba dostaje TĘ SAMĄ WIELKOŚĆ PORCJI (bez skalowania dla deserów!)");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**ZASADY:**");
        promptBuilder.AppendLine("- Jeśli deser ma >600 kcal całość → rozłóż na kilka dni");
        promptBuilder.AppendLine("- Jeśli deser ma <400 kcal całość → 1 porcja na osobę dziennie");
        promptBuilder.AppendLine("- Priorytetyzuj aby nie zostawać resztek (pełne porcje)");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**FORMAT ODPOWIEDZI:**");
        promptBuilder.AppendLine("Zwróć JSON:");
        promptBuilder.AppendLine(@"{
  ""totalPortions"": 4,
  ""portionCalories"": 300,
  ""portionsPerPerson"": 1.0,
  ""daysToSpread"": 2,
  ""portionsPerDay"": 3,
  ""explanation"": ""Deser ma 4 porcje po 300 kcal. Dla 3 osób wystarczy na 1.3 dnia, zaokrąglamy do 2 dni (dzień 1: wszyscy, dzień 2: 1 osoba)""
}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**PRZYKŁAD:**");
        promptBuilder.AppendLine("Tort (całość: 1200 kcal, porcja: 300 kcal) dla 3 osób:");
        promptBuilder.AppendLine("- totalPortions: 4 (1200/300)");
        promptBuilder.AppendLine("- portionCalories: 300");
        promptBuilder.AppendLine("- portionsPerPerson: 1.0 (każdy dostaje tyle samo)");
        promptBuilder.AppendLine("- daysToSpread: 2 (4 porcje / 3 osoby = 1.33 dni → 2 dni)");
        promptBuilder.AppendLine("- portionsPerDay: 3 (dzień 1), 1 (dzień 2)");

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
    public int PortionCalories { get; set; }

    [JsonPropertyName("portionsPerPerson")]
    public double PortionsPerPerson { get; set; }

    [JsonPropertyName("daysToSpread")]
    public int DaysToSpread { get; set; }

    [JsonPropertyName("portionsPerDay")]
    public int PortionsPerDay { get; set; }

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;
}
