using Microsoft.AspNetCore.Mvc;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;
using RecipesAIHelper.Services;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessingController : ControllerBase
{
    private readonly PdfProcessorService _pdfProcessor;
    private readonly OpenAIService _openAiService;
    private readonly RecipeDbContext _db;
    private readonly IConfiguration _configuration;
    private static bool _isProcessing = false;
    private static ProcessingStatus _status = new();

    public ProcessingController(
        PdfProcessorService pdfProcessor,
        OpenAIService openAiService,
        RecipeDbContext db,
        IConfiguration configuration)
    {
        _pdfProcessor = pdfProcessor;
        _openAiService = openAiService;
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

    [HttpGet("status")]
    public ActionResult<ProcessingStatus> GetStatus()
    {
        return Ok(_status);
    }

    private async Task ProcessPdfsAsync(List<string> fileNames)
    {
        try
        {
            var pdfDirectory = _configuration["Settings:PdfSourceDirectory"] ?? @"C:\Users\Karolina\Downloads\Dieta";
            var delayMs = int.TryParse(_configuration["Settings:DelayBetweenChunksMs"], out var delay) ? delay : 3000;
            var checkDuplicates = bool.TryParse(_configuration["Settings:CheckDuplicates"], out var checkDup) ? checkDup : true;
            var recentRecipesContext = int.TryParse(_configuration["Settings:RecentRecipesContext"], out var recentCtx) ? recentCtx : 10;

            var filePaths = fileNames.Select(name => Path.Combine(pdfDirectory, name)).ToList();

            _status.TotalFiles = filePaths.Count;
            _status.CurrentFile = 0;

            foreach (var pdfFile in filePaths)
            {
                _status.CurrentFile++;
                _status.Message = $"Processing {Path.GetFileName(pdfFile)}...";

                try
                {
                    var chunks = _pdfProcessor.ExtractTextInChunks(pdfFile);
                    _status.TotalChunks = chunks.Count;
                    _status.CurrentChunk = 0;

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        var chunk = chunks[i];
                        _status.CurrentChunk = i + 1;
                        _status.Message = $"Processing chunk {i + 1}/{chunks.Count} of {Path.GetFileName(pdfFile)}";

                        var recentRecipes = checkDuplicates ? _db.GetRecentRecipes(recentRecipesContext) : null;
                        var recipes = await _openAiService.ExtractRecipesFromChunk(chunk, recentRecipes);

                        foreach (var recipeData in recipes)
                        {
                            if (string.IsNullOrWhiteSpace(recipeData.Name) ||
                                recipeData.Ingredients == null ||
                                recipeData.Ingredients.Count == 0)
                                continue;

                            if (checkDuplicates && _db.RecipeExists(recipeData.Name))
                            {
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
                                CreatedAt = DateTime.Now
                            };

                            _db.InsertRecipe(recipe);
                            _status.RecipesSaved++;
                        }

                        if (i < chunks.Count - 1)
                            await Task.Delay(delayMs);
                    }
                }
                catch (Exception ex)
                {
                    _status.Errors++;
                    _status.LastError = $"Error processing {Path.GetFileName(pdfFile)}: {ex.Message}";
                }
            }

            _status.IsRunning = false;
            _status.Message = "Processing completed!";
        }
        catch (Exception ex)
        {
            _status.IsRunning = false;
            _status.Message = $"Critical error: {ex.Message}";
            _status.Errors++;
        }
        finally
        {
            _isProcessing = false;
        }
    }
}

public class ProcessingRequest
{
    public List<string> Files { get; set; } = new();
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
}
