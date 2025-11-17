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
    /// Get AI model settings for meal planning (recipe scaling, shopping list)
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
                    provider = _db.GetSetting("RecipeScaling_Provider") ?? "Gemini",
                    model = _db.GetSetting("RecipeScaling_Model") ?? "gemini-2.5-flash"
                },
                shoppingList = new
                {
                    provider = _db.GetSetting("ShoppingList_Provider") ?? "Gemini",
                    model = _db.GetSetting("ShoppingList_Model") ?? "gemini-2.5-flash"
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
    /// Update AI model settings for meal planning (recipe scaling, shopping list)
    /// PUT /api/aimodelsettings
    /// </summary>
    [HttpPut]
    public ActionResult UpdateAIModelSettings([FromBody] AIModelSettingsUpdate update)
    {
        try
        {
            var updated = false;

            // Update recipe scaling provider
            if (!string.IsNullOrEmpty(update.RecipeScaling?.Provider))
            {
                _db.UpsertSetting("RecipeScaling_Provider", update.RecipeScaling.Provider, "string",
                    "Provider AI do skalowania składników przepisów (OpenAI lub Gemini)");
                updated = true;
                Console.WriteLine($"✅ Zmieniono providera skalowania przepisów na: {update.RecipeScaling.Provider}");
            }

            // Update recipe scaling model
            if (!string.IsNullOrEmpty(update.RecipeScaling?.Model))
            {
                _db.UpsertSetting("RecipeScaling_Model", update.RecipeScaling.Model, "string",
                    "Model AI do skalowania składników przepisów");
                updated = true;
                Console.WriteLine($"✅ Zmieniono model skalowania przepisów na: {update.RecipeScaling.Model}");
            }

            // Update shopping list provider
            if (!string.IsNullOrEmpty(update.ShoppingList?.Provider))
            {
                _db.UpsertSetting("ShoppingList_Provider", update.ShoppingList.Provider, "string",
                    "Provider AI do generowania listy zakupów (OpenAI lub Gemini)");
                updated = true;
                Console.WriteLine($"✅ Zmieniono providera listy zakupów na: {update.ShoppingList.Provider}");
            }

            // Update shopping list model
            if (!string.IsNullOrEmpty(update.ShoppingList?.Model))
            {
                _db.UpsertSetting("ShoppingList_Model", update.ShoppingList.Model, "string",
                    "Model AI do generowania listy zakupów");
                updated = true;
                Console.WriteLine($"✅ Zmieniono model listy zakupów na: {update.ShoppingList.Model}");
            }

            if (updated)
            {
                return Ok(new
                {
                    message = "AI model settings updated successfully",
                    currentSettings = new
                    {
                        recipeScaling = new
                        {
                            provider = _db.GetSetting("RecipeScaling_Provider") ?? "Gemini",
                            model = _db.GetSetting("RecipeScaling_Model") ?? "gemini-2.5-flash"
                        },
                        shoppingList = new
                        {
                            provider = _db.GetSetting("ShoppingList_Provider") ?? "Gemini",
                            model = _db.GetSetting("ShoppingList_Model") ?? "gemini-2.5-flash"
                        }
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
    public ShoppingListSettings? ShoppingList { get; set; }
}

public class RecipeScalingSettings
{
    public string? Provider { get; set; }
    public string? Model { get; set; }
}

public class ShoppingListSettings
{
    public string? Provider { get; set; }
    public string? Model { get; set; }
}
