using System.Text.Json;
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
    [JsonConverter(typeof(FlexibleIntConverter))]
    public int Calories { get; set; }

    [JsonPropertyName("protein")]
    [JsonConverter(typeof(FlexibleDoubleConverter))]
    public double Protein { get; set; }

    [JsonPropertyName("carbohydrates")]
    [JsonConverter(typeof(FlexibleDoubleConverter))]
    public double Carbohydrates { get; set; }

    [JsonPropertyName("fat")]
    [JsonConverter(typeof(FlexibleDoubleConverter))]
    public double Fat { get; set; }

    [JsonPropertyName("mealType")]
    public string MealType { get; set; } = string.Empty;

    [JsonPropertyName("servings")]
    [JsonConverter(typeof(FlexibleNullableIntConverter))]
    public int? Servings { get; set; }

    [JsonPropertyName("nutritionVariants")]
    public List<NutritionVariant>? NutritionVariants { get; set; }
}

public class RecipeExtractionsResponse
{
    [JsonPropertyName("recipes")]
    public List<RecipeExtractionResult> Recipes { get; set; } = new();
}

// ==================== Custom JSON Converters ====================

/// <summary>
/// Flexible converter that accepts both int and string for int properties
/// </summary>
public class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetInt32();

            case JsonTokenType.String:
                var stringValue = reader.GetString();
                if (string.IsNullOrWhiteSpace(stringValue))
                    return 0;

                // Try parse, return 0 if failed
                if (int.TryParse(stringValue.Trim(), out int result))
                    return result;

                // Try parse as double and round (e.g., "794.5" -> 794)
                if (double.TryParse(stringValue.Trim(), out double doubleResult))
                    return (int)Math.Round(doubleResult);

                return 0;

            default:
                return 0;
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// Flexible converter for nullable int
/// </summary>
public class FlexibleNullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.Number:
                return reader.GetInt32();

            case JsonTokenType.String:
                var stringValue = reader.GetString();
                if (string.IsNullOrWhiteSpace(stringValue))
                    return null;

                if (int.TryParse(stringValue.Trim(), out int result))
                    return result;

                if (double.TryParse(stringValue.Trim(), out double doubleResult))
                    return (int)Math.Round(doubleResult);

                return null;

            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// Flexible converter that accepts both double and string for double properties
/// </summary>
public class FlexibleDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.GetDouble();

            case JsonTokenType.String:
                var stringValue = reader.GetString();
                if (string.IsNullOrWhiteSpace(stringValue))
                    return 0.0;

                // Try parse, return 0.0 if failed
                if (double.TryParse(stringValue.Trim(), out double result))
                    return result;

                return 0.0;

            default:
                return 0.0;
        }
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
