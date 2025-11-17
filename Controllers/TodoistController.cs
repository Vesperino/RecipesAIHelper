using Microsoft.AspNetCore.Mvc;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;
using RecipesAIHelper.Services;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodoistController : ControllerBase
{
    private readonly RecipeDbContext _db;

    public TodoistController(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get Todoist settings (API key masked)
    /// GET /api/todoist/settings
    /// </summary>
    [HttpGet("settings")]
    public ActionResult GetSettings()
    {
        try
        {
            var apiKey = _db.GetSetting("Todoist_ApiKey");
            var isConfigured = !string.IsNullOrEmpty(apiKey);

            return Ok(new
            {
                isConfigured = isConfigured,
                apiKey = MaskApiKey(apiKey)
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd pobierania ustawień Todoist: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update Todoist API key
    /// PUT /api/todoist/settings
    /// Body: { "apiKey": "your-api-key" }
    /// </summary>
    [HttpPut("settings")]
    public ActionResult UpdateSettings([FromBody] TodoistSettingsUpdate update)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(update.ApiKey))
            {
                return BadRequest(new { error = "API key is required" });
            }

            // Don't update if masked
            if (update.ApiKey == "***")
            {
                return Ok(new { message = "API key not changed (masked value)" });
            }

            _db.UpsertSetting("Todoist_ApiKey", update.ApiKey, "string", "Klucz API Todoist dla exportu list zakupowych");
            Console.WriteLine("✅ Zaktualizowano klucz API Todoist");

            return Ok(new { message = "Todoist API key updated successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd aktualizacji klucza Todoist: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Test Todoist connection
    /// POST /api/todoist/test
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult> TestConnection()
    {
        try
        {
            var apiKey = _db.GetSetting("Todoist_ApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest(new { error = "Todoist API key not configured" });
            }

            var service = new TodoistService(apiKey);

            // Test by creating a test project and immediately checking if it was created
            var testProjectName = $"🧪 Test - {DateTime.Now:HH:mm:ss}";
            Console.WriteLine($"🧪 Testowanie połączenia z Todoist...");
            Console.WriteLine($"   Tworzenie testowego projektu: {testProjectName}");

            var testItems = new List<ShoppingListItem>
            {
                new ShoppingListItem { Name = "Test Item", Quantity = "1 szt", Category = "test" }
            };

            var result = await service.ExportShoppingListAsync(
                "Test Connection",
                DateTime.Now,
                DateTime.Now,
                testItems
            );

            if (result != null && result.TasksCreated > 0)
            {
                Console.WriteLine($"✅ Test zakończony sukcesem! Projekt ID: {result.ProjectId}");
                return Ok(new
                {
                    message = "Connection successful",
                    projectId = result.ProjectId,
                    projectUrl = result.ProjectUrl
                });
            }
            else
            {
                return StatusCode(500, new { error = "Failed to create test project in Todoist" });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd testu Todoist: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Export shopping list to Todoist
    /// POST /api/todoist/export/{mealPlanId}
    /// </summary>
    [HttpPost("export/{mealPlanId}")]
    public async Task<ActionResult> ExportShoppingList(int mealPlanId)
    {
        try
        {
            // 1. Get Todoist API key
            var apiKey = _db.GetSetting("Todoist_ApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest(new
                {
                    error = "Todoist API key not configured",
                    hint = "Please configure your Todoist API key in Settings"
                });
            }

            // 2. Get meal plan
            var mealPlan = _db.GetMealPlan(mealPlanId);
            if (mealPlan == null)
            {
                return NotFound(new { error = "Meal plan not found" });
            }

            // 3. Get shopping list
            var shoppingList = _db.GetShoppingListByMealPlan(mealPlanId);
            if (shoppingList == null || shoppingList.Items == null || shoppingList.Items.Count == 0)
            {
                return BadRequest(new
                {
                    error = "No shopping list found for this meal plan",
                    hint = "Please generate a shopping list first"
                });
            }

            Console.WriteLine($"📋 Eksportowanie listy zakupowej do Todoist");
            Console.WriteLine($"   Plan: {mealPlan.Name}");
            Console.WriteLine($"   Okres: {mealPlan.StartDate:dd.MM.yyyy} - {mealPlan.EndDate:dd.MM.yyyy}");
            Console.WriteLine($"   Liczba pozycji: {shoppingList.Items.Count}");

            // 4. Create Todoist service
            var todoistService = new TodoistService(apiKey);

            // 5. Export to Todoist
            var result = await todoistService.ExportShoppingListAsync(
                mealPlan.Name,
                mealPlan.StartDate,
                mealPlan.EndDate,
                shoppingList.Items
            );

            if (result == null)
            {
                return StatusCode(500, new { error = "Failed to export to Todoist" });
            }

            Console.WriteLine($"✅ Eksport zakończony sukcesem!");
            Console.WriteLine($"   Projekt: {result.ProjectName}");
            Console.WriteLine($"   URL: {result.ProjectUrl}");
            Console.WriteLine($"   Zadania: {result.TasksCreated}/{result.TotalItems}");

            return Ok(new
            {
                message = "Shopping list exported to Todoist successfully",
                projectId = result.ProjectId,
                projectName = result.ProjectName,
                projectUrl = result.ProjectUrl,
                tasksCreated = result.TasksCreated,
                totalItems = result.TotalItems
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd eksportu do Todoist: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
            return "***";

        return apiKey.Substring(0, 4) + "***" + apiKey.Substring(apiKey.Length - 4);
    }
}

// Request models
public class TodoistSettingsUpdate
{
    public string ApiKey { get; set; } = string.Empty;
}
