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
            return provider.Name.ToLowerInvariant() switch
            {
                "openai" => new OpenAIService(provider.ApiKey, provider.Model),
                "gemini" or "google" => new GeminiService(provider.ApiKey, provider.Model),
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
