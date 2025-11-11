using OpenAI;
using OpenAI.Chat;
using RecipesAIHelper.Models;
using System.ClientModel;
using System.Text.Json;
using Polly;
using Polly.Retry;
using static RecipesAIHelper.Services.PdfImageService;
using static RecipesAIHelper.Services.PdfDirectService;

namespace RecipesAIHelper.Services;

public class OpenAIService : IAIService
{
    private readonly ChatClient _chatClient;
    private readonly string _modelName;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAIService(string apiKey, string modelName = "gpt-5-mini-2025-08-07")
    {
        _modelName = modelName;

        // Create client with extended timeout (default is 100s, we need 5 minutes)
        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromMinutes(5)
        };
        _chatClient = new ChatClient(modelName, new ApiKeyCredential(apiKey), clientOptions);

        // Polly retry policy: 3 próby z 2s, 4s, 8s opóźnieniami
        _retryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException) // Don't retry timeouts
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"⚠️ Błąd API (próba {retryCount}/3): {exception.GetType().Name}");
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

        Console.WriteLine($"✅ OpenAI Service zainicjalizowany z modelem: {_modelName}");
        Console.WriteLine($"   NetworkTimeout: 5 minut (300s)");
    }

    // ==================== IAIService Implementation ====================

    public string GetProviderName() => "OpenAI";

    public string GetModelName() => _modelName;

    public int GetMaxPagesPerChunk() => 3; // OpenAI Vision works best with 3 pages

    public bool SupportsDirectPDF() => true; // OpenAI supports PDF via ChatMessageContentPart.CreateImagePart

    // ==================== Recipe Extraction Methods ====================

    /// <summary>
    /// Extracts recipes from PDF file directly (sends PDF as Base64 to OpenAI)
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
                // Build prompt using shared PromptBuilder
                var systemPrompt = PromptBuilder.BuildPdfExtractionPrompt(recentRecipes);
                var userPrompt = PromptBuilder.BuildPdfUserPrompt(pdfChunk.FileName);

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(new List<ChatMessageContentPart>
                    {
                        ChatMessageContentPart.CreateTextPart(userPrompt),
                        ChatMessageContentPart.CreateImagePart(
                            BinaryData.FromBytes(Convert.FromBase64String(pdfChunk.Base64Data)),
                            "application/pdf")
                    })
                };

                Console.WriteLine($"📤 Wysyłanie PDF do OpenAI: {pdfChunk.FileName} ({pdfChunk.FileSize / 1024.0 / 1024.0:F2} MB)...");

                var startTime = DateTime.Now;
                string responseContent;

                // Use streaming if progress callback is provided
                if (progress != null)
                {
                    Console.WriteLine($"📡 Używam streaming API dla lepszej obsługi dużych plików...");
                    var responseBuilder = new System.Text.StringBuilder();

                    await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages))
                    {
                        foreach (var contentPart in update.ContentUpdate)
                        {
                            responseBuilder.Append(contentPart.Text);
                        }

                        var currentElapsed = (DateTime.Now - startTime).TotalSeconds;

                        // Report progress
                        progress.Report(new StreamingProgress
                        {
                            BytesReceived = responseBuilder.Length,
                            Message = $"Otrzymano {responseBuilder.Length / 1024.0:F1} KB...",
                            ElapsedSeconds = currentElapsed
                        });

                        // Show progress every ~10KB
                        if (responseBuilder.Length % 10000 < 100)
                        {
                            Console.WriteLine($"   📊 Odebrano {responseBuilder.Length / 1024.0:F1} KB... ({currentElapsed:F1}s)");
                        }
                    }

                    responseContent = responseBuilder.ToString().Trim();
                }
                else
                {
                    var completion = await _chatClient.CompleteChatAsync(messages);
                    responseContent = completion.Value.Content[0].Text.Trim();
                }

                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine($"📥 Otrzymano odpowiedź ({responseContent.Length} znaków, {elapsed:F1}s)");

                // Parse JSON response
                var recipes = ParseJsonResponse(responseContent);

                Console.WriteLine($"✅ Wyekstraktowano {recipes.Count} przepisów z PDF");

                return recipes;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"❌ Błąd parsowania JSON: {ex.Message}");
                Console.WriteLine($"   Sprawdź format odpowiedzi OpenAI");
                return new List<RecipeExtractionResult>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd przetwarzania PDF: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
                throw; // Let Polly retry
            }
        });
    }

    /// <summary>
    /// Extracts recipes from PDF page images using Vision API
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
                Console.WriteLine($"📤 Wysyłanie {imageChunk.Pages.Count} obrazów do OpenAI (chunk {imageChunk.ChunkNumber}, strony {imageChunk.StartPage}-{imageChunk.EndPage})...");
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

                // Build user message with text + images
                var contentParts = new List<ChatMessageContentPart>
                {
                    ChatMessageContentPart.CreateTextPart(userPrompt)
                };

                // Add each page image
                foreach (var page in imageChunk.Pages)
                {
                    var imagePart = ChatMessageContentPart.CreateImagePart(
                        BinaryData.FromBytes(page.ImageData),
                        page.MimeType);
                    contentParts.Add(imagePart);
                }

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(contentParts)
                };

                Console.WriteLine($"   ⏱️  Timeout: 5 minut");
                var startTime = DateTime.Now;
                string responseContent;

                // Create cancellation token with 5 minute timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

                // Use streaming if progress callback is provided
                if (progress != null)
                {
                    Console.WriteLine($"📡 Używam streaming API dla lepszej obsługi dużych plików...");
                    var responseBuilder = new System.Text.StringBuilder();

                    await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: cts.Token))
                    {
                        foreach (var contentPart in update.ContentUpdate)
                        {
                            responseBuilder.Append(contentPart.Text);
                        }

                        var currentElapsed = (DateTime.Now - startTime).TotalSeconds;

                        // Report progress
                        progress.Report(new StreamingProgress
                        {
                            BytesReceived = responseBuilder.Length,
                            Message = $"Otrzymano {responseBuilder.Length / 1024.0:F1} KB...",
                            ElapsedSeconds = currentElapsed
                        });

                        // Show progress every ~10KB
                        if (responseBuilder.Length % 10000 < 100)
                        {
                            Console.WriteLine($"   📊 Odebrano {responseBuilder.Length / 1024.0:F1} KB... ({currentElapsed:F1}s)");
                        }
                    }

                    responseContent = responseBuilder.ToString().Trim();
                }
                else
                {
                    var completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: cts.Token);
                    responseContent = completion.Value.Content[0].Text.Trim();
                }

                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine($"📥 Otrzymano odpowiedź ({responseContent.Length} znaków, {elapsed:F1}s)");

                // Parse JSON response
                var recipes = ParseJsonResponse(responseContent);

                Console.WriteLine($"✅ Wyekstraktowano {recipes.Count} przepisów z chunka {imageChunk.ChunkNumber}");

                return recipes;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"❌ Błąd parsowania JSON w chunku {imageChunk.ChunkNumber}: {ex.Message}");
                Console.WriteLine($"   Sprawdź format odpowiedzi OpenAI");
                return new List<RecipeExtractionResult>();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"⏱️ TIMEOUT chunka {imageChunk.ChunkNumber}: Przekroczono limit 5 minut!");
                Console.WriteLine($"   To może oznaczać zbyt duże obrazy lub problem z API OpenAI");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd przetwarzania chunka {imageChunk.ChunkNumber}: {ex.GetType().Name}");
                Console.WriteLine($"   Komunikat: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                throw; // Let Polly retry
            }
        });
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Parses JSON response from OpenAI
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
