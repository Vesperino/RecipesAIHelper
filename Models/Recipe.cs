using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipesAIHelper.Models;

public class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Ingredients { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    // Nutrition information (default/main values)
    public int Calories { get; set; }
    public double Protein { get; set; }  // in grams
    public double Carbohydrates { get; set; }  // in grams
    public double Fat { get; set; }  // in grams
    public int? Servings { get; set; }  // number of servings (portions)

    // Nutrition variants (stored as JSON in database)
    [JsonIgnore]
    public string? NutritionVariantsJson { get; set; }

    public List<NutritionVariant>? NutritionVariants
    {
        get
        {
            if (string.IsNullOrEmpty(NutritionVariantsJson))
                return null;

            try
            {
                return JsonSerializer.Deserialize<List<NutritionVariant>>(NutritionVariantsJson);
            }
            catch
            {
                return null;
            }
        }
        set
        {
            if (value == null || value.Count == 0)
            {
                NutritionVariantsJson = null;
            }
            else
            {
                NutritionVariantsJson = JsonSerializer.Serialize(value);
            }
        }
    }

    // Meal category
    public MealType MealType { get; set; }

    // Alternate meal category (e.g., can be both Sniadanie and Kolacja)
    public MealType? AlternateMealType { get; set; }

    public DateTime CreatedAt { get; set; }

    // Image paths
    public string? ImagePath { get; set; }
    public string? ImageUrl { get; set; }
}

public class NutritionVariant
{
    public string Label { get; set; } = string.Empty;  // np. "całość", "na porcję", "z dodatkami"

    [JsonConverter(typeof(FlexibleIntConverter))]
    public int Calories { get; set; }

    [JsonConverter(typeof(FlexibleDoubleConverter))]
    public double Protein { get; set; }

    [JsonConverter(typeof(FlexibleDoubleConverter))]
    public double Carbohydrates { get; set; }

    [JsonConverter(typeof(FlexibleDoubleConverter))]
    public double Fat { get; set; }

    public string? Notes { get; set; }  // opcjonalne uwagi jak "* Same chlebki"
}

public enum MealType
{
    Sniadanie,    // Śniadanie
    Obiad,        // Obiad
    Kolacja,      // Kolacja
    Deser,        // Deser
    Napoj         // Napój
}
