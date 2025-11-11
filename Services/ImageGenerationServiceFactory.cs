using RecipesAIHelper.Data;

namespace RecipesAIHelper.Services;

/// <summary>
/// Factory for creating image generation services based on database settings
/// </summary>
public class ImageGenerationServiceFactory
{
    private readonly RecipeDbContext _db;

    public ImageGenerationServiceFactory(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Creates the active image generation service based on Settings
    /// </summary>
    /// <returns>Image generation service instance or null if not configured</returns>
    public IImageGenerationService? CreateImageGenerationService()
    {
        try
        {
            // Get provider setting (default: OpenAI)
            var provider = _db.GetSetting("ImageGenerationProvider") ?? "OpenAI";

            Console.WriteLine($"🎨 Tworzenie serwisu generowania obrazów: {provider}");

            if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                var apiKey = _db.GetSetting("OpenAI_ApiKey");
                var model = _db.GetSetting("OpenAI_ImageModel") ?? "dall-e-3";

                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("❌ Brak klucza API OpenAI w ustawieniach");
                    return null;
                }

                return new OpenAIImageGenerationService(apiKey, model);
            }
            else if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) ||
                     provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                var apiKey = _db.GetSetting("Gemini_ApiKey");
                var model = _db.GetSetting("Gemini_ImageModel") ?? "imagen-4.0-ultra-generate-001";

                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("❌ Brak klucza API Gemini w ustawieniach");
                    return null;
                }

                return new GeminiImageGenerationService(apiKey, model);
            }
            else
            {
                Console.WriteLine($"❌ Nieznany provider generowania obrazów: {provider}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd tworzenia serwisu generowania obrazów: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the current image generation provider name
    /// </summary>
    public string GetCurrentProvider()
    {
        return _db.GetSetting("ImageGenerationProvider") ?? "OpenAI";
    }

    /// <summary>
    /// Sets the image generation provider
    /// </summary>
    public bool SetProvider(string provider)
    {
        if (!provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) &&
            !provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _db.UpsertSetting("ImageGenerationProvider", provider, "string",
            "Active image generation provider (OpenAI or Gemini)");
    }
}
