using RecipesAIHelper.Data;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Factory for creating AI service instances based on provider configuration
/// </summary>
public class AIServiceFactory
{
    private readonly RecipeDbContext _db;

    public AIServiceFactory(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Creates an AI service instance for the currently active provider
    /// Returns null if no active provider is configured
    /// </summary>
    public IAIService? CreateActiveService()
    {
        var activeProvider = _db.GetActiveAIProvider();

        if (activeProvider == null)
        {
            Console.WriteLine("⚠️ Brak aktywnego providera AI w bazie danych");
            return null;
        }

        return CreateService(activeProvider);
    }

    /// <summary>
    /// Creates an AI service instance for a specific provider
    /// </summary>
    public IAIService? CreateService(AIProvider provider)
    {
        Console.WriteLine($"🔧 Tworzenie serwisu AI: {provider.Name} ({provider.Model})");

        try
        {
            // Get API key from Settings based on provider name
            string? apiKey = null;
            var providerNameLower = provider.Name.ToLowerInvariant();

            if (providerNameLower == "openai")
            {
                apiKey = _db.GetSetting("OpenAI_ApiKey");
            }
            else if (providerNameLower == "gemini" || providerNameLower == "google")
            {
                apiKey = _db.GetSetting("Gemini_ApiKey");
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine($"❌ Brak klucza API dla providera {provider.Name} w Settings");
                Console.WriteLine($"   Skonfiguruj klucz API w zakładce Ustawienia");
                return null;
            }

            return providerNameLower switch
            {
                "openai" => new OpenAIService(apiKey, provider.Model),
                "gemini" or "google" => new GeminiService(apiKey, provider.Model),
                _ => throw new NotSupportedException($"Nieobsługiwany provider: {provider.Name}")
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd tworzenia serwisu {provider.Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates an AI service instance for a specific provider ID
    /// </summary>
    public IAIService? CreateService(int providerId)
    {
        var provider = _db.GetAIProvider(providerId);

        if (provider == null)
        {
            Console.WriteLine($"⚠️ Provider o ID {providerId} nie istnieje w bazie");
            return null;
        }

        return CreateService(provider);
    }

    /// <summary>
    /// Gets the active provider info without creating a service instance
    /// </summary>
    public AIProvider? GetActiveProvider()
    {
        return _db.GetActiveAIProvider();
    }

    /// <summary>
    /// Lists all available providers
    /// </summary>
    public List<AIProvider> GetAllProviders()
    {
        return _db.GetAllAIProviders();
    }
}
