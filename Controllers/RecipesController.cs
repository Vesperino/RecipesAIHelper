using Microsoft.AspNetCore.Mvc;
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
            // In a real app, add UpdateRecipe method to RecipeDbContext
            // For now, return not implemented
            return StatusCode(501, new { error = "Update not yet implemented" });
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
            // In a real app, add DeleteRecipe method to RecipeDbContext
            // For now, return not implemented
            return StatusCode(501, new { error = "Delete not yet implemented" });
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
}
