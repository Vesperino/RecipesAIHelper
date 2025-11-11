using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipesController : ControllerBase
{
    private readonly RecipeDbContext _db;

    public RecipesController(RecipeDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public ActionResult<List<Recipe>> GetAll()
    {
        try
        {
            var recipes = _db.GetAllRecipes();
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public ActionResult<Recipe> GetById(int id)
    {
        try
        {
            var recipes = _db.GetAllRecipes();
            var recipe = recipes.FirstOrDefault(r => r.Id == id);

            if (recipe == null)
                return NotFound(new { error = "Recipe not found" });

            return Ok(recipe);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("random/{mealType}")]
    public ActionResult<List<Recipe>> GetRandomByMealType(string mealType, [FromQuery] int count = 1)
    {
        try
        {
            if (!Enum.TryParse<MealType>(mealType, true, out var parsedMealType))
                return BadRequest(new { error = $"Invalid meal type: {mealType}" });

            var recipes = _db.GetRandomRecipesByMealType(parsedMealType, count);
            return Ok(recipes);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public ActionResult UpdateRecipe(int id, [FromBody] Recipe recipe)
    {
        try
        {
            recipe.Id = id;
            var success = _db.UpdateRecipe(recipe);

            if (!success)
                return NotFound(new { error = "Recipe not found" });

            return Ok(new { message = "Recipe updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public ActionResult DeleteRecipe(int id)
    {
        try
        {
            var success = _db.DeleteRecipe(id);

            if (!success)
                return NotFound(new { error = "Recipe not found" });

            return Ok(new { message = "Recipe deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("count")]
    public ActionResult<int> GetCount()
    {
        try
        {
            var count = _db.GetRecipeCount();
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{id}/image")]
    public async Task<ActionResult> UploadImage(int id, [FromForm] IFormFile image)
    {
        try
        {
            // Check if recipe exists
            var recipes = _db.GetAllRecipes();
            var recipe = recipes.FirstOrDefault(r => r.Id == id);

            if (recipe == null)
                return NotFound(new { error = "Recipe not found" });

            if (image == null || image.Length == 0)
                return BadRequest(new { error = "No image file provided" });

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { error = "Invalid file type. Allowed: jpg, jpeg, png, webp" });

            // Validate file size (max 10MB)
            if (image.Length > 10 * 1024 * 1024)
                return BadRequest(new { error = "File too large. Maximum size is 10MB" });

            // Create images directory if it doesn't exist
            var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "recipes");
            Directory.CreateDirectory(imagesDir);

            // Generate unique filename
            var fileName = $"recipe_{id}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(imagesDir, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            // Update database
            var imageUrl = $"/images/recipes/{fileName}";
            _db.UpdateRecipeImage(id, filePath, imageUrl);

            return Ok(new { message = "Image uploaded successfully", imageUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{id}/image")]
    public ActionResult DeleteImage(int id)
    {
        try
        {
            // Check if recipe exists
            var recipes = _db.GetAllRecipes();
            var recipe = recipes.FirstOrDefault(r => r.Id == id);

            if (recipe == null)
                return NotFound(new { error = "Recipe not found" });

            if (string.IsNullOrEmpty(recipe.ImagePath))
                return NotFound(new { error = "Recipe has no image" });

            // Delete physical file
            if (System.IO.File.Exists(recipe.ImagePath))
            {
                System.IO.File.Delete(recipe.ImagePath);
            }

            // Update database
            _db.UpdateRecipeImage(id, null, null);

            return Ok(new { message = "Image deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
