using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;
using RecipesAIHelper.Services;
using System.Security.Cryptography;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessingController : ControllerBase
{
    private readonly PdfImageService _pdfImageService;
    private readonly PdfDirectService _pdfDirectService;
    private readonly AIServiceFactory _aiServiceFactory;
    private readonly RecipeDbContext _db;
    private readonly IConfiguration _configuration;
    private static bool _isProcessing = false;
    private static ProcessingStatus _status = new();

    public ProcessingController(
        PdfImageService pdfImageService,
        AIServiceFactory aiServiceFactory,
        RecipeDbContext db,
        IConfiguration configuration)
    {
        _pdfImageService = pdfImageService;
        _pdfDirectService = new PdfDirectService();
        _aiServiceFactory = aiServiceFactory;
        _db = db;
        _configuration = configuration;
    }

    [HttpPost("start")]
    public ActionResult StartProcessing([FromBody] ProcessingRequest request)
    {
        if (_isProcessing)
            return BadRequest(new { error = "Processing already in progress" });

        _isProcessing = true;
        _status = new ProcessingStatus { IsRunning = true, Message = "Starting processing..." };

        // Start processing in background
        Task.Run(async () => await ProcessPdfsAsync(request.Files));

        return Ok(new { message = "Processing started", status = _status });
    }

    [HttpPost("process-uploaded")]
    public ActionResult StartProcessingUploaded([FromBody] ProcessUploadedRequest request)
    {
        if (_isProcessing)
            return BadRequest(new { error = "Processing already in progress" });

        _isProcessing = true;
        _status = new ProcessingStatus { IsRunning = true, Message = "Starting processing uploaded files..." };

        // Start processing in background with full file paths
        Task.Run(async () => await ProcessUploadedFilesAsync(request.FilePaths));

        return Ok(new { message = "Processing started", status = _status });
    }

    [HttpGet("status")]
    public ActionResult<ProcessingStatus> GetStatus()
    {
        return Ok(_status);
    }

    private async Task ProcessPdfsAsync(List<string> fileNames)
    {
        try
        {
            // Get active AI provider from database
            var activeProvider = _aiServiceFactory.GetActiveProvider();
            if (activeProvider == null)
            {
                _status.IsRunning = false;
                _status.Message = "Brak aktywnego providera AI. Skonfiguruj providera w zakładce 'Ustawienia'.";
                _status.Errors++;
                Console.WriteLine("❌ BŁĄD: Brak aktywnego providera AI w bazie danych");
                Console.WriteLine("   Przejdź do zakładki 'Ustawienia' i skonfiguruj providera (OpenAI lub Gemini)");
                _isProcessing = false;
                return;
            }

            // Create AI service instance
            var aiService = _aiServiceFactory.CreateService(activeProvider);
            if (aiService == null)
            {
                _status.IsRunning = false;
                _status.Message = "Nie udało się utworzyć serwisu AI";
                _status.Errors++;
                Console.WriteLine($"❌ BŁĄD: Nie udało się utworzyć serwisu dla providera {activeProvider.Name}");
                _isProcessing = false;
                return;
            }

            var pdfDirectory = _configuration["Settings:PdfSourceDirectory"] ?? @"C:\Users\Karolina\Downloads\Dieta";
            var delayMs = int.TryParse(_configuration["Settings:DelayBetweenChunksMs"], out var delay) ? delay : 3000;
            var checkDuplicates = bool.TryParse(_configuration["Settings:CheckDuplicates"], out var checkDup) ? checkDup : true;
            var recentRecipesContext = int.TryParse(_configuration["Settings:RecentRecipesContext"], out var recentCtx) ? recentCtx : 10;

            var filePaths = fileNames.Select(name => Path.Combine(pdfDirectory, name)).ToList();

            Console.WriteLine("\n================================================================================");
            Console.WriteLine("ROZPOCZĘCIE PRZETWARZANIA PDF");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"Folder: {pdfDirectory}");
            Console.WriteLine($"AI Provider: {activeProvider.Name}");
            Console.WriteLine($"Model: {activeProvider.Model}");
            Console.WriteLine($"Max stron/chunk: {activeProvider.MaxPagesPerChunk}");
            Console.WriteLine($"Obsługuje bezpośrednie PDF: {(activeProvider.SupportsDirectPDF ? "TAK" : "NIE (konwersja do obrazów)")}");
            Console.WriteLine($"Rate limiting: {delayMs}ms opóźnienia między chunkami");
            Console.WriteLine($"Sprawdzanie duplikatów: {(checkDuplicates ? "TAK" : "NIE")}");
            if (checkDuplicates)
                Console.WriteLine($"Kontekst ostatnich przepisów: {recentRecipesContext}");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"📄 Znaleziono {filePaths.Count} plików do przetworzenia\n");

            _status.TotalFiles = filePaths.Count;
            _status.CurrentFile = 0;

            foreach (var pdfFile in filePaths)
            {
                _status.CurrentFile++;
                _status.Message = $"Processing {Path.GetFileName(pdfFile)}...";

                Console.WriteLine("================================================================================");
                Console.WriteLine($"📋 Przetwarzanie [{_status.CurrentFile}/{_status.TotalFiles}]: {Path.GetFileName(pdfFile)}");
                Console.WriteLine("================================================================================");

                // Track number of recipes extracted from this file
                int recipesExtractedFromFile = 0;

                try
                {
                    // Track recipes already processed in THIS PDF to avoid duplicates within chunks
                    var processedInThisPdf = new List<string>();
                    var recentRecipes = checkDuplicates ? _db.GetRecentRecipes(recentRecipesContext) : null;

                    List<RecipeExtractionResult> allRecipes = new List<RecipeExtractionResult>();

                    // Create progress callback to update status
                    var progress = new Progress<StreamingProgress>(p =>
                    {
                        _status.StreamingBytesReceived = p.BytesReceived;
                        _status.StreamingMessage = p.Message;
                        _status.StreamingElapsedSeconds = p.ElapsedSeconds;
                    });

                    // Check if provider supports direct PDF processing
                    if (activeProvider.SupportsDirectPDF)
                    {
                        // Direct PDF mode - send whole PDF at once
                        Console.WriteLine($"📄 Wysyłanie PDF bezpośrednio do {activeProvider.Name} (bez renderowania do obrazów)...");

                        var pdfChunk = _pdfDirectService.PreparePdfForApi(pdfFile);
                        _status.TotalChunks = 1;
                        _status.CurrentChunk = 1;

                        if (recentRecipes != null && recentRecipes.Count > 0)
                        {
                            Console.WriteLine($"  Kontekst: {recentRecipes.Count} ostatnich przepisów w bazie");
                        }

                        var startTime = DateTime.Now;
                        var recipes = await aiService.ExtractRecipesFromPdf(pdfChunk, recentRecipes, progress);
                        var processingTime = (DateTime.Now - startTime).TotalSeconds;

                        Console.WriteLine($"✅ Otrzymano {recipes.Count} przepisów (czas: {processingTime:F1}s)");
                        allRecipes.AddRange(recipes);

                        // Reset streaming progress after completion
                        _status.StreamingBytesReceived = 0;
                        _status.StreamingMessage = "";
                        _status.StreamingElapsedSeconds = 0;
                    }
                    else
                    {
                        // Image mode - render PDF to images and send chunks
                        var pagesPerChunk = activeProvider.MaxPagesPerChunk;
                        var imageChunks = _pdfImageService.RenderPdfInChunks(pdfFile, pagesPerChunk, dpi: 1200, saveDebugImages: true, targetHeight: 3200);

                        _status.TotalChunks = imageChunks.Count;
                        _status.CurrentChunk = 0;

                        Console.WriteLine($"📊 PDF wyrenderowany w {imageChunks.Count} chunkach po {pagesPerChunk} stron (1200 DPI → 3200px)\n");

                        for (int i = 0; i < imageChunks.Count; i++)
                        {
                            var imageChunk = imageChunks[i];
                            _status.CurrentChunk = i + 1;
                            _status.Message = $"Processing chunk {i + 1}/{imageChunks.Count} of {Path.GetFileName(pdfFile)}";

                            Console.WriteLine($"[Chunk {imageChunk.ChunkNumber}/{imageChunks.Count}] Strony {imageChunk.StartPage}-{imageChunk.EndPage}");
                            Console.WriteLine($"  Liczba obrazów: {imageChunk.Pages.Count}");

                            if (recentRecipes != null && recentRecipes.Count > 0)
                            {
                                Console.WriteLine($"  Kontekst: {recentRecipes.Count} ostatnich przepisów w bazie");
                            }

                            if (processedInThisPdf.Count > 0)
                            {
                                Console.WriteLine($"  Historia PDF: {processedInThisPdf.Count} przepisów już przetworzonych w tym pliku");
                            }

                            Console.WriteLine($"  ⏳ Wysyłanie obrazów do {activeProvider.Name} ({activeProvider.Model})...");
                            var startTime = DateTime.Now;
                            var recipes = await aiService.ExtractRecipesFromImages(imageChunk, recentRecipes, processedInThisPdf, progress);
                            var processingTime = (DateTime.Now - startTime).TotalSeconds;

                            Console.WriteLine($"  ✅ Otrzymano {recipes.Count} przepisów (czas: {processingTime:F1}s)");
                            allRecipes.AddRange(recipes);

                            // Reset streaming progress after chunk completion
                            _status.StreamingBytesReceived = 0;
                            _status.StreamingMessage = "";
                            _status.StreamingElapsedSeconds = 0;

                            if (i < imageChunks.Count - 1)
                            {
                                Console.WriteLine($"\n  ⏸️  Oczekiwanie {delayMs}ms przed następnym chunkiem...\n");
                                await Task.Delay(delayMs);
                            }
                        }
                    }

                    // Save all recipes to database
                    foreach (var recipeData in allRecipes)
                    {
                        if (string.IsNullOrWhiteSpace(recipeData.Name))
                        {
                            Console.WriteLine($"    ⚠️  Pominięto przepis bez nazwy");
                            continue;
                        }

                        if (recipeData.Ingredients == null || recipeData.Ingredients.Count == 0)
                        {
                            Console.WriteLine($"    ⚠️  Pominięto '{recipeData.Name}' - brak składników");
                            continue;
                        }

                        if (checkDuplicates && _db.RecipeExists(recipeData.Name))
                        {
                            Console.WriteLine($"    ⏭️  Pominięto '{recipeData.Name}' - duplikat (dokładne dopasowanie)");
                            _status.DuplicatesSkipped++;
                            continue;
                        }

                        var recipe = new Recipe
                        {
                            Name = recipeData.Name,
                            Description = recipeData.Description,
                            Ingredients = string.Join("\n", recipeData.Ingredients),
                            Instructions = recipeData.Instructions,
                            Calories = recipeData.Calories,
                            Protein = recipeData.Protein,
                            Carbohydrates = recipeData.Carbohydrates,
                            Fat = recipeData.Fat,
                            MealType = Enum.TryParse<MealType>(recipeData.MealType, out var mealType)
                                ? mealType
                                : MealType.Obiad,
                            CreatedAt = DateTime.Now,
                            Servings = recipeData.Servings,
                            NutritionVariants = recipeData.NutritionVariants
                        };

                        // Debug: Log NutritionVariantsJson before saving
                        if (recipeData.NutritionVariants != null && recipeData.NutritionVariants.Count > 0)
                        {
                            Console.WriteLine($"    🔍 DEBUG {recipe.Name}: recipeData.NutritionVariants ma {recipeData.NutritionVariants.Count} wariantów");
                            Console.WriteLine($"    🔍 DEBUG {recipe.Name}: recipe.NutritionVariantsJson = {(recipe.NutritionVariantsJson == null ? "NULL" : $"{recipe.NutritionVariantsJson.Length} znaków")}");
                            if (recipe.NutritionVariantsJson != null && recipe.NutritionVariantsJson.Length < 200)
                            {
                                Console.WriteLine($"    🔍 DEBUG JSON: {recipe.NutritionVariantsJson}");
                            }
                        }

                        _db.InsertRecipe(recipe);
                        Console.WriteLine($"    ✅ Zapisano: {recipe.Name} ({recipe.MealType}) - {recipe.Calories} kcal");
                        _status.RecipesSaved++;
                        recipesExtractedFromFile++;

                        // Add to processed list to prevent duplicates in subsequent chunks
                        processedInThisPdf.Add(recipe.Name);
                    }

                    // Save file checksum after successful processing
                    try
                    {
                        var fileChecksum = CalculateFileChecksum(pdfFile);
                        var fileInfo = new FileInfo(pdfFile);

                        var processedFile = new ProcessedFile
                        {
                            FileName = Path.GetFileName(pdfFile),
                            FileChecksum = fileChecksum,
                            FileSizeBytes = fileInfo.Length,
                            ProcessedAt = DateTime.Now,
                            RecipesExtracted = recipesExtractedFromFile
                        };

                        _db.InsertProcessedFile(processedFile);
                        Console.WriteLine($"    💾 Zapisano checksum pliku: {fileChecksum}");
                    }
                    catch (Exception checksumEx)
                    {
                        Console.WriteLine($"    ⚠️  Nie udało się zapisać checksumy: {checksumEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _status.Errors++;
                    _status.LastError = $"Error processing {Path.GetFileName(pdfFile)}: {ex.Message}";
                    Console.WriteLine($"❌ Błąd podczas przetwarzania {Path.GetFileName(pdfFile)}: {ex.Message}");
                }

                Console.WriteLine($"\n✅ Zakończono plik: {Path.GetFileName(pdfFile)}");
                Console.WriteLine($"   Chunków przetworzonych: {_status.TotalChunks}");
                Console.WriteLine($"   Przepisów zapisanych: {_status.RecipesSaved}");
                Console.WriteLine($"   Duplikatów pominiętych: {_status.DuplicatesSkipped}");
                Console.WriteLine("────────────────────────────────────────────────────────────────────────────────\n");
            }

            _status.IsRunning = false;
            _status.Message = "Processing completed!";

            Console.WriteLine("\n================================================================================");
            Console.WriteLine("🎉 PRZETWARZANIE ZAKOŃCZONE");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"📁 Plików przetworzonych: {_status.TotalFiles}");
            Console.WriteLine($"📋 Przepisów zapisanych: {_status.RecipesSaved}");
            Console.WriteLine($"⏭️  Duplikatów pominiętych: {_status.DuplicatesSkipped}");
            Console.WriteLine($"❌ Błędów: {_status.Errors}");
            Console.WriteLine($"📊 Obecna liczba przepisów w bazie: {_db.GetRecipeCount()}");
            Console.WriteLine("================================================================================\n");
        }
        catch (Exception ex)
        {
            _status.IsRunning = false;
            _status.Message = $"Critical error: {ex.Message}";
            _status.Errors++;
            Console.WriteLine($"\n❌ BŁĄD KRYTYCZNY: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ProcessUploadedFilesAsync(List<string> filePaths)
    {
        try
        {
            // Get active AI provider from database
            var activeProvider = _aiServiceFactory.GetActiveProvider();
            if (activeProvider == null)
            {
                _status.IsRunning = false;
                _status.Message = "Brak aktywnego providera AI. Skonfiguruj providera w zakładce 'Ustawienia'.";
                _status.Errors++;
                Console.WriteLine("❌ BŁĄD: Brak aktywnego providera AI w bazie danych");
                _isProcessing = false;
                return;
            }

            // Create AI service instance
            var aiService = _aiServiceFactory.CreateService(activeProvider);
            if (aiService == null)
            {
                _status.IsRunning = false;
                _status.Message = "Nie udało się utworzyć serwisu AI";
                _status.Errors++;
                Console.WriteLine($"❌ BŁĄD: Nie udało się utworzyć serwisu dla providera {activeProvider.Name}");
                _isProcessing = false;
                return;
            }

            var delayMs = int.TryParse(_configuration["Settings:DelayBetweenChunksMs"], out var delay) ? delay : 3000;
            var checkDuplicates = bool.TryParse(_configuration["Settings:CheckDuplicates"], out var checkDup) ? checkDup : true;
            var recentRecipesContext = int.TryParse(_configuration["Settings:RecentRecipesContext"], out var recentCtx) ? recentCtx : 10;

            Console.WriteLine("\n================================================================================");
            Console.WriteLine("ROZPOCZĘCIE PRZETWARZANIA UPLOADOWANYCH PLIKÓW");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"AI Provider: {activeProvider.Name}");
            Console.WriteLine($"Model: {activeProvider.Model}");
            Console.WriteLine($"Max stron/chunk: {activeProvider.MaxPagesPerChunk}");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"📄 Znaleziono {filePaths.Count} plików do przetworzenia\n");

            _status.TotalFiles = filePaths.Count;
            _status.CurrentFile = 0;

            // Use the same processing logic as ProcessPdfsAsync
            // This is a simplified version - in production you might want to refactor common logic
            foreach (var filePath in filePaths)
            {
                _status.CurrentFile++;
                _status.Message = $"Processing {Path.GetFileName(filePath)}...";

                Console.WriteLine("================================================================================");
                Console.WriteLine($"📋 Przetwarzanie [{_status.CurrentFile}/{_status.TotalFiles}]: {Path.GetFileName(filePath)}");
                Console.WriteLine("================================================================================");

                int recipesExtractedFromFile = 0;

                try
                {
                    var processedInThisPdf = new List<string>();
                    var recentRecipes = checkDuplicates ? _db.GetRecentRecipes(recentRecipesContext) : null;

                    List<RecipeExtractionResult> allRecipes = new List<RecipeExtractionResult>();

                    var progress = new Progress<StreamingProgress>(p =>
                    {
                        _status.StreamingBytesReceived = p.BytesReceived;
                        _status.StreamingMessage = p.Message;
                        _status.StreamingElapsedSeconds = p.ElapsedSeconds;
                    });

                    // Check if file is PDF or image
                    var extension = Path.GetExtension(filePath).ToLowerInvariant();

                    if (extension == ".pdf")
                    {
                        // PDF processing
                        if (activeProvider.SupportsDirectPDF)
                        {
                            Console.WriteLine($"📄 Wysyłanie PDF bezpośrednio do {activeProvider.Name}...");
                            var pdfChunk = _pdfDirectService.PreparePdfForApi(filePath);
                            _status.TotalChunks = 1;
                            _status.CurrentChunk = 1;

                            var startTime = DateTime.Now;
                            var recipes = await aiService.ExtractRecipesFromPdf(pdfChunk, recentRecipes, progress);
                            var processingTime = (DateTime.Now - startTime).TotalSeconds;

                            Console.WriteLine($"✅ Otrzymano {recipes.Count} przepisów (czas: {processingTime:F1}s)");
                            allRecipes.AddRange(recipes);
                        }
                        else
                        {
                            // Image mode
                            var pagesPerChunk = activeProvider.MaxPagesPerChunk;
                            var imageChunks = _pdfImageService.RenderPdfInChunks(filePath, pagesPerChunk, dpi: 1200, saveDebugImages: false, targetHeight: 3200);

                            _status.TotalChunks = imageChunks.Count;
                            _status.CurrentChunk = 0;

                            for (int i = 0; i < imageChunks.Count; i++)
                            {
                                var imageChunk = imageChunks[i];
                                _status.CurrentChunk = i + 1;

                                var startTime = DateTime.Now;
                                var recipes = await aiService.ExtractRecipesFromImages(imageChunk, recentRecipes, processedInThisPdf, progress);
                                var processingTime = (DateTime.Now - startTime).TotalSeconds;

                                Console.WriteLine($"✅ Chunk {i + 1}/{imageChunks.Count}: {recipes.Count} przepisów (czas: {processingTime:F1}s)");
                                allRecipes.AddRange(recipes);

                                if (i < imageChunks.Count - 1)
                                    await Task.Delay(delayMs);
                            }
                        }
                    }
                    else if (extension == ".jpg" || extension == ".jpeg" || extension == ".png")
                    {
                        // Single image processing
                        Console.WriteLine($"🖼️ Przetwarzanie pojedynczego obrazu...");
                        // TODO: Implement single image processing
                        Console.WriteLine("⚠️ Przetwarzanie pojedynczych obrazów nie jest jeszcze zaimplementowane");
                    }

                    // Save recipes to database
                    foreach (var recipeData in allRecipes)
                    {
                        if (string.IsNullOrWhiteSpace(recipeData.Name))
                            continue;

                        if (recipeData.Ingredients == null || recipeData.Ingredients.Count == 0)
                            continue;

                        if (checkDuplicates && _db.RecipeExists(recipeData.Name))
                        {
                            Console.WriteLine($"    ⏭️  Pominięto '{recipeData.Name}' - duplikat");
                            _status.DuplicatesSkipped++;
                            continue;
                        }

                        var recipe = new Recipe
                        {
                            Name = recipeData.Name,
                            Description = recipeData.Description,
                            Ingredients = string.Join("\n", recipeData.Ingredients),
                            Instructions = recipeData.Instructions,
                            Calories = recipeData.Calories,
                            Protein = recipeData.Protein,
                            Carbohydrates = recipeData.Carbohydrates,
                            Fat = recipeData.Fat,
                            MealType = Enum.TryParse<MealType>(recipeData.MealType, out var mealType)
                                ? mealType
                                : MealType.Obiad,
                            CreatedAt = DateTime.Now,
                            Servings = recipeData.Servings,
                            NutritionVariants = recipeData.NutritionVariants
                        };

                        _db.InsertRecipe(recipe);
                        Console.WriteLine($"    ✅ Zapisano: {recipe.Name} ({recipe.MealType}) - {recipe.Calories} kcal");
                        _status.RecipesSaved++;
                        recipesExtractedFromFile++;

                        processedInThisPdf.Add(recipe.Name);
                    }

                    // Save file checksum
                    try
                    {
                        var fileChecksum = CalculateFileChecksum(filePath);
                        var fileInfo = new FileInfo(filePath);

                        var processedFile = new ProcessedFile
                        {
                            FileName = Path.GetFileName(filePath),
                            FileChecksum = fileChecksum,
                            FileSizeBytes = fileInfo.Length,
                            ProcessedAt = DateTime.Now,
                            RecipesExtracted = recipesExtractedFromFile
                        };

                        _db.InsertProcessedFile(processedFile);
                        Console.WriteLine($"    💾 Zapisano checksum pliku");
                    }
                    catch (Exception checksumEx)
                    {
                        Console.WriteLine($"    ⚠️  Nie udało się zapisać checksumy: {checksumEx.Message}");
                    }

                    // Delete temporary uploaded file
                    try
                    {
                        if (System.IO.File.Exists(filePath))
                            System.IO.File.Delete(filePath);
                    }
                    catch (Exception deleteEx)
                    {
                        Console.WriteLine($"    ⚠️  Nie udało się usunąć pliku tymczasowego: {deleteEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _status.Errors++;
                    _status.LastError = $"Error processing {Path.GetFileName(filePath)}: {ex.Message}";
                    Console.WriteLine($"❌ Błąd podczas przetwarzania: {ex.Message}");
                }

                Console.WriteLine($"\n✅ Zakończono plik: {Path.GetFileName(filePath)}");
            }

            _status.IsRunning = false;
            _status.Message = "Processing completed!";

            Console.WriteLine("\n================================================================================");
            Console.WriteLine("🎉 PRZETWARZANIE ZAKOŃCZONE");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"📁 Plików przetworzonych: {_status.TotalFiles}");
            Console.WriteLine($"📋 Przepisów zapisanych: {_status.RecipesSaved}");
            Console.WriteLine($"⏭️  Duplikatów pominiętych: {_status.DuplicatesSkipped}");
            Console.WriteLine($"❌ Błędów: {_status.Errors}");
            Console.WriteLine("================================================================================\n");
        }
        catch (Exception ex)
        {
            _status.IsRunning = false;
            _status.Message = $"Critical error: {ex.Message}";
            _status.Errors++;
            Console.WriteLine($"\n❌ BŁĄD KRYTYCZNY: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private string CalculateFileChecksum(string filePath)
    {
        using var stream = System.IO.File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }
}

public class ProcessingRequest
{
    public List<string> Files { get; set; } = new();
}

public class ProcessUploadedRequest
{
    public List<string> FilePaths { get; set; } = new();
}

public class ProcessingStatus
{
    public bool IsRunning { get; set; }
    public string Message { get; set; } = "";
    public int TotalFiles { get; set; }
    public int CurrentFile { get; set; }
    public int TotalChunks { get; set; }
    public int CurrentChunk { get; set; }
    public int RecipesSaved { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int Errors { get; set; }
    public string? LastError { get; set; }

    // Streaming progress
    public int StreamingBytesReceived { get; set; }
    public string StreamingMessage { get; set; } = "";
    public double StreamingElapsedSeconds { get; set; }
}
