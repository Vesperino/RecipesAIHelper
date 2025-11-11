using Microsoft.AspNetCore.Mvc;
using RecipesAIHelper.Data;
using RecipesAIHelper.Services;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly RecipeDbContext _db;

    public ImagesController(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Generate image for a single recipe
    /// POST /api/images/generate/{recipeId}
    /// </summary>
    [HttpPost("generate/{recipeId}")]
    public async Task<ActionResult> GenerateImage(int recipeId)
    {
        try
        {
            // Get recipe
            var recipes = _db.GetAllRecipes();
            var recipe = recipes.FirstOrDefault(r => r.Id == recipeId);

            if (recipe == null)
                return NotFound(new { error = "Recipe not found" });

            Console.WriteLine($"🎨 Rozpoczynam generowanie obrazu dla przepisu #{recipeId}: {recipe.Name}");

            // Create image generation service using factory
            var factory = new ImageGenerationServiceFactory(_db);
            var imageService = factory.CreateImageGenerationService();

            if (imageService == null)
            {
                return BadRequest(new
                {
                    error = "Image generation service not configured. Please configure API keys in Settings.",
                    hint = "Set OpenAI_ApiKey or Gemini_ApiKey in database Settings table"
                });
            }

            Console.WriteLine($"   Using provider: {imageService.ProviderName}");

            // Generate image
            var base64Image = await imageService.GenerateRecipeImageAsync(recipe.Name, recipe.Description);

            if (base64Image == null)
            {
                return StatusCode(500, new { error = "Failed to generate image" });
            }

            // Save image to file
            var imageUrl = await imageService.SaveImageToFileAsync(base64Image, recipe.Id, recipe.Name);
            var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageUrl.TrimStart('/'));

            // Update database
            _db.UpdateRecipeImage(recipe.Id, imagePath, imageUrl);

            Console.WriteLine($"✅ Obraz wygenerowany i zapisany: {imageUrl}");

            return Ok(new
            {
                message = "Image generated successfully",
                imageUrl,
                recipeId = recipe.Id,
                recipeName = recipe.Name,
                provider = imageService.ProviderName
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd generowania obrazu: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate images for multiple recipes
    /// POST /api/images/generate-batch
    /// Body: { "recipeIds": [1, 2, 3] }
    /// </summary>
    [HttpPost("generate-batch")]
    public async Task<ActionResult> GenerateBatch([FromBody] BatchGenerateRequest request)
    {
        try
        {
            if (request?.RecipeIds == null || request.RecipeIds.Count == 0)
                return BadRequest(new { error = "No recipe IDs provided" });

            Console.WriteLine($"🎨 Rozpoczynam batch generowanie dla {request.RecipeIds.Count} przepisów");

            // Create image generation service using factory
            var factory = new ImageGenerationServiceFactory(_db);
            var imageService = factory.CreateImageGenerationService();

            if (imageService == null)
            {
                return BadRequest(new
                {
                    error = "Image generation service not configured. Please configure API keys in Settings.",
                    hint = "Set OpenAI_ApiKey or Gemini_ApiKey in database Settings table"
                });
            }

            Console.WriteLine($"   Using provider: {imageService.ProviderName}");

            var results = new List<BatchGenerateResult>();

            // Get all recipes once
            var allRecipes = _db.GetAllRecipes();

            foreach (var recipeId in request.RecipeIds)
            {
                try
                {
                    var recipe = allRecipes.FirstOrDefault(r => r.Id == recipeId);
                    if (recipe == null)
                    {
                        results.Add(new BatchGenerateResult
                        {
                            RecipeId = recipeId,
                            Success = false,
                            Error = "Recipe not found"
                        });
                        continue;
                    }

                    Console.WriteLine($"   📷 [{results.Count + 1}/{request.RecipeIds.Count}] Generowanie dla: {recipe.Name}");

                    // Generate image
                    var base64Image = await imageService.GenerateRecipeImageAsync(recipe.Name, recipe.Description);

                    if (base64Image == null)
                    {
                        results.Add(new BatchGenerateResult
                        {
                            RecipeId = recipeId,
                            RecipeName = recipe.Name,
                            Success = false,
                            Error = "Failed to generate image"
                        });
                        continue;
                    }

                    // Save image
                    var imageUrl = await imageService.SaveImageToFileAsync(base64Image, recipe.Id, recipe.Name);
                    var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageUrl.TrimStart('/'));

                    // Update database
                    _db.UpdateRecipeImage(recipe.Id, imagePath, imageUrl);

                    results.Add(new BatchGenerateResult
                    {
                        RecipeId = recipeId,
                        RecipeName = recipe.Name,
                        Success = true,
                        ImageUrl = imageUrl
                    });

                    Console.WriteLine($"   ✅ Sukces: {recipe.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Błąd dla przepisu #{recipeId}: {ex.Message}");
                    results.Add(new BatchGenerateResult
                    {
                        RecipeId = recipeId,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            var successCount = results.Count(r => r.Success);
            Console.WriteLine($"✅ Batch generowanie zakończone: {successCount}/{request.RecipeIds.Count} sukces");

            return Ok(new
            {
                message = $"Generated {successCount} out of {request.RecipeIds.Count} images",
                totalRequested = request.RecipeIds.Count,
                successful = successCount,
                failed = request.RecipeIds.Count - successCount,
                provider = imageService.ProviderName,
                results
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd batch generowania: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate images for all recipes without images
    /// POST /api/images/generate-all-missing
    /// </summary>
    [HttpPost("generate-all-missing")]
    public async Task<ActionResult> GenerateAllMissing()
    {
        try
        {
            // Get all recipes without images
            var allRecipes = _db.GetAllRecipes();
            var recipesWithoutImages = allRecipes
                .Where(r => string.IsNullOrEmpty(r.ImageUrl))
                .ToList();

            if (recipesWithoutImages.Count == 0)
            {
                return Ok(new { message = "No recipes without images found", count = 0 });
            }

            Console.WriteLine($"🎨 Znaleziono {recipesWithoutImages.Count} przepisów bez zdjęć");

            // Use batch generation
            var batchRequest = new BatchGenerateRequest
            {
                RecipeIds = recipesWithoutImages.Select(r => r.Id).ToList()
            };

            // Call batch endpoint internally
            return await GenerateBatch(batchRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get count of recipes without images
    /// GET /api/images/missing-count
    /// </summary>
    [HttpGet("missing-count")]
    public ActionResult<int> GetMissingCount()
    {
        try
        {
            var allRecipes = _db.GetAllRecipes();
            var count = allRecipes.Count(r => string.IsNullOrEmpty(r.ImageUrl));
            return Ok(new { count, total = allRecipes.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

// Request/Response models
public class BatchGenerateRequest
{
    public List<int> RecipeIds { get; set; } = new();
}

public class BatchGenerateResult
{
    public int RecipeId { get; set; }
    public string? RecipeName { get; set; }
    public bool Success { get; set; }
    public string? ImageUrl { get; set; }
    public string? Error { get; set; }
}
