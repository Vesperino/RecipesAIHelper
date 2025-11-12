using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;
using RecipesAIHelper.Services;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SourceFilesController : ControllerBase
{
    private readonly RecipeDbContext _db;
    private readonly PdfImageService _pdfImageService;
    private readonly PdfDirectService _pdfDirectService;
    private readonly AIServiceFactory _aiServiceFactory;
    private readonly IConfiguration _configuration;

    public SourceFilesController(
        RecipeDbContext db,
        PdfImageService pdfImageService,
        AIServiceFactory aiServiceFactory,
        IConfiguration configuration)
    {
        _db = db;
        _pdfImageService = pdfImageService;
        _pdfDirectService = new PdfDirectService();
        _aiServiceFactory = aiServiceFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Get list of all unique source PDF files with recipe counts
    /// </summary>
    [HttpGet]
    public ActionResult<List<SourceFileInfo>> GetSourceFiles()
    {
        try
        {
            var counts = _db.GetRecipeCountsBySourceFile();
            var result = counts.Select(kvp => new SourceFileInfo
            {
                FileName = kvp.Key,
                RecipeCount = kvp.Value
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all recipes from a specific source PDF file
    /// </summary>
    [HttpGet("{fileName}/recipes")]
    public ActionResult<List<Recipe>> GetRecipesBySourceFile(string fileName)
    {
        try
        {
            var recipes = _db.GetRecipesBySourceFile(fileName);
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete all recipes from a specific source PDF file
    /// </summary>
    [HttpDelete("{fileName}/recipes")]
    public ActionResult DeleteRecipesBySourceFile(string fileName)
    {
        try
        {
            var deletedCount = _db.DeleteRecipesBySourceFile(fileName);
            return Ok(new { deletedCount, message = $"Usunięto {deletedCount} przepisów z pliku '{fileName}'" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Regenerate all recipes from a specific source PDF file
    /// This will delete existing recipes and reprocess the file
    /// </summary>
    [HttpPost("{fileName}/regenerate")]
    public async Task<ActionResult> RegenerateRecipesFromFile(string fileName)
    {
        try
        {
            // Get PDF source directory from config
            var pdfDirectory = _configuration["Settings:PdfSourceDirectory"] ?? @"C:\Users\Karolina\Downloads\Dieta";
            var pdfFilePath = Path.Combine(pdfDirectory, fileName);

            // Check if file exists
            if (!System.IO.File.Exists(pdfFilePath))
            {
                return NotFound(new { error = $"Plik '{fileName}' nie został znaleziony w katalogu: {pdfDirectory}" });
            }

            // Delete existing recipes from this file
            var deletedCount = _db.DeleteRecipesBySourceFile(fileName);
            Console.WriteLine($"🗑️  Usunięto {deletedCount} przepisów z pliku '{fileName}'");

            // Get active AI provider
            var activeProvider = _aiServiceFactory.GetActiveProvider();
            if (activeProvider == null)
            {
                return BadRequest(new { error = "Brak aktywnego providera AI. Skonfiguruj providera w zakładce 'Ustawienia'." });
            }

            // Create AI service
            var aiService = _aiServiceFactory.CreateService(activeProvider);
            if (aiService == null)
            {
                return StatusCode(500, new { error = $"Nie udało się utworzyć serwisu dla providera {activeProvider.Name}" });
            }

            // Get settings
            var delayMs = int.TryParse(_configuration["Settings:DelayBetweenChunksMs"], out var delay) ? delay : 3000;
            var checkDuplicates = bool.TryParse(_configuration["Settings:CheckDuplicates"], out var checkDup) ? checkDup : true;
            var recentRecipesContext = int.TryParse(_configuration["Settings:RecentRecipesContext"], out var recentCtx) ? recentCtx : 10;

            Console.WriteLine($"📄 Regenerowanie przepisów z pliku: {fileName}");
            Console.WriteLine($"   Provider: {activeProvider.Name} ({activeProvider.Model})");

            // Process the PDF file
            var allRecipes = new List<RecipeExtractionResult>();
            var processedInThisPdf = new List<string>();
            var recentRecipes = checkDuplicates ? _db.GetRecentRecipes(recentRecipesContext) : null;

            var progress = new Progress<StreamingProgress>(p => { /* No-op for now */ });

            // Check if provider supports direct PDF processing
            if (activeProvider.SupportsDirectPDF)
            {
                Console.WriteLine($"   Wysyłanie PDF bezpośrednio do {activeProvider.Name}...");
                var pdfChunk = _pdfDirectService.PreparePdfForApi(pdfFilePath);
                var startTime = DateTime.Now;
                var recipes = await aiService.ExtractRecipesFromPdf(pdfChunk, recentRecipes, progress);
                var processingTime = (DateTime.Now - startTime).TotalSeconds;
                Console.WriteLine($"   ✅ Otrzymano {recipes.Count} przepisów (czas: {processingTime:F1}s)");
                allRecipes.AddRange(recipes);
            }
            else
            {
                // Image mode
                var pagesPerChunk = activeProvider.MaxPagesPerChunk;
                var imageChunks = _pdfImageService.RenderPdfInChunks(pdfFilePath, pagesPerChunk, dpi: 1200, saveDebugImages: false, targetHeight: 3200);
                Console.WriteLine($"   PDF wyrenderowany w {imageChunks.Count} chunkach po {pagesPerChunk} stron");

                for (int i = 0; i < imageChunks.Count; i++)
                {
                    var imageChunk = imageChunks[i];
                    Console.WriteLine($"   Chunk {i + 1}/{imageChunks.Count}: Strony {imageChunk.StartPage}-{imageChunk.EndPage}");

                    var startTime = DateTime.Now;
                    var recipes = await aiService.ExtractRecipesFromImages(imageChunk, recentRecipes, processedInThisPdf, progress);
                    var processingTime = (DateTime.Now - startTime).TotalSeconds;

                    Console.WriteLine($"   ✅ Otrzymano {recipes.Count} przepisów (czas: {processingTime:F1}s)");
                    allRecipes.AddRange(recipes);

                    if (i < imageChunks.Count - 1)
                    {
                        Console.WriteLine($"   ⏸️  Oczekiwanie {delayMs}ms...");
                        await Task.Delay(delayMs);
                    }
                }
            }

            // Save recipes to database
            int savedCount = 0;
            int skippedCount = 0;

            foreach (var recipeData in allRecipes)
            {
                if (string.IsNullOrWhiteSpace(recipeData.Name))
                    continue;

                if (recipeData.Ingredients == null || recipeData.Ingredients.Count == 0)
                    continue;

                if (checkDuplicates && _db.RecipeExists(recipeData.Name))
                {
                    Console.WriteLine($"   ⏭️  Pominięto '{recipeData.Name}' - duplikat");
                    skippedCount++;
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
                    NutritionVariants = recipeData.NutritionVariants,
                    SourcePdfFile = fileName
                };

                _db.InsertRecipe(recipe);
                Console.WriteLine($"   ✅ Zapisano: {recipe.Name} ({recipe.MealType}) - {recipe.Calories} kcal");
                savedCount++;
                processedInThisPdf.Add(recipe.Name);
            }

            Console.WriteLine($"✅ Regeneracja zakończona: zapisano {savedCount} przepisów, pominięto {skippedCount}");

            return Ok(new
            {
                deletedCount,
                savedCount,
                skippedCount,
                message = $"Regeneracja zakończona: usunięto {deletedCount} starych przepisów, zapisano {savedCount} nowych"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd podczas regeneracji: {ex.Message}");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}

public class SourceFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public int RecipeCount { get; set; }
}
