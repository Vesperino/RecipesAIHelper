using System.Text.Json.Serialization;

namespace RecipesAIHelper.Models;

public class RecipeExtractionResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("ingredients")]
    public List<string> Ingredients { get; set; } = new();

    [JsonPropertyName("instructions")]
    public string Instructions { get; set; } = string.Empty;

    [JsonPropertyName("calories")]
    public int Calories { get; set; }

    [JsonPropertyName("protein")]
    public double Protein { get; set; }

    [JsonPropertyName("carbohydrates")]
    public double Carbohydrates { get; set; }

    [JsonPropertyName("fat")]
    public double Fat { get; set; }

    [JsonPropertyName("mealType")]
    public string MealType { get; set; } = string.Empty;
}

public class RecipeExtractionsResponse
{
    [JsonPropertyName("recipes")]
    public List<RecipeExtractionResult> Recipes { get; set; } = new();
}
