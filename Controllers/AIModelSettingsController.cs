using Microsoft.AspNetCore.Mvc;
using RecipesAIHelper.Data;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AIModelSettingsController : ControllerBase
{
    private readonly RecipeDbContext _db;

    public AIModelSettingsController(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get AI model settings for scaling and dessert planning
    /// GET /api/aimodelsettings
    /// </summary>
    [HttpGet]
    public ActionResult GetAIModelSettings()
    {
        try
        {
            var settings = new
            {
                recipeScaling = new
                {
                    model = _db.GetSetting("RecipeScaling_Model") ?? "gemini-2.5-flash"
                },
                dessertPlanning = new
                {
                    model = _db.GetSetting("DessertPlanning_Model") ?? "gemini-2.5-flash"
                }
            };

            return Ok(settings);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update AI model settings for scaling and dessert planning
    /// PUT /api/aimodelsettings
    /// </summary>
    [HttpPut]
    public ActionResult UpdateAIModelSettings([FromBody] AIModelSettingsUpdate update)
    {
        try
        {
            var updated = false;

            // Update recipe scaling model
            if (!string.IsNullOrEmpty(update.RecipeScaling?.Model))
            {
                _db.UpsertSetting("RecipeScaling_Model", update.RecipeScaling.Model, "string",
                    "Model AI do skalowania składników przepisów");
                updated = true;
                Console.WriteLine($"✅ Zmieniono model skalowania przepisów na: {update.RecipeScaling.Model}");
            }

            // Update dessert planning model
            if (!string.IsNullOrEmpty(update.DessertPlanning?.Model))
            {
                _db.UpsertSetting("DessertPlanning_Model", update.DessertPlanning.Model, "string",
                    "Model AI do planowania deserów");
                updated = true;
                Console.WriteLine($"✅ Zmieniono model planowania deserów na: {update.DessertPlanning.Model}");
            }

            if (updated)
            {
                return Ok(new
                {
                    message = "AI model settings updated successfully",
                    currentSettings = new
                    {
                        recipeScaling = _db.GetSetting("RecipeScaling_Model") ?? "gemini-2.5-flash",
                        dessertPlanning = _db.GetSetting("DessertPlanning_Model") ?? "gemini-2.5-flash"
                    }
                });
            }
            else
            {
                return BadRequest(new { error = "No valid settings provided" });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd aktualizacji ustawień modeli AI: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

// Request models
public class AIModelSettingsUpdate
{
    public RecipeScalingSettings? RecipeScaling { get; set; }
    public DessertPlanningSettings? DessertPlanning { get; set; }
}

public class RecipeScalingSettings
{
    public string? Model { get; set; }
}

public class DessertPlanningSettings
{
    public string? Model { get; set; }
}
