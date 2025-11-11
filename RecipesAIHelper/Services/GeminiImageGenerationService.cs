using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;

namespace RecipesAIHelper.Services;

/// <summary>
/// Service for generating recipe images using Google Imagen 4.0 Ultra model
/// </summary>
public class GeminiImageGenerationService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly JsonSerializerOptions _jsonOptions;

    public string ProviderName => $"Google Imagen ({_model})";

    public GeminiImageGenerationService(string apiKey, string model = "imagen-4.0-ultra-generate-001")
    {
        _apiKey = apiKey;
        _model = model;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        // Polly retry policy: 3 attempts with exponential backoff
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"⚠️ Błąd Imagen API (próba {retryCount}/3): {exception.GetType().Name}");
                    Console.WriteLine($"   Komunikat: {exception.Message}");
                    Console.WriteLine($"   Ponowienie za {timeSpan.TotalSeconds}s...");
                });

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        Console.WriteLine($"✅ GeminiImageGenerationService zainicjalizowany ({_model})");
    }

    /// <summary>
    /// Generates an image for a recipe using Imagen 4.0 Ultra
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

                // Build request following Imagen Vertex AI format
                var requestBody = new
                {
                    instances = new[]
                    {
                        new { prompt }
                    },
                    parameters = new
                    {
                        outputMimeType = "image/jpeg",
                        sampleCount = 1,
                        personGeneration = "ALLOW_ADULT",
                        aspectRatio = "1:1",
                        imageSize = "1K" // 1024x1024
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:predict?key={_apiKey}";
                var response = await _httpClient.PostAsync(requestUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ Błąd API: {response.StatusCode}");
                    Console.WriteLine($"   Odpowiedź: {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                // Debug: Save response
                var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "imagen_response_debug.json");
                File.WriteAllText(debugPath, responseContent);
                Console.WriteLine($"🔍 DEBUG: Zapisano odpowiedź do: {debugPath}");

                var imageResponse = JsonSerializer.Deserialize<ImagenApiResponse>(responseContent, _jsonOptions);

                if (imageResponse?.Predictions == null || imageResponse.Predictions.Count == 0)
                {
                    Console.WriteLine("❌ Brak wygenerowanych obrazów w odpowiedzi");
                    return null;
                }

                var prediction = imageResponse.Predictions[0];
                if (string.IsNullOrEmpty(prediction.BytesBase64Encoded))
                {
                    Console.WriteLine("❌ Brak danych obrazu w odpowiedzi");
                    return null;
                }

                Console.WriteLine($"✅ Obraz wygenerowany ({prediction.BytesBase64Encoded.Length} znaków base64)");
                return prediction.BytesBase64Encoded;
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
        // Simple prompt combining name and description as requested by user
        return $"Wygeneruj zdjęcie: {recipeName}, {recipeDescription}";
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

            // Generate filename: recipe_{id}_{timestamp}.jpg (Imagen outputs JPEG)
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"recipe_{recipeId}_gen_{timestamp}.jpg";
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

// Response models for Imagen API (Vertex AI format)
internal class ImagenApiResponse
{
    public List<ImagenPrediction>? Predictions { get; set; }
}

internal class ImagenPrediction
{
    public string BytesBase64Encoded { get; set; } = string.Empty;
}
