using Mscc.GenerativeAI;
using RecipesAIHelper.Models;
using System.Text.Json;
using Polly;
using Polly.Retry;
using static RecipesAIHelper.Services.PdfImageService;
using static RecipesAIHelper.Services.PdfDirectService;

namespace RecipesAIHelper.Services;

public class GeminiService : IAIService
{
    private readonly GoogleAI _genAi;
    private readonly GenerativeModel _model;
    private readonly string _modelName;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly JsonSerializerOptions _jsonOptions;

    public GeminiService(string apiKey, string modelName = "gemini-2.5-flash")
    {
        _modelName = modelName;
        _genAi = new GoogleAI(apiKey);
        _model = _genAi.GenerativeModel(model: modelName);

        // Set timeout to 10 minutes (600s) to handle large PDFs
        _model.Timeout = TimeSpan.FromMinutes(10);

        // Polly retry policy: 3 attempts with 2s, 4s, 8s delays
        _retryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"⚠️ Błąd Gemini API (próba {retryCount}/3): {exception.GetType().Name}");
                    Console.WriteLine($"   Komunikat: {exception.Message}");
                    if (exception.InnerException != null)
                    {
                        Console.WriteLine($"   Inner: {exception.InnerException.GetType().Name} - {exception.InnerException.Message}");
                    }
                    Console.WriteLine($"   Ponowienie za {timeSpan.TotalSeconds}s...");
                });

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        Console.WriteLine($"✅ Gemini Service zainicjalizowany z modelem: {_modelName}");
        Console.WriteLine($"   Max pages per chunk: 100 stron (1M token context)");
        Console.WriteLine($"   Direct PDF support: enabled (inline_data with base64)");
        Console.WriteLine($"   Timeout: 10 minut (600s)");
    }

    // ==================== IAIService Implementation ====================

    public string GetProviderName() => "Gemini";

    public string GetModelName() => _modelName;

    public int GetMaxPagesPerChunk() => 100; // Gemini 2.5 Flash supports ~1500 pages total

    public bool SupportsDirectPDF() => true; // Gemini supports PDF via inline_data with base64

    // ==================== Recipe Extraction Methods ====================

    /// <summary>
    /// Extracts recipes from PDF file directly using Gemini inline_data
    /// </summary>
    public async Task<List<RecipeExtractionResult>> ExtractRecipesFromPdf(
        PdfFileChunk pdfChunk,
        List<Recipe>? recentRecipes = null,
        IProgress<StreamingProgress>? progress = null)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                Console.WriteLine($"📤 Wysyłanie PDF do Gemini: {pdfChunk.FileName} ({pdfChunk.FileSize / 1024.0 / 1024.0:F2} MB)...");

                // Build prompt using shared PromptBuilder
                var systemPrompt = PromptBuilder.BuildPdfExtractionPrompt(recentRecipes);
                var userPrompt = PromptBuilder.BuildPdfUserPrompt(pdfChunk.FileName);
                var promptText = $"{systemPrompt}\n\n{userPrompt}";

                // Build multimodal request with text + PDF
                var parts = new List<IPart>();

                // Add text prompt
                parts.Add(new TextData { Text = promptText });

                // Add PDF as inline data (raw base64, not data URI)
                // Gemini API expects inline_data format: {"mime_type": "application/pdf", "data": "base64..."}
                parts.Add(new InlineData
                {
                    MimeType = "application/pdf",
                    Data = pdfChunk.Base64Data
                });

                Console.WriteLine($"✅ Przygotowano request z PDF ({pdfChunk.FileSize / 1024.0:F0} KB)");

                var content = new Content
                {
                    Role = Role.User,
                    Parts = parts
                };

                var request = new GenerateContentRequest
                {
                    Contents = new[] { content }.ToList()
                };

                var startTime = DateTime.Now;
                Console.WriteLine($"📡 Używam streaming API dla lepszej obsługi dużych plików...");

                // Use streaming API to keep connection alive during processing
                var responseBuilder = new System.Text.StringBuilder();
                await foreach (var chunk in _model.GenerateContentStream(request))
                {
                    var chunkText = chunk?.Text ?? "";
                    responseBuilder.Append(chunkText);

                    var currentElapsed = (DateTime.Now - startTime).TotalSeconds;

                    // Report progress
                    progress?.Report(new StreamingProgress
                    {
                        BytesReceived = responseBuilder.Length,
                        Message = $"Otrzymano {responseBuilder.Length / 1024.0:F1} KB...",
                        ElapsedSeconds = currentElapsed
                    });

                    // Show progress every ~10KB
                    if (responseBuilder.Length % 10000 < chunkText.Length)
                    {
                        Console.WriteLine($"   📊 Odebrano {responseBuilder.Length / 1024.0:F1} KB... ({currentElapsed:F1}s)");
                    }
                }

                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                var responseContent = responseBuilder.ToString().Trim();
                Console.WriteLine($"📥 Otrzymano odpowiedź ({responseContent.Length} znaków, {elapsed:F1}s)");

                // Debug: Save response to file
                var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gemini_response_debug.json");
                File.WriteAllText(debugPath, responseContent);
                Console.WriteLine($"🔍 DEBUG: Zapisano odpowiedź Gemini do: {debugPath}");

                // Parse JSON response
                var recipes = ParseJsonResponse(responseContent);

                Console.WriteLine($"✅ Wyekstraktowano {recipes.Count} przepisów z PDF");

                // Debug: Log nutritionVariants info
                foreach (var recipe in recipes)
                {
                    var variantsCount = recipe.NutritionVariants?.Count ?? 0;
                    Console.WriteLine($"   📊 {recipe.Name}: nutritionVariants = {(recipe.NutritionVariants == null ? "NULL" : $"{variantsCount} wariantów")}");
                    if (recipe.NutritionVariants != null)
                    {
                        foreach (var variant in recipe.NutritionVariants)
                        {
                            Console.WriteLine($"      - {variant.Label}: {variant.Calories} kcal, B:{variant.Protein}g, W:{variant.Carbohydrates}g, T:{variant.Fat}g");
                        }
                    }
                }

                return recipes;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"❌ Błąd parsowania JSON: {ex.Message}");
                Console.WriteLine($"   Sprawdź format odpowiedzi Gemini");
                return new List<RecipeExtractionResult>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd przetwarzania PDF: {ex.GetType().Name}");
                Console.WriteLine($"   Komunikat: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"   Stack trace (first 500 chars): {ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))}");
                throw;
            }
        });
    }

    /// <summary>
    /// Extracts recipes from PDF page images using Gemini Vision API
    /// </summary>
    public async Task<List<RecipeExtractionResult>> ExtractRecipesFromImages(
        PdfImageChunk imageChunk,
        List<Recipe>? recentRecipes = null,
        List<string>? alreadyProcessedInPdf = null,
        IProgress<StreamingProgress>? progress = null)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                var totalImageSizeMB = imageChunk.Pages.Sum(p => p.ImageData.Length) / 1024.0 / 1024.0;
                Console.WriteLine($"📤 Wysyłanie {imageChunk.Pages.Count} obrazów do Gemini (chunk {imageChunk.ChunkNumber}, strony {imageChunk.StartPage}-{imageChunk.EndPage})...");
                Console.WriteLine($"   Rozmiar danych: {totalImageSizeMB:F2} MB");

                foreach (var page in imageChunk.Pages)
                {
                    var pageSizeMB = page.ImageData.Length / 1024.0 / 1024.0;
                    Console.WriteLine($"   - Strona {page.PageNumber}: {pageSizeMB:F2} MB");
                }

                if (totalImageSizeMB > 20)
                {
                    Console.WriteLine($"   ⚠️  UWAGA: Duży rozmiar danych ({totalImageSizeMB:F2} MB) - przetwarzanie może zająć więcej czasu");
                }

                // Build prompt using shared PromptBuilder
                var systemPrompt = PromptBuilder.BuildImageExtractionPrompt(recentRecipes, alreadyProcessedInPdf);
                var userPrompt = PromptBuilder.BuildImageUserPrompt(imageChunk.StartPage, imageChunk.EndPage, imageChunk.Pages.Count);
                var promptText = $"{systemPrompt}\n\n{userPrompt}";

                // Build multimodal request with text + images
                var parts = new List<IPart>();

                // Add text prompt
                parts.Add(new TextData { Text = promptText });

                // Add all images
                foreach (var page in imageChunk.Pages)
                {
                    var imageBase64 = Convert.ToBase64String(page.ImageData);
                    parts.Add(new InlineData
                    {
                        MimeType = "image/png",
                        Data = imageBase64
                    });
                    Console.WriteLine($"   📷 Dodano obraz strony {page.PageNumber} ({page.ImageData.Length / 1024.0:F0} KB)");
                }

                var content = new Content
                {
                    Role = Role.User,
                    Parts = parts
                };

                var request = new GenerateContentRequest
                {
                    Contents = new[] { content }.ToList()
                };

                Console.WriteLine($"✅ Przygotowano request z {parts.Count} częściami (1 prompt + {imageChunk.Pages.Count} obrazów)");

                var startTime = DateTime.Now;
                Console.WriteLine($"📡 Używam streaming API dla lepszej obsługi dużych plików...");

                // Use streaming API to keep connection alive during processing
                var responseBuilder = new System.Text.StringBuilder();
                await foreach (var chunk in _model.GenerateContentStream(request))
                {
                    var chunkText = chunk?.Text ?? "";
                    responseBuilder.Append(chunkText);

                    var currentElapsed = (DateTime.Now - startTime).TotalSeconds;

                    // Report progress
                    progress?.Report(new StreamingProgress
                    {
                        BytesReceived = responseBuilder.Length,
                        Message = $"Otrzymano {responseBuilder.Length / 1024.0:F1} KB...",
                        ElapsedSeconds = currentElapsed
                    });

                    // Show progress every ~10KB
                    if (responseBuilder.Length % 10000 < chunkText.Length)
                    {
                        Console.WriteLine($"   📊 Odebrano {responseBuilder.Length / 1024.0:F1} KB... ({currentElapsed:F1}s)");
                    }
                }

                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                var responseContent = responseBuilder.ToString().Trim();
                Console.WriteLine($"📥 Otrzymano odpowiedź ({responseContent.Length} znaków, {elapsed:F1}s)");

                // Debug: Save response to file
                var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"gemini_response_chunk{imageChunk.ChunkNumber}_debug.json");
                File.WriteAllText(debugPath, responseContent);
                Console.WriteLine($"🔍 DEBUG: Zapisano odpowiedź Gemini do: {debugPath}");

                // Parse JSON response
                var recipes = ParseJsonResponse(responseContent);

                Console.WriteLine($"✅ Wyekstraktowano {recipes.Count} przepisów z chunka {imageChunk.ChunkNumber}");

                // Debug: Log nutritionVariants info
                foreach (var recipe in recipes)
                {
                    var variantsCount = recipe.NutritionVariants?.Count ?? 0;
                    Console.WriteLine($"   📊 {recipe.Name}: nutritionVariants = {(recipe.NutritionVariants == null ? "NULL" : $"{variantsCount} wariantów")}");
                    if (recipe.NutritionVariants != null)
                    {
                        foreach (var variant in recipe.NutritionVariants)
                        {
                            Console.WriteLine($"      - {variant.Label}: {variant.Calories} kcal, B:{variant.Protein}g, W:{variant.Carbohydrates}g, T:{variant.Fat}g");
                        }
                    }
                }

                return recipes;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"❌ Błąd parsowania JSON w chunku {imageChunk.ChunkNumber}: {ex.Message}");
                Console.WriteLine($"   Sprawdź format odpowiedzi Gemini");
                return new List<RecipeExtractionResult>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd przetwarzania chunka {imageChunk.ChunkNumber}: {ex.GetType().Name}");
                Console.WriteLine($"   Komunikat: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                throw;
            }
        });
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Parses JSON response from Gemini
    /// </summary>
    private List<RecipeExtractionResult> ParseJsonResponse(string responseContent)
    {
        // Remove markdown code blocks if present
        responseContent = responseContent
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        try
        {
            var response = JsonSerializer.Deserialize<RecipeExtractionsResponse>(responseContent, _jsonOptions);

            if (response?.Recipes == null || response.Recipes.Count == 0)
            {
                Console.WriteLine("⚠️ Brak przepisów w odpowiedzi JSON");
                return new List<RecipeExtractionResult>();
            }

            // Validate each recipe
            var validRecipes = response.Recipes
                .Where(r => !string.IsNullOrWhiteSpace(r.Name) && r.Ingredients.Count > 0)
                .ToList();

            if (validRecipes.Count < response.Recipes.Count)
            {
                Console.WriteLine($"⚠️ Odrzucono {response.Recipes.Count - validRecipes.Count} niepełnych przepisów");
            }

            return validRecipes;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"❌ Błąd deserializacji JSON: {ex.Message}");
            Console.WriteLine($"📄 Odpowiedź:\n{responseContent.Substring(0, Math.Min(500, responseContent.Length))}...");
            throw;
        }
    }
}
