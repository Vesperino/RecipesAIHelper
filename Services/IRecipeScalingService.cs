using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Interface for recipe scaling services (Gemini, OpenAI, etc.)
/// </summary>
public interface IRecipeScalingService
{
    /// <summary>
    /// Scale recipe ingredients by a given factor using AI
    /// </summary>
    Task<List<string>> ScaleRecipeIngredientsAsync(
        Recipe baseRecipe,
        double scalingFactor,
        MealType mealType);
}
