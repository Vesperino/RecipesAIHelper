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
    public async Task<ActionResult> AutoGenerate(int planId, [FromBody] AutoGenerateRequest request)
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
            var usedRecipeIds = new HashSet<int>(); // Track used recipes across all days

            // For each day in the plan
            foreach (var day in plan.Days)
            {
                var dayName = GetDayOfWeekName(day.DayOfWeek);
                var dayDate = day.Date.ToString("dd.MM");

                if (request.UseCalorieTarget)
                {
                    // Calorie-optimized generation
                    var result = GenerateCalorieOptimizedDay(day, request, dayName, dayDate, usedRecipeIds);
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

            // Auto-scale recipes if plan has persons
            var persons = _db.GetMealPlanPersons(planId);
            var scaledCount = 0;
            var scalingErrors = new List<string>();

            if (persons.Count > 0 && addedCount > 0)
            {
                Console.WriteLine($"🔧 Wykryto {persons.Count} osób w planie - automatyczne skalowanie przepisów...");

                // Get active AI provider
                var activeProvider = _aiFactory.GetActiveProvider();
                if (activeProvider != null)
                {
                    // Get API key
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

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        // Reload plan to get fresh entries
                        var freshPlan = _db.GetMealPlan(planId);
                        if (freshPlan?.Days != null)
                        {
                            // Collect all entries
                            var allEntries = new List<MealPlanEntry>();
                            foreach (var day in freshPlan.Days)
                            {
                                if (day.Entries != null)
                                {
                                    allEntries.AddRange(day.Entries);
                                }
                            }

                            // Scale each entry
                            var avgCalories = persons.Average(p => p.TargetCalories);

                            foreach (var entry in allEntries)
                            {
                                if (entry.Recipe == null) continue;

                                try
                                {
                                    var isDessert = entry.MealType == MealType.Deser;

                                    if (isDessert)
                                    {
                                        var dessertService = new DessertPlanningService(apiKey, activeProvider.Model);
                                        var dessertPlan = await dessertService.PlanDessertAsync(entry.Recipe, persons);

                                        foreach (var person in persons)
                                        {
                                            var scaledRecipe = new MealPlanRecipe
                                            {
                                                MealPlanEntryId = entry.Id,
                                                PersonId = person.Id,
                                                BaseRecipeId = entry.Recipe.Id,
                                                ScalingFactor = 1.0,
                                                ScaledIngredients = new List<string> { entry.Recipe.Ingredients },
                                                ScaledCalories = dessertPlan.PortionCalories,
                                                ScaledProtein = entry.Recipe.Protein,
                                                ScaledCarbs = entry.Recipe.Carbohydrates,
                                                ScaledFat = entry.Recipe.Fat,
                                                CreatedAt = DateTime.Now
                                            };
                                            _db.CreateMealPlanRecipe(scaledRecipe);
                                        }

                                        Console.WriteLine($"   🍰 {entry.Recipe.Name}: przeskalowano jako deser");
                                        scaledCount++;
                                    }
                                    else
                                    {
                                        var scalingService = new RecipeScalingService(apiKey, activeProvider.Model);

                                        foreach (var person in persons)
                                        {
                                            var scalingFactor = person.TargetCalories / avgCalories;
                                            var scaledIngredients = await scalingService.ScaleRecipeIngredientsAsync(
                                                entry.Recipe,
                                                scalingFactor,
                                                entry.MealType
                                            );

                                            if (scaledIngredients.Count == 0)
                                            {
                                                scaledIngredients = new List<string> { entry.Recipe.Ingredients };
                                            }

                                            var scaledCalories = (int)Math.Round(entry.Recipe.Calories * scalingFactor);

                                            var scaledRecipe = new MealPlanRecipe
                                            {
                                                MealPlanEntryId = entry.Id,
                                                PersonId = person.Id,
                                                BaseRecipeId = entry.Recipe.Id,
                                                ScalingFactor = scalingFactor,
                                                ScaledIngredients = scaledIngredients,
                                                ScaledCalories = scaledCalories,
                                                ScaledProtein = entry.Recipe.Protein * scalingFactor,
                                                ScaledCarbs = entry.Recipe.Carbohydrates * scalingFactor,
                                                ScaledFat = entry.Recipe.Fat * scalingFactor,
                                                CreatedAt = DateTime.Now
                                            };
                                            _db.CreateMealPlanRecipe(scaledRecipe);
                                        }

                                        Console.WriteLine($"   ✓ {entry.Recipe.Name}: przeskalowano dla {persons.Count} osób");
                                        scaledCount++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    var errorMsg = $"Błąd skalowania '{entry.Recipe.Name}': {ex.Message}";
                                    scalingErrors.Add(errorMsg);
                                    Console.WriteLine($"   ⚠️ {errorMsg}");
                                }
                            }

                            if (scaledCount > 0)
                            {
                                Console.WriteLine($"✅ Automatyczne skalowanie zakończone: {scaledCount}/{allEntries.Count} przepisów");
                            }
                        }
                    }
                    else
                    {
                        scalingErrors.Add("Brak klucza API - pomiń automatyczne skalowanie");
                        Console.WriteLine("⚠️ Brak klucza API - pomijam automatyczne skalowanie");
                    }
                }
                else
                {
                    scalingErrors.Add("Brak aktywnego providera AI - pomiń automatyczne skalowanie");
                    Console.WriteLine("⚠️ Brak aktywnego providera AI - pomijam automatyczne skalowanie");
                }
            }

            // Return updated plan
            var updatedPlan = _db.GetMealPlan(planId);

            var finalMessage = warnings.Count > 0
                ? $"Dodano {addedCount} przepisów, ale wystąpiły ostrzeżenia"
                : $"Auto-generowano {addedCount} przepisów";

            if (scaledCount > 0)
            {
                finalMessage += $" i automatycznie przeskalowano {scaledCount} dla {persons.Count} osób";
            }

            if (scalingErrors.Count > 0)
            {
                warnings.AddRange(scalingErrors);
            }

            return Ok(new
            {
                message = finalMessage,
                addedCount,
                scaledCount,
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

    private (int AddedCount, string? Warning) GenerateCalorieOptimizedDay(MealPlanDay day, AutoGenerateRequest request, string dayName, string dayDate, HashSet<int> usedRecipeIds)
    {
        var addedCount = 0;
        var targetCalories = request.TargetCalories;
        var margin = request.CalorieMargin;

        Console.WriteLine($"   🎯 {dayName} ({dayDate}): Optymalizacja dla {targetCalories} ± {margin} kcal");

        // Calculate how many calories per category (distribute evenly)
        var caloriesPerCategory = targetCalories / request.Categories.Count;

        var selectedRecipes = new List<(Recipe Recipe, MealType MealType)>();
        var totalCalories = 0;
        var random = new Random();

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

            var candidates = _db.GetRecipesByCalorieRange(mealType, minCal, maxCal)
                .Where(r => !usedRecipeIds.Contains(r.Id)) // Filter out already used recipes
                .ToList();

            if (candidates.Count == 0)
            {
                // Fallback: get any random recipe of this type (excluding used ones)
                candidates = _db.GetRandomRecipesByMealType(mealType, 20)
                    .Where(r => !usedRecipeIds.Contains(r.Id))
                    .ToList();
            }

            if (candidates.Count == 0)
            {
                Console.WriteLine($"   ⚠️ Brak nieużywanych przepisów dla kategorii {categoryStr}");
                continue;
            }

            // Select recipe closest to target with randomization to ensure variety
            // Take top 3 closest matches and randomly select one
            var topMatches = candidates
                .OrderBy(r => Math.Abs(r.Calories - caloriesPerCategory))
                .Take(Math.Min(3, candidates.Count)) // Take up to 3, but not more than available
                .ToList();

            var bestRecipe = topMatches[random.Next(topMatches.Count)];

            if (bestRecipe != null)
            {
                selectedRecipes.Add((bestRecipe, mealType));
                totalCalories += bestRecipe.Calories;
                usedRecipeIds.Add(bestRecipe.Id); // Mark recipe as used
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

    /// <summary>
    /// Get all persons in a meal plan
    /// GET /api/mealplans/{planId}/persons
    /// </summary>
    [HttpGet("{planId}/persons")]
    public ActionResult<List<MealPlanPerson>> GetPersons(int planId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            var persons = _db.GetMealPlanPersons(planId);
            return Ok(persons);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd pobierania osób: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add person to meal plan
    /// POST /api/mealplans/{planId}/persons
    /// Body: { "name": "Magda", "targetCalories": 2100 }
    /// </summary>
    [HttpPost("{planId}/persons")]
    public ActionResult<MealPlanPerson> AddPerson(int planId, [FromBody] AddPersonRequest request)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { error = "Person name is required" });

            if (request.TargetCalories < 1000 || request.TargetCalories > 5000)
                return BadRequest(new { error = "Target calories must be between 1000 and 5000" });

            // Check if plan already has max persons (5)
            var existingPersons = _db.GetMealPlanPersons(planId);
            if (existingPersons.Count >= 5)
                return BadRequest(new { error = "Maximum 5 persons per meal plan" });

            // Check if person name already exists
            if (existingPersons.Any(p => p.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new { error = "Person with this name already exists in the plan" });

            var person = new MealPlanPerson
            {
                MealPlanId = planId,
                Name = request.Name,
                TargetCalories = request.TargetCalories,
                SortOrder = existingPersons.Count, // Add to end
                CreatedAt = DateTime.Now
            };

            var personId = _db.CreateMealPlanPerson(person);
            person.Id = personId;

            Console.WriteLine($"👤 Dodano osobę: {person.Name} ({person.TargetCalories} kcal/dzień)");

            return CreatedAtAction(nameof(GetPersons), new { planId }, person);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd dodawania osoby: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update person in meal plan
    /// PUT /api/mealplans/{planId}/persons/{personId}
    /// Body: { "name": "Magda", "targetCalories": 2200 }
    /// </summary>
    [HttpPut("{planId}/persons/{personId}")]
    public ActionResult UpdatePerson(int planId, int personId, [FromBody] UpdatePersonRequest request)
    {
        try
        {
            var person = _db.GetMealPlanPersons(planId).FirstOrDefault(p => p.Id == personId);
            if (person == null)
                return NotFound(new { error = "Person not found" });

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                // Check for duplicate names
                var existingPersons = _db.GetMealPlanPersons(planId);
                if (existingPersons.Any(p => p.Id != personId && p.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase)))
                    return BadRequest(new { error = "Person with this name already exists in the plan" });

                person.Name = request.Name;
            }

            if (request.TargetCalories.HasValue)
            {
                if (request.TargetCalories.Value < 1000 || request.TargetCalories.Value > 5000)
                    return BadRequest(new { error = "Target calories must be between 1000 and 5000" });

                person.TargetCalories = request.TargetCalories.Value;
            }

            var success = _db.UpdateMealPlanPerson(person);
            if (!success)
                return StatusCode(500, new { error = "Failed to update person" });

            Console.WriteLine($"✏️ Zaktualizowano osobę: {person.Name} ({person.TargetCalories} kcal/dzień)");

            return Ok(new { message = "Person updated successfully", person });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd aktualizacji osoby: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete person from meal plan (cascade deletes their scaled recipes)
    /// DELETE /api/mealplans/{planId}/persons/{personId}
    /// </summary>
    [HttpDelete("{planId}/persons/{personId}")]
    public ActionResult DeletePerson(int planId, int personId)
    {
        try
        {
            var person = _db.GetMealPlanPersons(planId).FirstOrDefault(p => p.Id == personId);
            if (person == null)
                return NotFound(new { error = "Person not found" });

            var success = _db.DeleteMealPlanPerson(personId);
            if (!success)
                return StatusCode(500, new { error = "Failed to delete person" });

            Console.WriteLine($"🗑️ Usunięto osobę: {person.Name}");

            return Ok(new { message = "Person deleted successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd usuwania osoby: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Scale recipe for all persons in meal plan
    /// POST /api/mealplans/{planId}/entries/{entryId}/scale
    /// </summary>
    [HttpPost("{planId}/entries/{entryId}/scale")]
    public async Task<ActionResult> ScaleRecipeForAllPersons(int planId, int entryId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            var persons = _db.GetMealPlanPersons(planId);
            if (persons.Count == 0)
                return BadRequest(new { error = "No persons in meal plan. Add persons first." });

            // Get the entry and its recipe
            MealPlanEntry? entry = null;
            if (plan.Days != null)
            {
                foreach (var day in plan.Days)
                {
                    if (day.Entries != null)
                    {
                        entry = day.Entries.FirstOrDefault(e => e.Id == entryId);
                        if (entry != null) break;
                    }
                }
            }

            if (entry == null)
                return NotFound(new { error = "Entry not found" });

            if (entry.Recipe == null)
                return BadRequest(new { error = "Entry has no recipe" });

            var recipe = entry.Recipe;
            Console.WriteLine($"📊 Skalowanie przepisu '{recipe.Name}' dla {persons.Count} osób...");

            // Get active AI provider for scaling service
            var activeProvider = _aiFactory.GetActiveProvider();
            if (activeProvider == null)
                return BadRequest(new { error = "No active AI provider configured" });

            // Get API key
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
                return BadRequest(new { error = "API key not configured. Configure it in Settings." });

            // Check if this is a dessert
            var isDessert = entry.MealType == MealType.Deser;

            if (isDessert)
            {
                // Use DessertPlanningService
                var dessertService = new DessertPlanningService(apiKey, activeProvider.Model);
                var dessertPlan = await dessertService.PlanDessertAsync(recipe, persons);

                Console.WriteLine($"🍰 Plan deseru: {dessertPlan.TotalPortions} porcji, {dessertPlan.DaysToSpread} dni");
                Console.WriteLine($"   {dessertPlan.Explanation}");

                // For desserts, create same portion for everyone
                foreach (var person in persons)
                {
                    var scaledRecipe = new MealPlanRecipe
                    {
                        MealPlanEntryId = entryId,
                        PersonId = person.Id,
                        BaseRecipeId = recipe.Id,
                        ScalingFactor = 1.0, // Same portion for everyone
                        ScaledIngredients = new List<string> { recipe.Ingredients },
                        ScaledCalories = dessertPlan.PortionCalories,
                        ScaledProtein = recipe.Protein,
                        ScaledCarbs = recipe.Carbohydrates,
                        ScaledFat = recipe.Fat,
                        CreatedAt = DateTime.Now
                    };

                    _db.CreateMealPlanRecipe(scaledRecipe);
                    Console.WriteLine($"   ✓ {person.Name}: {dessertPlan.PortionCalories} kcal (1.0x)");
                }

                return Ok(new
                {
                    message = "Dessert planned successfully",
                    recipeName = recipe.Name,
                    isDessert = true,
                    dessertPlan = new
                    {
                        totalPortions = dessertPlan.TotalPortions,
                        portionCalories = dessertPlan.PortionCalories,
                        daysToSpread = dessertPlan.DaysToSpread,
                        explanation = dessertPlan.Explanation
                    },
                    scaledRecipes = persons.Count
                });
            }
            else
            {
                // Regular recipe - scale for each person
                var scalingService = new RecipeScalingService(apiKey, activeProvider.Model);

                // Calculate average target calories to use as baseline
                var avgCalories = persons.Average(p => p.TargetCalories);
                var baselineCalories = recipe.Calories;

                var scaledCount = 0;

                foreach (var person in persons)
                {
                    // Calculate scaling factor based on person's calorie target
                    var scalingFactor = person.TargetCalories / avgCalories;

                    Console.WriteLine($"   → {person.Name}: współczynnik {scalingFactor:F2}");

                    // Scale ingredients using AI
                    var scaledIngredients = await scalingService.ScaleRecipeIngredientsAsync(
                        recipe,
                        scalingFactor,
                        entry.MealType
                    );

                    if (scaledIngredients.Count == 0)
                    {
                        Console.WriteLine($"   ⚠️ Nie udało się przeskalować dla {person.Name}, używam bazowego przepisu");
                        scaledIngredients = new List<string> { recipe.Ingredients };
                    }

                    // Calculate scaled nutrition
                    var scaledCalories = (int)Math.Round(recipe.Calories * scalingFactor);
                    var scaledProtein = recipe.Protein * scalingFactor;
                    var scaledCarbs = recipe.Carbohydrates * scalingFactor;
                    var scaledFat = recipe.Fat * scalingFactor;

                    var scaledRecipe = new MealPlanRecipe
                    {
                        MealPlanEntryId = entryId,
                        PersonId = person.Id,
                        BaseRecipeId = recipe.Id,
                        ScalingFactor = scalingFactor,
                        ScaledIngredients = scaledIngredients,
                        ScaledCalories = scaledCalories,
                        ScaledProtein = scaledProtein,
                        ScaledCarbs = scaledCarbs,
                        ScaledFat = scaledFat,
                        CreatedAt = DateTime.Now
                    };

                    _db.CreateMealPlanRecipe(scaledRecipe);
                    scaledCount++;

                    Console.WriteLine($"   ✓ {person.Name}: {scaledCalories} kcal ({scalingFactor:F2}x)");
                }

                return Ok(new
                {
                    message = "Recipe scaled successfully",
                    recipeName = recipe.Name,
                    isDessert = false,
                    scaledRecipes = scaledCount,
                    persons = persons.Select(p => new
                    {
                        name = p.Name,
                        targetCalories = p.TargetCalories
                    })
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd skalowania przepisu: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get scaled recipes for a specific entry
    /// GET /api/mealplans/{planId}/entries/{entryId}/scaled
    /// </summary>
    [HttpGet("{planId}/entries/{entryId}/scaled")]
    public ActionResult GetScaledRecipes(int planId, int entryId)
    {
        try
        {
            var scaledRecipes = _db.GetMealPlanRecipes(entryId);
            return Ok(scaledRecipes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd pobierania przeskalowanych przepisów: {ex.Message}");
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

public class AddPersonRequest
{
    public string Name { get; set; } = string.Empty;
    public int TargetCalories { get; set; }
}

public class UpdatePersonRequest
{
    public string? Name { get; set; }
    public int? TargetCalories { get; set; }
}
