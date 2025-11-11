namespace RecipesAIHelper.Services;

/// <summary>
/// Interface for image generation services (DALL-E, Imagen, etc.)
/// </summary>
public interface IImageGenerationService
{
    /// <summary>
    /// Generates an image for a recipe
    /// </summary>
    /// <param name="recipeName">Recipe name</param>
    /// <param name="recipeDescription">Recipe description</param>
    /// <returns>Base64 encoded image data</returns>
    Task<string?> GenerateRecipeImageAsync(string recipeName, string recipeDescription);

    /// <summary>
    /// Saves base64 image to file and returns the file path
    /// </summary>
    Task<string> SaveImageToFileAsync(string base64Data, int recipeId, string recipeName);

    /// <summary>
    /// Gets the provider name (e.g., "OpenAI DALL-E 3", "Google Imagen")
    /// </summary>
    string ProviderName { get; }
}
