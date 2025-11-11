using Microsoft.AspNetCore.Mvc;
using RecipesAIHelper.Data;
using RecipesAIHelper.Services;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageSettingsController : ControllerBase
{
    private readonly RecipeDbContext _db;

    public ImageSettingsController(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get image generation settings
    /// GET /api/imagesettings
    /// </summary>
    [HttpGet]
    public ActionResult GetImageSettings()
    {
        try
        {
            var settings = new
            {
                provider = _db.GetSetting("ImageGenerationProvider") ?? "OpenAI",
                openAI = new
                {
                    apiKey = MaskApiKey(_db.GetSetting("OpenAI_ApiKey")),
                    model = _db.GetSetting("OpenAI_ImageModel") ?? "dall-e-3"
                },
                gemini = new
                {
                    apiKey = MaskApiKey(_db.GetSetting("Gemini_ApiKey")),
                    model = _db.GetSetting("Gemini_ImageModel") ?? "imagen-4.0-ultra-generate-001"
                },
                availableProviders = new[] { "OpenAI", "Gemini" }
            };

            return Ok(settings);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update image generation settings
    /// PUT /api/imagesettings
    /// </summary>
    [HttpPut]
    public ActionResult UpdateImageSettings([FromBody] ImageSettingsUpdate update)
    {
        try
        {
            var updated = false;

            // Update provider
            if (!string.IsNullOrEmpty(update.Provider))
            {
                if (update.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) ||
                    update.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    _db.UpsertSetting("ImageGenerationProvider", update.Provider, "string",
                        "Provider generowania obrazów (OpenAI lub Gemini)");
                    updated = true;
                    Console.WriteLine($"✅ Zmieniono providera generowania obrazów na: {update.Provider}");
                }
            }

            // Update OpenAI settings
            if (update.OpenAI != null)
            {
                if (!string.IsNullOrEmpty(update.OpenAI.ApiKey) && update.OpenAI.ApiKey != "***")
                {
                    _db.UpsertSetting("OpenAI_ApiKey", update.OpenAI.ApiKey, "string",
                        "Klucz API OpenAI dla generowania obrazów");
                    updated = true;
                    Console.WriteLine("✅ Zaktualizowano klucz API OpenAI");
                }

                if (!string.IsNullOrEmpty(update.OpenAI.Model))
                {
                    _db.UpsertSetting("OpenAI_ImageModel", update.OpenAI.Model, "string",
                        "Model OpenAI do generowania obrazów");
                    updated = true;
                    Console.WriteLine($"✅ Zmieniono model OpenAI na: {update.OpenAI.Model}");
                }
            }

            // Update Gemini settings
            if (update.Gemini != null)
            {
                if (!string.IsNullOrEmpty(update.Gemini.ApiKey) && update.Gemini.ApiKey != "***")
                {
                    _db.UpsertSetting("Gemini_ApiKey", update.Gemini.ApiKey, "string",
                        "Klucz API Google Gemini dla generowania obrazów");
                    updated = true;
                    Console.WriteLine("✅ Zaktualizowano klucz API Gemini");
                }

                if (!string.IsNullOrEmpty(update.Gemini.Model))
                {
                    _db.UpsertSetting("Gemini_ImageModel", update.Gemini.Model, "string",
                        "Model Gemini do generowania obrazów");
                    updated = true;
                    Console.WriteLine($"✅ Zmieniono model Gemini na: {update.Gemini.Model}");
                }
            }

            if (updated)
            {
                return Ok(new
                {
                    message = "Image settings updated successfully",
                    currentProvider = _db.GetSetting("ImageGenerationProvider") ?? "OpenAI"
                });
            }
            else
            {
                return BadRequest(new { error = "No valid settings provided" });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd aktualizacji ustawień obrazów: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Switch image generation provider
    /// POST /api/imagesettings/switch-provider
    /// Body: { "provider": "OpenAI" } or { "provider": "Gemini" }
    /// </summary>
    [HttpPost("switch-provider")]
    public ActionResult SwitchProvider([FromBody] ProviderSwitch request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Provider))
            {
                return BadRequest(new { error = "Provider name is required" });
            }

            var factory = new ImageGenerationServiceFactory(_db);
            if (!factory.SetProvider(request.Provider))
            {
                return BadRequest(new
                {
                    error = $"Invalid provider: {request.Provider}",
                    hint = "Valid providers are: OpenAI, Gemini"
                });
            }

            Console.WriteLine($"✅ Przełączono na providera: {request.Provider}");

            return Ok(new
            {
                message = $"Switched to {request.Provider}",
                provider = request.Provider
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test image generation with current settings
    /// POST /api/imagesettings/test
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult> TestImageGeneration()
    {
        try
        {
            var factory = new ImageGenerationServiceFactory(_db);
            var service = factory.CreateImageGenerationService();

            if (service == null)
            {
                return BadRequest(new
                {
                    error = "Image generation service not configured",
                    hint = "Please configure API keys first"
                });
            }

            Console.WriteLine($"🧪 Testowanie generowania obrazu z providerem: {service.ProviderName}");

            // Test with simple prompt
            var base64Image = await service.GenerateRecipeImageAsync(
                "Test Recipe",
                "Simple test image for configuration validation"
            );

            if (base64Image == null)
            {
                return StatusCode(500, new
                {
                    error = "Failed to generate test image",
                    provider = service.ProviderName
                });
            }

            Console.WriteLine($"✅ Test zakończony sukcesem ({base64Image.Length} znaków base64)");

            return Ok(new
            {
                message = "Test successful",
                provider = service.ProviderName,
                imageSizeBytes = base64Image.Length
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd testu: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
            return "***";

        return apiKey.Substring(0, 4) + "***" + apiKey.Substring(apiKey.Length - 4);
    }
}

// Request models
public class ImageSettingsUpdate
{
    public string? Provider { get; set; }
    public OpenAIImageSettings? OpenAI { get; set; }
    public GeminiImageSettings? Gemini { get; set; }
}

public class OpenAIImageSettings
{
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
}

public class GeminiImageSettings
{
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
}

public class ProviderSwitch
{
    public string Provider { get; set; } = string.Empty;
}
