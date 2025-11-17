using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Interface for shopping list generation services (Gemini, OpenAI, etc.)
/// </summary>
public interface IShoppingListService
{
    /// <summary>
    /// Generates a shopping list from meal plan recipes (legacy single-shot approach)
    /// </summary>
    Task<ShoppingListResponse?> GenerateShoppingListAsync(List<Recipe> recipes);

    /// <summary>
    /// Generates a shopping list using day-by-day chunking approach
    /// </summary>
    /// <param name="recipesByDay">Recipes grouped by day number (day number → list of recipes)</param>
    /// <returns>Merged shopping list from all days</returns>
    Task<ShoppingListResponse?> GenerateShoppingListChunked(Dictionary<int, List<Recipe>> recipesByDay);
}
