using System.Text.Json.Serialization;
using RecipesAIHelper.Services;

namespace RecipesAIHelper.Models;

/// <summary>
/// Meal plan (e.g., "Plan na styczeń", "Plan świąteczny")
/// </summary>
public class MealPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties (not stored in DB)
    public List<MealPlanDay>? Days { get; set; }
    public List<MealPlanPerson>? Persons { get; set; }
}

/// <summary>
/// Specific day in a meal plan
/// </summary>
public class MealPlanDay
{
    public int Id { get; set; }
    public int MealPlanId { get; set; }

    /// <summary>
    /// Day of week (0 = Monday, 6 = Sunday)
    /// </summary>
    public int DayOfWeek { get; set; }

    /// <summary>
    /// Specific date for this day
    /// </summary>
    public DateTime Date { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [JsonIgnore]
    public MealPlan? MealPlan { get; set; }

    public List<MealPlanEntry>? Entries { get; set; }
}

/// <summary>
/// Recipe assigned to a specific day in the meal plan
/// </summary>
public class MealPlanEntry
{
    public int Id { get; set; }
    public int MealPlanDayId { get; set; }
    public int RecipeId { get; set; }
    public MealType MealType { get; set; }

    /// <summary>
    /// Order within the day (for custom sorting)
    /// </summary>
    public int Order { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [JsonIgnore]
    public MealPlanDay? MealPlanDay { get; set; }

    public Recipe? Recipe { get; set; }

    // Scaled recipes for multi-person meal plans
    public List<MealPlanRecipe>? ScaledRecipes { get; set; }
}

/// <summary>
/// Day of week names (Polish)
/// </summary>
public static class DayOfWeekNames
{
    public static readonly Dictionary<int, string> Polish = new()
    {
        { 0, "Poniedziałek" },
        { 1, "Wtorek" },
        { 2, "Środa" },
        { 3, "Czwartek" },
        { 4, "Piątek" },
        { 5, "Sobota" },
        { 6, "Niedziela" }
    };
}

/// <summary>
/// Shopping list for a meal plan
/// </summary>
public class ShoppingList
{
    public int Id { get; set; }
    public int MealPlanId { get; set; }
    public DateTime GeneratedAt { get; set; }

    // Stored as JSON in database
    [JsonIgnore]
    public string ItemsJson { get; set; } = string.Empty;

    // Computed property
    public List<ShoppingListItem>? Items
    {
        get => string.IsNullOrEmpty(ItemsJson) ? null : System.Text.Json.JsonSerializer.Deserialize<List<ShoppingListItem>>(ItemsJson);
        set => ItemsJson = value != null ? System.Text.Json.JsonSerializer.Serialize(value) : string.Empty;
    }
}

/// <summary>
/// Person in a meal plan with their calorie target
/// </summary>
public class MealPlanPerson
{
    public int Id { get; set; }
    public int MealPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TargetCalories { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [JsonIgnore]
    public MealPlan? MealPlan { get; set; }
}

/// <summary>
/// Scaled recipe for a specific person in a meal plan
/// </summary>
public class MealPlanRecipe
{
    public int Id { get; set; }
    public int MealPlanEntryId { get; set; }
    public int PersonId { get; set; }
    public int BaseRecipeId { get; set; }

    /// <summary>
    /// Scaling factor applied to the recipe (e.g., 1.2 = +20%)
    /// </summary>
    public double ScalingFactor { get; set; }

    // Scaled ingredient list (stored as JSON in database)
    [JsonIgnore]
    public string ScaledIngredientsJson { get; set; } = string.Empty;

    // Computed property for scaled ingredients
    public List<string>? ScaledIngredients
    {
        get => string.IsNullOrEmpty(ScaledIngredientsJson) ? null : System.Text.Json.JsonSerializer.Deserialize<List<string>>(ScaledIngredientsJson);
        set => ScaledIngredientsJson = value != null ? System.Text.Json.JsonSerializer.Serialize(value) : string.Empty;
    }

    // Scaled nutrition values
    public int ScaledCalories { get; set; }
    public double ScaledProtein { get; set; }
    public double ScaledCarbs { get; set; }
    public double ScaledFat { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [JsonIgnore]
    public MealPlanEntry? MealPlanEntry { get; set; }

    public MealPlanPerson? Person { get; set; }
    public Recipe? BaseRecipe { get; set; }
}
