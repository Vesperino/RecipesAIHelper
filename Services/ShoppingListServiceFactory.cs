using RecipesAIHelper.Data;

namespace RecipesAIHelper.Services;

/// <summary>
/// Factory for creating shopping list service instances based on provider settings
/// </summary>
public class ShoppingListServiceFactory
{
    private readonly RecipeDbContext _db;

    public ShoppingListServiceFactory(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Create shopping list service based on database settings
    /// </summary>
    /// <returns>IShoppingListService instance or null if provider not configured</returns>
    public IShoppingListService? CreateShoppingListService()
    {
        var provider = _db.GetSetting("ShoppingList_Provider") ?? "Gemini";
        var model = _db.GetSetting("ShoppingList_Model") ?? "gemini-2.5-flash";

        Console.WriteLine($"🏭 ShoppingListServiceFactory: Provider={provider}, Model={model}");

        var providerLower = provider.ToLowerInvariant();

        if (providerLower == "openai")
        {
            var apiKey = _db.GetSetting("OpenAI_ApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ ShoppingListServiceFactory: Brak klucza API dla OpenAI");
                return null;
            }

            return new OpenAIShoppingListService(apiKey, model);
        }
        else if (providerLower == "gemini" || providerLower == "google")
        {
            var apiKey = _db.GetSetting("Gemini_ApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ ShoppingListServiceFactory: Brak klucza API dla Gemini");
                return null;
            }

            return new GeminiShoppingListService(apiKey, model);
        }
        else
        {
            Console.WriteLine($"❌ ShoppingListServiceFactory: Nieznany provider: {provider}");
            return null;
        }
    }

    /// <summary>
    /// Create shopping list service with explicit provider and model
    /// </summary>
    public IShoppingListService? CreateShoppingListService(string provider, string apiKey, string model)
    {
        var providerLower = provider.ToLowerInvariant();

        if (providerLower == "openai")
        {
            return new OpenAIShoppingListService(apiKey, model);
        }
        else if (providerLower == "gemini" || providerLower == "google")
        {
            return new GeminiShoppingListService(apiKey, model);
        }
        else
        {
            Console.WriteLine($"❌ ShoppingListServiceFactory: Nieznany provider: {provider}");
            return null;
        }
    }
}
