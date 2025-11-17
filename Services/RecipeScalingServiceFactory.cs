using RecipesAIHelper.Data;

namespace RecipesAIHelper.Services;

/// <summary>
/// Factory for creating recipe scaling service instances based on provider settings
/// </summary>
public class RecipeScalingServiceFactory
{
    private readonly RecipeDbContext _db;

    public RecipeScalingServiceFactory(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Create scaling service based on database settings
    /// </summary>
    /// <returns>IRecipeScalingService instance or null if provider not configured</returns>
    public IRecipeScalingService? CreateScalingService()
    {
        var provider = _db.GetSetting("RecipeScaling_Provider") ?? "Gemini";
        var model = _db.GetSetting("RecipeScaling_Model") ?? "gemini-2.5-flash";

        Console.WriteLine($"🏭 RecipeScalingServiceFactory: Provider={provider}, Model={model}");

        var providerLower = provider.ToLowerInvariant();

        if (providerLower == "openai")
        {
            var apiKey = _db.GetSetting("OpenAI_ApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ RecipeScalingServiceFactory: Brak klucza API dla OpenAI");
                return null;
            }

            return new OpenAIRecipeScalingService(apiKey, model);
        }
        else if (providerLower == "gemini" || providerLower == "google")
        {
            var apiKey = _db.GetSetting("Gemini_ApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ RecipeScalingServiceFactory: Brak klucza API dla Gemini");
                return null;
            }

            return new GeminiRecipeScalingService(apiKey, model);
        }
        else
        {
            Console.WriteLine($"❌ RecipeScalingServiceFactory: Nieznany provider: {provider}");
            return null;
        }
    }

    /// <summary>
    /// Create scaling service with explicit provider and model
    /// </summary>
    public IRecipeScalingService? CreateScalingService(string provider, string apiKey, string model)
    {
        var providerLower = provider.ToLowerInvariant();

        if (providerLower == "openai")
        {
            return new OpenAIRecipeScalingService(apiKey, model);
        }
        else if (providerLower == "gemini" || providerLower == "google")
        {
            return new GeminiRecipeScalingService(apiKey, model);
        }
        else
        {
            Console.WriteLine($"❌ RecipeScalingServiceFactory: Nieznany provider: {provider}");
            return null;
        }
    }
}
