using Microsoft.AspNetCore.Mvc;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;
using RecipesAIHelper.Services;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MealPlansController : ControllerBase
{
    private readonly RecipeDbContext _db;
    private readonly AIServiceFactory _aiFactory;

    public MealPlansController(RecipeDbContext db, AIServiceFactory aiFactory)
    {
        _db = db;
        _aiFactory = aiFactory;
    }

    /// <summary>
    /// Get all meal plans
    /// GET /api/mealplans
    /// </summary>
    [HttpGet]
    public ActionResult<List<MealPlan>> GetAll()
    {
        try
        {
            var plans = _db.GetAllMealPlans();
            return Ok(plans);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd pobierania planów: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get meal plan by ID with full details (days and entries)
    /// GET /api/mealplans/{id}
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<MealPlan> GetById(int id)
    {
        try
        {
            var plan = _db.GetMealPlan(id);

            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            return Ok(plan);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd pobierania planu #{id}: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create new meal plan with days
    /// POST /api/mealplans
    /// Body: { "name": "Plan na styczeń", "startDate": "2025-01-01", "endDate": "2025-01-31", "numberOfDays": 7 }
    /// </summary>
    [HttpPost]
    public ActionResult<MealPlan> Create([FromBody] CreateMealPlanRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { error = "Plan name is required" });

            if (request.StartDate >= request.EndDate)
                return BadRequest(new { error = "Start date must be before end date" });

            if (request.NumberOfDays < 1 || request.NumberOfDays > 31)
                return BadRequest(new { error = "Number of days must be between 1 and 31" });

            Console.WriteLine($"📅 Tworzenie planu: {request.Name} ({request.NumberOfDays} dni)");

            // Create meal plan
            var mealPlan = new MealPlan
            {
                Name = request.Name,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var planId = _db.CreateMealPlan(mealPlan);
            mealPlan.Id = planId;

            // Create days for the plan
            var currentDate = request.StartDate;
            for (int i = 0; i < request.NumberOfDays; i++)
            {
                var dayOfWeek = (int)currentDate.DayOfWeek;
                // Convert Sunday from 0 to 6 (Monday = 0, Sunday = 6)
                dayOfWeek = dayOfWeek == 0 ? 6 : dayOfWeek - 1;

                var day = new MealPlanDay
                {
                    MealPlanId = planId,
                    DayOfWeek = dayOfWeek,
                    Date = currentDate,
                    CreatedAt = DateTime.Now
                };

                _db.CreateMealPlanDay(day);
                currentDate = currentDate.AddDays(1);
            }

            Console.WriteLine($"✅ Plan utworzony: {mealPlan.Name} (ID: {planId})");

            // Return full plan with days
            var createdPlan = _db.GetMealPlan(planId);
            return CreatedAtAction(nameof(GetById), new { id = planId }, createdPlan);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd tworzenia planu: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update meal plan
    /// PUT /api/mealplans/{id}
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult UpdatePlan(int id, [FromBody] UpdateMealPlanRequest request)
    {
        try
        {
            var plan = _db.GetMealPlan(id);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            if (!string.IsNullOrWhiteSpace(request.Name))
                plan.Name = request.Name;

            if (request.StartDate.HasValue)
                plan.StartDate = request.StartDate.Value;

            if (request.EndDate.HasValue)
                plan.EndDate = request.EndDate.Value;

            if (request.IsActive.HasValue)
                plan.IsActive = request.IsActive.Value;

            plan.UpdatedAt = DateTime.Now;

            var success = _db.UpdateMealPlan(plan);

            if (!success)
                return StatusCode(500, new { error = "Failed to update meal plan" });

            Console.WriteLine($"✅ Plan zaktualizowany: {plan.Name}");
            return Ok(new { message = "Meal plan updated successfully", plan });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd aktualizacji planu #{id}: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete meal plan (cascade deletes days and entries)
    /// DELETE /api/mealplans/{id}
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult DeletePlan(int id)
    {
        try
        {
            var plan = _db.GetMealPlan(id);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            var success = _db.DeleteMealPlan(id);

            if (!success)
                return StatusCode(500, new { error = "Failed to delete meal plan" });

            Console.WriteLine($"🗑️ Plan usunięty: {plan.Name}");
            return Ok(new { message = "Meal plan deleted successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd usuwania planu #{id}: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add recipe to a specific day
    /// POST /api/mealplans/{planId}/days/{dayId}/entries
    /// Body: { "recipeId": 5, "mealType": 1, "order": 0 }
    /// </summary>
    [HttpPost("{planId}/days/{dayId}/entries")]
    public ActionResult AddRecipeToDay(int planId, int dayId, [FromBody] AddRecipeRequest request)
    {
        try
        {
            // Validate meal plan exists
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            // Validate day exists and belongs to plan
            var days = _db.GetMealPlanDays(planId);
            var day = days.FirstOrDefault(d => d.Id == dayId);
            if (day == null)
                return NotFound(new { error = "Meal plan day not found" });

            // Validate recipe exists
            var recipes = _db.GetAllRecipes();
            var recipe = recipes.FirstOrDefault(r => r.Id == request.RecipeId);
            if (recipe == null)
                return NotFound(new { error = "Recipe not found" });

            // Create entry
            var entry = new MealPlanEntry
            {
                MealPlanDayId = dayId,
                RecipeId = request.RecipeId,
                MealType = request.MealType,
                Order = request.Order ?? 0,
                CreatedAt = DateTime.Now
            };

            var entryId = _db.CreateMealPlanEntry(entry);
            entry.Id = entryId;

            Console.WriteLine($"➕ Dodano przepis '{recipe.Name}' do dnia {day.Date:dd.MM} ({request.MealType})");

            return CreatedAtAction(nameof(GetById), new { id = planId }, entry);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd dodawania przepisu: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove recipe from day
    /// DELETE /api/mealplans/{planId}/days/{dayId}/entries/{entryId}
    /// </summary>
    [HttpDelete("{planId}/days/{dayId}/entries/{entryId}")]
    public ActionResult RemoveRecipeFromDay(int planId, int dayId, int entryId)
    {
        try
        {
            var success = _db.DeleteMealPlanEntry(entryId);

            if (!success)
                return NotFound(new { error = "Entry not found" });

            Console.WriteLine($"➖ Usunięto przepis z planu (entry ID: {entryId})");
            return Ok(new { message = "Recipe removed from meal plan" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd usuwania przepisu: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update recipe order (for drag & drop)
    /// PUT /api/mealplans/{planId}/days/{dayId}/entries/{entryId}/order
    /// Body: { "newOrder": 2 }
    /// </summary>
    [HttpPut("{planId}/days/{dayId}/entries/{entryId}/order")]
    public ActionResult UpdateEntryOrder(int planId, int dayId, int entryId, [FromBody] UpdateOrderRequest request)
    {
        try
        {
            var success = _db.UpdateMealPlanEntryOrder(entryId, request.NewOrder);

            if (!success)
                return NotFound(new { error = "Entry not found" });

            Console.WriteLine($"🔄 Zmieniono kolejność przepisu (entry ID: {entryId} → order: {request.NewOrder})");
            return Ok(new { message = "Order updated successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd zmiany kolejności: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Auto-generate random recipes for meal plan
    /// POST /api/mealplans/{planId}/auto-generate
    /// Body: { "categories": ["Sniadanie", "Obiad", "Kolacja"], "perDay": 1 }
    /// </summary>
    [HttpPost("{planId}/auto-generate")]
    public ActionResult AutoGenerate(int planId, [FromBody] AutoGenerateRequest request)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            if (plan.Days == null || plan.Days.Count == 0)
                return BadRequest(new { error = "Meal plan has no days" });

            Console.WriteLine($"🎲 Auto-generowanie dla planu: {plan.Name}");
            Console.WriteLine($"   Kategorie: {string.Join(", ", request.Categories)}");
            Console.WriteLine($"   Na dzień: {request.PerDay} z każdej kategorii");
            if (request.UseCalorieTarget)
            {
                Console.WriteLine($"   🎯 Optymalizacja kaloryczna: {request.TargetCalories} ± {request.CalorieMargin} kcal");
            }

            var addedCount = 0;
            var warnings = new List<string>();
            var missingDetails = new Dictionary<string, List<string>>(); // Day -> missing categories

            // For each day in the plan
            foreach (var day in plan.Days)
            {
                var dayName = GetDayOfWeekName(day.DayOfWeek);
                var dayDate = day.Date.ToString("dd.MM");

                if (request.UseCalorieTarget)
                {
                    // Calorie-optimized generation
                    var result = GenerateCalorieOptimizedDay(day, request, dayName, dayDate);
                    addedCount += result.AddedCount;
                    if (result.Warning != null)
                    {
                        warnings.Add(result.Warning);
                    }
                }
                else
                {
                    // Standard random generation
                    var result = GenerateStandardDay(day, request, dayName, dayDate);
                    addedCount += result.AddedCount;
                    if (result.MissingDetails != null && result.MissingDetails.Count > 0)
                    {
                        var dayKey = $"{dayName} ({dayDate})";
                        missingDetails[dayKey] = result.MissingDetails;
                    }
                }
            }

            // Build warning messages
            if (missingDetails.Count > 0)
            {
                warnings.Add($"⚠️ Nie znaleziono wystarczająco przepisów dla niektórych dni:");
                foreach (var kvp in missingDetails)
                {
                    warnings.Add($"  • {kvp.Key}: {string.Join(", ", kvp.Value)}");
                }
                warnings.Add("💡 Możesz dodać więcej przepisów ręcznie lub zduplikować istniejące.");
            }

            Console.WriteLine($"✅ Auto-generowanie zakończone: {addedCount} przepisów");
            if (warnings.Count > 0)
            {
                foreach (var warning in warnings)
                {
                    Console.WriteLine(warning);
                }
            }

            // Return updated plan
            var updatedPlan = _db.GetMealPlan(planId);
            return Ok(new
            {
                message = warnings.Count > 0
                    ? $"Dodano {addedCount} przepisów, ale wystąpiły ostrzeżenia"
                    : $"Auto-generowano {addedCount} przepisów",
                addedCount,
                warnings = warnings.Count > 0 ? warnings : null,
                plan = updatedPlan
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd auto-generowania: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private (int AddedCount, List<string>? MissingDetails) GenerateStandardDay(MealPlanDay day, AutoGenerateRequest request, string dayName, string dayDate)
    {
        var addedCount = 0;
        var missingDetails = new List<string>();

        // For each requested category
        foreach (var categoryStr in request.Categories)
        {
            if (!Enum.TryParse<MealType>(categoryStr, true, out var mealType))
            {
                Console.WriteLine($"⚠️ Nieznana kategoria: {categoryStr}");
                continue;
            }

            // Check how many recipes already exist for this day and category
            var existingCount = day.Entries?.Count(e => e.MealType == mealType) ?? 0;
            var needed = request.PerDay - existingCount;

            if (needed <= 0)
            {
                Console.WriteLine($"   ✓ {dayName} ({dayDate}) - {categoryStr}: już ma {existingCount} przepisów (pomijam)");
                continue;
            }

            Console.WriteLine($"   → {dayName} ({dayDate}) - {categoryStr}: ma {existingCount}, dodaję {needed}");

            // Get random recipes for this category (only what's needed)
            var randomRecipes = _db.GetRandomRecipesByMealType(mealType, needed);

            if (randomRecipes.Count < needed)
            {
                // Not enough recipes for this category
                var missing = needed - randomRecipes.Count;
                missingDetails.Add($"{categoryStr} (brakuje {missing})");
            }

            foreach (var recipe in randomRecipes)
            {
                var entry = new MealPlanEntry
                {
                    MealPlanDayId = day.Id,
                    RecipeId = recipe.Id,
                    MealType = mealType,
                    Order = 0, // Will be auto-sorted by meal type
                    CreatedAt = DateTime.Now
                };

                _db.CreateMealPlanEntry(entry);
                addedCount++;
            }
        }

        return (addedCount, missingDetails.Count > 0 ? missingDetails : null);
    }

    private (int AddedCount, string? Warning) GenerateCalorieOptimizedDay(MealPlanDay day, AutoGenerateRequest request, string dayName, string dayDate)
    {
        var addedCount = 0;
        var targetCalories = request.TargetCalories;
        var margin = request.CalorieMargin;

        Console.WriteLine($"   🎯 {dayName} ({dayDate}): Optymalizacja dla {targetCalories} ± {margin} kcal");

        // Calculate how many calories per category (distribute evenly)
        var caloriesPerCategory = targetCalories / request.Categories.Count;

        var selectedRecipes = new List<(Recipe Recipe, MealType MealType)>();
        var totalCalories = 0;

        foreach (var categoryStr in request.Categories)
        {
            if (!Enum.TryParse<MealType>(categoryStr, true, out var mealType))
            {
                Console.WriteLine($"⚠️ Nieznana kategoria: {categoryStr}");
                continue;
            }

            // Get recipes in calorie range for this category
            var minCal = Math.Max(0, caloriesPerCategory - margin);
            var maxCal = caloriesPerCategory + margin;

            var candidates = _db.GetRecipesByCalorieRange(mealType, minCal, maxCal);

            if (candidates.Count == 0)
            {
                // Fallback: get any random recipe of this type
                candidates = _db.GetRandomRecipesByMealType(mealType, 10);
            }

            if (candidates.Count == 0)
            {
                Console.WriteLine($"   ⚠️ Brak przepisów dla kategorii {categoryStr}");
                continue;
            }

            // Select recipe closest to target
            var bestRecipe = candidates.OrderBy(r => Math.Abs(r.Calories - caloriesPerCategory)).FirstOrDefault();

            if (bestRecipe != null)
            {
                selectedRecipes.Add((bestRecipe, mealType));
                totalCalories += bestRecipe.Calories;
                Console.WriteLine($"      ✓ {categoryStr}: {bestRecipe.Name} ({bestRecipe.Calories} kcal)");
            }
        }

        // Check if total is within acceptable range
        var difference = Math.Abs(totalCalories - targetCalories);
        var isWithinRange = difference <= margin;

        Console.WriteLine($"   📊 Suma kalorii: {totalCalories} kcal (różnica: {(totalCalories - targetCalories):+#;-#;0} kcal)");

        // Add selected recipes to the plan
        foreach (var (recipe, mealType) in selectedRecipes)
        {
            var entry = new MealPlanEntry
            {
                MealPlanDayId = day.Id,
                RecipeId = recipe.Id,
                MealType = mealType,
                Order = 0,
                CreatedAt = DateTime.Now
            };

            _db.CreateMealPlanEntry(entry);
            addedCount++;
        }

        string? warning = null;
        if (!isWithinRange)
        {
            warning = $"⚠️ {dayName} ({dayDate}): Suma kalorii ({totalCalories} kcal) wykracza poza zakres {targetCalories} ± {margin} kcal";
        }

        return (addedCount, warning);
    }

    /// <summary>
    /// Get saved shopping list for meal plan
    /// GET /api/mealplans/{planId}/shopping-list
    /// </summary>
    [HttpGet("{planId}/shopping-list")]
    public ActionResult GetShoppingList(int planId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            var shoppingList = _db.GetShoppingListByMealPlan(planId);
            if (shoppingList == null)
                return NotFound(new { error = "No shopping list found for this meal plan" });

            return Ok(new
            {
                id = shoppingList.Id,
                mealPlanId = shoppingList.MealPlanId,
                mealPlanName = plan.Name,
                generatedAt = shoppingList.GeneratedAt,
                itemCount = shoppingList.Items?.Count ?? 0,
                items = shoppingList.Items
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd pobierania listy zakupowej: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate shopping list for meal plan
    /// POST /api/mealplans/{planId}/shopping-list
    /// </summary>
    [HttpPost("{planId}/shopping-list")]
    public async Task<ActionResult> GenerateShoppingList(int planId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            if (plan.Days == null || plan.Days.Count == 0)
                return BadRequest(new { error = "Meal plan has no days" });

            Console.WriteLine($"🛒 Generowanie listy zakupowej dla planu: {plan.Name}");

            // Collect all recipes from all days
            var allRecipes = new List<Recipe>();
            foreach (var day in plan.Days)
            {
                if (day.Entries != null)
                {
                    foreach (var entry in day.Entries)
                    {
                        if (entry.Recipe != null)
                        {
                            allRecipes.Add(entry.Recipe);
                        }
                    }
                }
            }

            if (allRecipes.Count == 0)
                return BadRequest(new { error = "Meal plan has no recipes" });

            Console.WriteLine($"   Znaleziono {allRecipes.Count} przepisów w planie");

            // Get active AI provider
            var activeProvider = _aiFactory.GetActiveProvider();
            if (activeProvider == null)
                return BadRequest(new { error = "No active AI provider configured" });

            // Get API key from Settings based on provider name
            string? apiKey = null;
            var providerNameLower = activeProvider.Name.ToLowerInvariant();
            if (providerNameLower == "openai")
            {
                apiKey = _db.GetSetting("OpenAI_ApiKey");
            }
            else if (providerNameLower == "gemini" || providerNameLower == "google")
            {
                apiKey = _db.GetSetting("Gemini_ApiKey");
            }

            if (string.IsNullOrEmpty(apiKey))
                return BadRequest(new { error = "API key not configured for active provider. Configure it in Settings." });

            // Create shopping list service
            var shoppingListService = new ShoppingListService(apiKey, activeProvider.Model);

            // Generate shopping list
            var shoppingList = await shoppingListService.GenerateShoppingListAsync(allRecipes);

            if (shoppingList == null || shoppingList.Items.Count == 0)
                return StatusCode(500, new { error = "Failed to generate shopping list" });

            Console.WriteLine($"✅ Lista zakupowa wygenerowana: {shoppingList.Items.Count} pozycji");

            // Save shopping list to database
            var itemsJson = System.Text.Json.JsonSerializer.Serialize(shoppingList.Items);
            var savedList = _db.SaveShoppingList(planId, itemsJson);

            Console.WriteLine($"💾 Lista zakupowa zapisana w bazie (ID: {savedList.Id})");

            return Ok(new
            {
                message = "Shopping list generated and saved successfully",
                id = savedList.Id,
                mealPlanName = plan.Name,
                recipeCount = allRecipes.Count,
                itemCount = shoppingList.Items.Count,
                generatedAt = savedList.GeneratedAt,
                items = shoppingList.Items
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd generowania listy zakupowej: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private string GetDayOfWeekName(int dayOfWeek)
    {
        return dayOfWeek switch
        {
            0 => "Poniedziałek",
            1 => "Wtorek",
            2 => "Środa",
            3 => "Czwartek",
            4 => "Piątek",
            5 => "Sobota",
            6 => "Niedziela",
            _ => $"Dzień {dayOfWeek}"
        };
    }
}

// Request models
public class CreateMealPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int NumberOfDays { get; set; } = 7;
}

public class UpdateMealPlanRequest
{
    public string? Name { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsActive { get; set; }
}

public class AddRecipeRequest
{
    public int RecipeId { get; set; }
    public MealType MealType { get; set; }
    public int? Order { get; set; }
}

public class UpdateOrderRequest
{
    public int NewOrder { get; set; }
}

public class AutoGenerateRequest
{
    public List<string> Categories { get; set; } = new() { "Sniadanie", "Obiad", "Kolacja" };
    public int PerDay { get; set; } = 1;
    public bool UseCalorieTarget { get; set; } = false;
    public int TargetCalories { get; set; } = 1800;
    public int CalorieMargin { get; set; } = 200;
}
