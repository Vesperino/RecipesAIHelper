using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Interface for shopping list generation services (Gemini, OpenAI, etc.)
/// </summary>
public interface IShoppingListService
{
    /// <summary>
    /// Generates a shopping list from meal plan recipes
    /// </summary>
    Task<ShoppingListResponse?> GenerateShoppingListAsync(List<Recipe> recipes);
}
