using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;

namespace RecipesAIHelper.Services;

/// <summary>
/// Service for generating recipe images using OpenAI DALL-E 3
/// </summary>
public class OpenAIImageGenerationService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly JsonSerializerOptions _jsonOptions;

    public string ProviderName => $"OpenAI {_model}";

    public OpenAIImageGenerationService(string apiKey, string model = "dall-e-3")
    {
        _apiKey = apiKey;
        _model = model;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        // Add authorization header
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        // Polly retry policy: 3 attempts with exponential backoff
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"⚠️ Błąd OpenAI API (próba {retryCount}/3): {exception.GetType().Name}");
                    Console.WriteLine($"   Komunikat: {exception.Message}");
                    Console.WriteLine($"   Ponowienie za {timeSpan.TotalSeconds}s...");
                });

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        Console.WriteLine($"✅ OpenAIImageGenerationService zainicjalizowany ({_model})");
    }

    /// <summary>
    /// Generates an image for a recipe using DALL-E 3
    /// </summary>
    /// <param name="recipeName">Recipe name</param>
    /// <param name="recipeDescription">Recipe description</param>
    /// <returns>Base64 encoded image data (PNG format)</returns>
    public async Task<string?> GenerateRecipeImageAsync(string recipeName, string recipeDescription)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                Console.WriteLine($"🎨 Generowanie obrazu dla przepisu: {recipeName}");

                var prompt = BuildImagePrompt(recipeName, recipeDescription);
                Console.WriteLine($"   Prompt: {prompt}");

                // Build request following OpenAI Images API format
                // https://platform.openai.com/docs/api-reference/images/create
                // Note: DALL-E 2 doesn't support 'quality' parameter
                object requestBody;

                if (_model == "dall-e-2")
                {
                    // DALL-E 2: no quality parameter
                    requestBody = new
                    {
                        model = _model,
                        prompt = prompt,
                        n = 1,
                        size = "1024x1024",
                        response_format = "b64_json"
                    };
                }
                else
                {
                    // DALL-E 3, GPT Image models: support quality parameter
                    requestBody = new
                    {
                        model = _model,
                        prompt = prompt,
                        n = 1,
                        size = "1024x1024",
                        quality = "standard", // "standard" or "hd"
                        response_format = "b64_json"
                    };
                }

                var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/images/generations", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Błąd API: {response.StatusCode}");
                    Console.WriteLine($"   Odpowiedź: {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                // Debug: Save response
                var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dalle_response_debug.json");
                File.WriteAllText(debugPath, responseContent);
                Console.WriteLine($"🔍 DEBUG: Zapisano odpowiedź do: {debugPath}");

                var imageResponse = JsonSerializer.Deserialize<DallEApiResponse>(responseContent, _jsonOptions);

                if (imageResponse?.Data == null || imageResponse.Data.Count == 0)
                {
                    Console.WriteLine("❌ Brak wygenerowanych obrazów w odpowiedzi");
                    return null;
                }

                var imageData = imageResponse.Data[0];
                if (string.IsNullOrEmpty(imageData.B64Json))
                {
                    Console.WriteLine("❌ Brak danych obrazu w odpowiedzi");
                    return null;
                }

                Console.WriteLine($"✅ Obraz wygenerowany ({imageData.B64Json.Length} znaków base64)");
                if (!string.IsNullOrEmpty(imageData.RevisedPrompt))
                {
                    Console.WriteLine($"   Zmodyfikowany prompt: {imageData.RevisedPrompt}");
                }

                return imageData.B64Json;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd generowania obrazu: {ex.GetType().Name}");
                Console.WriteLine($"   Komunikat: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
                throw;
            }
        });
    }

    /// <summary>
    /// Builds the image generation prompt from recipe data
    /// </summary>
    private string BuildImagePrompt(string recipeName, string recipeDescription)
    {
        // Enhanced prompt for better food photography results
        return $"Wygeneruj realistyczne zdjęcie {recipeName}. {recipeDescription}.";
    }

    /// <summary>
    /// Saves base64 image to file and returns the file path
    /// </summary>
    public async Task<string> SaveImageToFileAsync(string base64Data, int recipeId, string recipeName)
    {
        try
        {
            // Create images directory if it doesn't exist
            var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "recipes");
            Directory.CreateDirectory(imagesDir);

            // Generate filename: recipe_{id}_{timestamp}.png (DALL-E outputs PNG)
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"recipe_{recipeId}_gen_{timestamp}.png";
            var filePath = Path.Combine(imagesDir, fileName);

            // Convert base64 to bytes and save
            var imageBytes = Convert.FromBase64String(base64Data);
            await File.WriteAllBytesAsync(filePath, imageBytes);

            Console.WriteLine($"💾 Obraz zapisany: {filePath} ({imageBytes.Length / 1024.0:F2} KB)");

            // Return relative path for web access
            return $"/images/recipes/{fileName}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd zapisu obrazu: {ex.Message}");
            throw;
        }
    }
}

// Response models for OpenAI Images API
internal class DallEApiResponse
{
    public long Created { get; set; }
    public List<DallEImageData>? Data { get; set; }
}

internal class DallEImageData
{
    public string? B64Json { get; set; }
    public string? Url { get; set; }
    public string? RevisedPrompt { get; set; }
}
