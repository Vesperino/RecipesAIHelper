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
    private readonly RecipeScalingServiceFactory _scalingFactory;
    private readonly ShoppingListServiceFactory _shoppingListFactory;

    public MealPlansController(RecipeDbContext db, AIServiceFactory aiFactory)
    {
        _db = db;
        _aiFactory = aiFactory;
        _scalingFactory = new RecipeScalingServiceFactory(db);
        _shoppingListFactory = new ShoppingListServiceFactory(db);
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

            // Check if plan has persons - if yes, override calorie target with max person calories
            var planPersons = _db.GetMealPlanPersons(planId);
            if (planPersons.Count > 0)
            {
                var maxCalories = planPersons.Max(p => p.TargetCalories);
                request.TargetCalories = maxCalories;
                request.UseCalorieTarget = true; // Force calorie optimization
                Console.WriteLine($"   👥 Plan ma {planPersons.Count} osób - generuję dla najwyższej kaloryczności: {maxCalories} kcal");
            }
            else if (request.UseCalorieTarget)
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

            // Auto-scale recipes if plan has persons (and skipScaling is false)
            var persons = _db.GetMealPlanPersons(planId);
            var scaledCount = 0;
            var scalingErrors = new List<string>();

            if (persons.Count > 0 && addedCount > 0 && !request.SkipScaling)
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
                            Console.WriteLine($"   🎯 NOWA FILOZOFIA: Per-day intelligent scaling - każda osoba dostanie DOKŁADNIE swoje kalorie");

                            // Process day by day to calculate person-specific daily scaling factors
                            foreach (var day in freshPlan.Days)
                            {
                                if (day.Entries == null || day.Entries.Count == 0) continue;

                                // Calculate daily calorie sums separately for scalable and non-scalable recipes
                                var doNotScaleCalories = day.Entries
                                    .Where(e => e.Recipe != null && e.Recipe.DoNotScale)
                                    .Sum(e => e.Recipe!.Calories);

                                var scalableCalories = day.Entries
                                    .Where(e => e.Recipe != null && !e.Recipe.DoNotScale)
                                    .Sum(e => e.Recipe!.Calories);

                                var dailyCaloriesSum = doNotScaleCalories + scalableCalories;

                                var dayName = day.Date.ToString("yyyy-MM-dd");
                                Console.WriteLine($"   📅 Dzień: {dayName} (suma bazowa: {dailyCaloriesSum} kcal, DoNotScale: {doNotScaleCalories} kcal, Skalowalne: {scalableCalories} kcal)");

                                if (dailyCaloriesSum == 0)
                                {
                                    Console.WriteLine($"      ⚠️ Pomiń dzień bez kalorii");
                                    continue;
                                }

                                // Process each person for this day
                                foreach (var person in persons)
                                {
                                    // Check if daily calories are within ±50 kcal tolerance
                                    var calorieDifference = Math.Abs(dailyCaloriesSum - person.TargetCalories);
                                    var withinTolerance = calorieDifference <= 50;

                                    // Calculate person-specific day scaling factor
                                    // For scalable recipes only: (targetCalories - doNotScaleCalories) / scalableCalories
                                    double dayScalingFactor;
                                    if (withinTolerance)
                                    {
                                        dayScalingFactor = 1.0;
                                    }
                                    else if (scalableCalories == 0)
                                    {
                                        // All recipes are DoNotScale - no scaling possible
                                        dayScalingFactor = 1.0;
                                        Console.WriteLine($"      ⚠️ {person.Name}: Wszystkie przepisy są DoNotScale - brak możliwości skalowania");
                                    }
                                    else
                                    {
                                        var remainingCalories = person.TargetCalories - doNotScaleCalories;
                                        dayScalingFactor = (double)remainingCalories / scalableCalories;
                                    }

                                    if (withinTolerance)
                                    {
                                        Console.WriteLine($"      👤 {person.Name}: cel {person.TargetCalories} kcal → ✓ W tolerancji ±50 kcal (różnica: {calorieDifference} kcal), bez skalowania");
                                    }
                                    else
                                    {
                                        var percentChange = (int)Math.Round((dayScalingFactor - 1.0) * 100);
                                        var sign = percentChange >= 0 ? "+" : "";
                                        Console.WriteLine($"      👤 {person.Name}: cel {person.TargetCalories} kcal → współczynnik {dayScalingFactor:F3} ({sign}{percentChange}%)");
                                    }

                                    // Scale each entry in this day for this person
                                    foreach (var entry in day.Entries)
                                    {
                                        if (entry.Recipe == null) continue;

                                        try
                                        {
                                            List<string> scaledIngredients;
                                            double actualScalingFactor = dayScalingFactor;

                                            // Check if recipe should not be scaled
                                            if (entry.Recipe.DoNotScale)
                                            {
                                                // Don't scale this recipe - use factor 1.0
                                                actualScalingFactor = 1.0;
                                                scaledIngredients = new List<string> { entry.Recipe.Ingredients };
                                                Console.WriteLine($"         🔒 {entry.Recipe.Name}: {entry.Recipe.Calories} kcal (NIE SKALOWANY - stała porcja)");
                                            }
                                            // Scale all other recipes with day factor
                                            else if (withinTolerance)
                                            {
                                                // Within tolerance - use base ingredients without AI scaling
                                                scaledIngredients = new List<string> { entry.Recipe.Ingredients };
                                                Console.WriteLine($"         ✓ {entry.Recipe.Name}: {entry.Recipe.Calories} kcal (bez skalowania)");
                                            }
                                            else
                                            {
                                                // Outside tolerance - scale with AI
                                                var scalingService = _scalingFactory.CreateScalingService();
                                                if (scalingService == null)
                                                {
                                                    Console.WriteLine($"         ⚠️ {entry.Recipe.Name}: brak serwisu skalowania - fallback do bazowych składników");
                                                    scaledIngredients = new List<string> { entry.Recipe.Ingredients };
                                                }
                                                else
                                                {
                                                    scaledIngredients = await scalingService.ScaleRecipeIngredientsAsync(
                                                        entry.Recipe,
                                                        dayScalingFactor,
                                                        entry.MealType
                                                    );
                                                }

                                                if (scaledIngredients.Count == 0)
                                                {
                                                    Console.WriteLine($"         ⚠️ {entry.Recipe.Name}: fallback do bazowych składników");
                                                    scaledIngredients = new List<string> { entry.Recipe.Ingredients };
                                                }

                                                var scaledCalories = (int)Math.Round(entry.Recipe.Calories * dayScalingFactor);
                                                Console.WriteLine($"         ✓ {entry.Recipe.Name}: {scaledCalories} kcal ({entry.Recipe.Calories}→{scaledCalories})");
                                            }

                                            var scaledRecipe = new MealPlanRecipe
                                            {
                                                MealPlanEntryId = entry.Id,
                                                PersonId = person.Id,
                                                BaseRecipeId = entry.Recipe.Id,
                                                ScalingFactor = actualScalingFactor,
                                                ScaledIngredients = scaledIngredients,
                                                ScaledCalories = (int)Math.Round(entry.Recipe.Calories * actualScalingFactor),
                                                ScaledProtein = entry.Recipe.Protein * actualScalingFactor,
                                                ScaledCarbs = entry.Recipe.Carbohydrates * actualScalingFactor,
                                                ScaledFat = entry.Recipe.Fat * actualScalingFactor,
                                                CreatedAt = DateTime.Now
                                            };
                                            _db.CreateMealPlanRecipe(scaledRecipe);
                                            scaledCount++;
                                        }
                                        catch (Exception ex)
                                        {
                                            var errorMsg = $"Błąd skalowania '{entry.Recipe.Name}' dla {person.Name}: {ex.Message}";
                                            scalingErrors.Add(errorMsg);
                                            Console.WriteLine($"         ❌ {errorMsg}");
                                        }
                                    }

                                    // Calculate and display daily sum for this person
                                    var personDailySum = day.Entries
                                        .Where(e => e.Recipe != null)
                                        .Sum(e => (int)Math.Round(e.Recipe!.Calories * dayScalingFactor));
                                    Console.WriteLine($"      ✅ {person.Name} suma dnia: {personDailySum} kcal (cel: {person.TargetCalories})");
                                }
                            }

                            if (scaledCount > 0)
                            {
                                Console.WriteLine($"✅ Automatyczne skalowanie zakończone: {scaledCount} przepisów dla {persons.Count} osób");
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
            else if (persons.Count > 0 && addedCount > 0 && request.SkipScaling)
            {
                Console.WriteLine($"⏭️ Pominięto automatyczne skalowanie (skipScaling=true). Użyj 'Skaluj przepisy' aby przeskalować później.");
                warnings.Add("ℹ️ Skalowanie zostało pominięte. Kliknij 'Skaluj przepisy' aby przeskalować dla osób w planie.");
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

    /// <summary>
    /// Check scaling status - how many recipes need scaling
    /// GET /api/mealplans/{planId}/scaling-status
    /// </summary>
    [HttpGet("{planId}/scaling-status")]
    public ActionResult GetScalingStatus(int planId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            var persons = _db.GetMealPlanPersons(planId);
            if (persons.Count == 0)
                return Ok(new { needsScaling = false, message = "Plan nie ma osób - skalowanie nie jest wymagane" });

            if (plan.Days == null || plan.Days.Count == 0)
                return Ok(new { needsScaling = false, message = "Plan nie ma dni" });

            // Count total entries (excluding DoNotScale recipes)
            var totalEntries = 0;
            var unscaledDetails = new List<object>();

            foreach (var day in plan.Days)
            {
                if (day.Entries == null) continue;

                foreach (var entry in day.Entries)
                {
                    if (entry.Recipe == null || entry.Recipe.DoNotScale) continue;
                    totalEntries++;
                }
            }

            var totalNeeded = totalEntries * persons.Count;

            // Count existing scaled recipes
            var existingScaled = _db.GetMealPlanRecipes(planId);
            var scaledCount = existingScaled.Count;

            var missingCount = totalNeeded - scaledCount;
            var needsScaling = missingCount > 0;

            // If missing, collect details about what needs scaling
            if (needsScaling)
            {
                var existingScaledKeys = new HashSet<string>(
                    existingScaled.Select(r => $"{r.MealPlanEntryId}_{r.PersonId}")
                );

                foreach (var day in plan.Days)
                {
                    if (day.Entries == null) continue;

                    foreach (var entry in day.Entries)
                    {
                        if (entry.Recipe == null || entry.Recipe.DoNotScale) continue;

                        foreach (var person in persons)
                        {
                            var scaleKey = $"{entry.Id}_{person.Id}";
                            if (!existingScaledKeys.Contains(scaleKey))
                            {
                                unscaledDetails.Add(new
                                {
                                    recipeName = entry.Recipe.Name,
                                    personName = person.Name,
                                    dayDate = day.Date.ToString("yyyy-MM-dd")
                                });
                            }
                        }
                    }
                }
            }

            return Ok(new
            {
                needsScaling,
                totalNeeded,
                scaledCount,
                missingCount,
                persons = persons.Count,
                entries = totalEntries,
                unscaledDetails = unscaledDetails.Count > 0 ? unscaledDetails.Take(20).ToList() : null,
                hasMore = unscaledDetails.Count > 20
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Manually scale all recipes in meal plan for persons
    /// POST /api/mealplans/{planId}/scale-recipes
    /// Query param: resetAll (default: true) - if false, only scale missing recipes
    /// </summary>
    [HttpPost("{planId}/scale-recipes")]
    public async Task<ActionResult> ScaleRecipes(int planId, [FromQuery] bool resetAll = true)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            var persons = _db.GetMealPlanPersons(planId);
            if (persons.Count == 0)
                return BadRequest(new { error = "Meal plan has no persons - scaling requires persons" });

            if (plan.Days == null || plan.Days.Count == 0)
                return BadRequest(new { error = "Meal plan has no days" });

            Console.WriteLine($"🔧 Manualne skalowanie dla planu: {plan.Name}");
            Console.WriteLine($"   👥 {persons.Count} osób w planie");
            Console.WriteLine($"   🔄 Tryb: {(resetAll ? "Skaluj wszystko od nowa" : "Skaluj tylko brakujące")}");

            // Get existing scaled recipes to check what's already done
            var existingScaledRecipes = _db.GetAllScaledRecipesForPlan(planId);
            var existingScaledKeys = new HashSet<string>(
                existingScaledRecipes.Select(r => $"{r.MealPlanEntryId}_{r.PersonId}")
            );

            if (resetAll)
            {
                // Clear existing scaled recipes
                Console.WriteLine($"   🗑️ Czyszczenie wszystkich istniejących przeskalowanych przepisów...");
                foreach (var scaledRecipe in existingScaledRecipes)
                {
                    _db.DeleteMealPlanRecipe(scaledRecipe.Id);
                }
                Console.WriteLine($"   ✓ Usunięto {existingScaledRecipes.Count} starych przeskalowanych przepisów");
                existingScaledKeys.Clear();
            }
            else
            {
                Console.WriteLine($"   ℹ️ Znaleziono {existingScaledRecipes.Count} już przeskalowanych przepisów - zostaną pominięte");
            }

            var scaledCount = 0;
            var skippedCount = 0;
            var scalingErrors = new List<string>();

            // Get active AI provider
            var activeProvider = _aiFactory.GetActiveProvider();
            if (activeProvider == null)
            {
                return BadRequest(new { error = "Brak aktywnego providera AI - skalowanie wymaga aktywnego providera" });
            }

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
            {
                return BadRequest(new { error = "Brak klucza API - skalowanie wymaga skonfigurowanego klucza API" });
            }

            // Reload plan to get fresh entries
            var freshPlan = _db.GetMealPlan(planId);
            if (freshPlan?.Days == null)
            {
                return BadRequest(new { error = "Nie można załadować planu" });
            }

            Console.WriteLine($"   🎯 Per-day intelligent scaling - każda osoba dostanie DOKŁADNIE swoje kalorie");

            // Process day by day to calculate person-specific daily scaling factors
            foreach (var day in freshPlan.Days)
            {
                if (day.Entries == null || day.Entries.Count == 0) continue;

                // Calculate daily calorie sums separately for scalable and non-scalable recipes
                var doNotScaleCalories = day.Entries
                    .Where(e => e.Recipe != null && e.Recipe.DoNotScale)
                    .Sum(e => e.Recipe!.Calories);

                var scalableCalories = day.Entries
                    .Where(e => e.Recipe != null && !e.Recipe.DoNotScale)
                    .Sum(e => e.Recipe!.Calories);

                var dailyCaloriesSum = doNotScaleCalories + scalableCalories;

                var dayName = day.Date.ToString("yyyy-MM-dd");
                Console.WriteLine($"   📅 Dzień: {dayName} (suma bazowa: {dailyCaloriesSum} kcal, DoNotScale: {doNotScaleCalories} kcal, Skalowalne: {scalableCalories} kcal)");

                if (dailyCaloriesSum == 0)
                {
                    Console.WriteLine($"      ⚠️ Pomiń dzień bez kalorii");
                    continue;
                }

                // Process each person for this day
                foreach (var person in persons)
                {
                    // Check if daily calories are within ±50 kcal tolerance
                    var calorieDifference = Math.Abs(dailyCaloriesSum - person.TargetCalories);
                    var withinTolerance = calorieDifference <= 50;

                    // Calculate person-specific day scaling factor
                    // For scalable recipes only: (targetCalories - doNotScaleCalories) / scalableCalories
                    double dayScalingFactor;
                    if (withinTolerance)
                    {
                        dayScalingFactor = 1.0;
                    }
                    else if (scalableCalories == 0)
                    {
                        // All recipes are DoNotScale - no scaling possible
                        dayScalingFactor = 1.0;
                        Console.WriteLine($"      ⚠️ {person.Name}: Wszystkie przepisy są DoNotScale - brak możliwości skalowania");
                    }
                    else
                    {
                        var remainingCalories = person.TargetCalories - doNotScaleCalories;
                        dayScalingFactor = (double)remainingCalories / scalableCalories;
                    }

                    if (withinTolerance)
                    {
                        Console.WriteLine($"      👤 {person.Name}: cel {person.TargetCalories} kcal → ✓ W tolerancji ±50 kcal (różnica: {calorieDifference} kcal), bez skalowania");
                    }
                    else
                    {
                        var percentChange = (int)Math.Round((dayScalingFactor - 1.0) * 100);
                        var sign = percentChange >= 0 ? "+" : "";
                        Console.WriteLine($"      👤 {person.Name}: cel {person.TargetCalories} kcal → współczynnik {dayScalingFactor:F3} ({sign}{percentChange}%)");
                    }

                    // Scale each entry in this day for this person
                    foreach (var entry in day.Entries)
                    {
                        if (entry.Recipe == null) continue;

                        // Check if this entry×person combination is already scaled
                        var scaleKey = $"{entry.Id}_{person.Id}";
                        if (existingScaledKeys.Contains(scaleKey))
                        {
                            skippedCount++;
                            Console.WriteLine($"         ⏭️ {entry.Recipe.Name} dla {person.Name}: już przeskalowane (pomijam)");
                            continue;
                        }

                        try
                        {
                            List<string> scaledIngredients;
                            double actualScalingFactor = dayScalingFactor;

                            // Check if recipe should not be scaled
                            if (entry.Recipe.DoNotScale)
                            {
                                // Don't scale this recipe - use factor 1.0
                                actualScalingFactor = 1.0;
                                scaledIngredients = new List<string> { entry.Recipe.Ingredients };
                                Console.WriteLine($"         🔒 {entry.Recipe.Name}: {entry.Recipe.Calories} kcal (NIE SKALOWANY - stała porcja)");
                            }
                            // Scale all other recipes with day factor
                            else if (withinTolerance)
                            {
                                // Within tolerance - use base ingredients without AI scaling
                                scaledIngredients = new List<string> { entry.Recipe.Ingredients };
                                Console.WriteLine($"         ✓ {entry.Recipe.Name}: {entry.Recipe.Calories} kcal (bez skalowania)");
                            }
                            else
                            {
                                // Outside tolerance - scale with AI
                                var scalingService = _scalingFactory.CreateScalingService();
                                if (scalingService == null)
                                {
                                    Console.WriteLine($"         ⚠️ {entry.Recipe.Name}: brak serwisu skalowania - fallback do bazowych składników");
                                    scaledIngredients = new List<string> { entry.Recipe.Ingredients };
                                }
                                else
                                {
                                    scaledIngredients = await scalingService.ScaleRecipeIngredientsAsync(
                                        entry.Recipe,
                                        dayScalingFactor,
                                        entry.MealType
                                    );

                                    if (scaledIngredients.Count == 0)
                                    {
                                        Console.WriteLine($"         ⚠️ {entry.Recipe.Name}: fallback do bazowych składników");
                                        scaledIngredients = new List<string> { entry.Recipe.Ingredients };
                                    }
                                }

                                var scaledCalories = (int)Math.Round(entry.Recipe.Calories * dayScalingFactor);
                                Console.WriteLine($"         ✓ {entry.Recipe.Name}: {scaledCalories} kcal ({entry.Recipe.Calories}→{scaledCalories})");
                            }

                            var scaledRecipe = new MealPlanRecipe
                            {
                                MealPlanEntryId = entry.Id,
                                PersonId = person.Id,
                                BaseRecipeId = entry.Recipe.Id,
                                ScalingFactor = actualScalingFactor,
                                ScaledIngredients = scaledIngredients,
                                ScaledCalories = (int)Math.Round(entry.Recipe.Calories * actualScalingFactor),
                                ScaledProtein = entry.Recipe.Protein * actualScalingFactor,
                                ScaledCarbs = entry.Recipe.Carbohydrates * actualScalingFactor,
                                ScaledFat = entry.Recipe.Fat * actualScalingFactor,
                                CreatedAt = DateTime.Now
                            };
                            _db.CreateMealPlanRecipe(scaledRecipe);
                            scaledCount++;
                        }
                        catch (Exception ex)
                        {
                            var errorMsg = $"Błąd skalowania '{entry.Recipe.Name}' dla {person.Name}: {ex.Message}";
                            scalingErrors.Add(errorMsg);
                            Console.WriteLine($"         ❌ {errorMsg}");
                        }
                    }

                    // Calculate and display daily sum for this person
                    var personDailySum = day.Entries
                        .Where(e => e.Recipe != null)
                        .Sum(e => (int)Math.Round(e.Recipe!.Calories * dayScalingFactor));
                    Console.WriteLine($"      ✅ {person.Name} suma dnia: {personDailySum} kcal (cel: {person.TargetCalories})");
                }
            }

            var summaryMsg = skippedCount > 0
                ? $"✅ Manualne skalowanie zakończone: {scaledCount} nowych, {skippedCount} pominiętych (już przeskalowanych)"
                : $"✅ Manualne skalowanie zakończone: {scaledCount} przepisów dla {persons.Count} osób";
            Console.WriteLine(summaryMsg);

            // Return updated plan
            var updatedPlan = _db.GetMealPlan(planId);

            var responseMsg = skippedCount > 0
                ? $"Przeskalowano {scaledCount} nowych przepisów (pominięto {skippedCount} już przeskalowanych)"
                : $"Przeskalowano {scaledCount} przepisów dla {persons.Count} osób";

            return Ok(new
            {
                message = responseMsg,
                scaledCount,
                skippedCount,
                warnings = scalingErrors.Count > 0 ? scalingErrors : null,
                plan = updatedPlan
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd manualnego skalowania: {ex.Message}");
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

            // Get persons count for UI display
            var hasPersons = plan.Persons != null && plan.Persons.Count > 0;

            // Count recipes in plan
            var recipeCount = 0;
            if (plan.Days != null)
            {
                foreach (var day in plan.Days)
                {
                    if (day.Entries != null)
                    {
                        recipeCount += day.Entries.Count;
                    }
                }
            }

            return Ok(new
            {
                id = shoppingList.Id,
                mealPlanId = shoppingList.MealPlanId,
                mealPlanName = plan.Name,
                recipeCount,
                generatedAt = shoppingList.GeneratedAt,
                itemCount = shoppingList.Items?.Count ?? 0,
                items = shoppingList.Items,
                personsCount = plan.Persons?.Count ?? 0,
                usesScaledIngredients = hasPersons,
                persons = plan.Persons?.Select(p => new { p.Name, p.TargetCalories }).ToList()
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

            // Check if plan has persons (multi-person mode)
            var hasPersons = plan.Persons != null && plan.Persons.Count > 0;

            if (hasPersons)
            {
                Console.WriteLine($"   👥 Plan ma {plan.Persons!.Count} osób - używam przeskalowanych składników");
            }

            // Group recipes by day number (for chunked generation)
            var recipesByDay = new Dictionary<int, List<Recipe>>();
            int dayIndex = 1;

            foreach (var day in plan.Days)
            {
                if (!recipesByDay.ContainsKey(dayIndex))
                    recipesByDay[dayIndex] = new List<Recipe>();

                if (day.Entries != null)
                {
                    foreach (var entry in day.Entries)
                    {
                        if (entry.Recipe == null) continue;

                        // If plan has persons and entry has scaled recipes, use scaled ingredients
                        if (hasPersons && entry.ScaledRecipes != null && entry.ScaledRecipes.Count > 0)
                        {
                            // Aggregate scaled ingredients from all persons for this entry
                            // OPCJA B: BEZ nagłówków osobowych - tylko składniki
                            var allScaledIngredients = new List<string>();

                            foreach (var scaledRecipe in entry.ScaledRecipes)
                            {
                                if (scaledRecipe.ScaledIngredients != null && scaledRecipe.ScaledIngredients.Count > 0)
                                {
                                    // Dodaj składniki bez nagłówka osoby
                                    allScaledIngredients.AddRange(scaledRecipe.ScaledIngredients);
                                }
                            }

                            if (allScaledIngredients.Count > 0)
                            {
                                // Create pseudo-Recipe with aggregated scaled ingredients
                                var pseudoRecipe = new Recipe
                                {
                                    Id = entry.Recipe.Id,
                                    Name = entry.Recipe.Name,
                                    Ingredients = string.Join("\n", allScaledIngredients),
                                    Calories = entry.Recipe.Calories,
                                    MealType = entry.Recipe.MealType
                                };

                                recipesByDay[dayIndex].Add(pseudoRecipe);
                                Console.WriteLine($"      ✓ Dzień {dayIndex}: {entry.Recipe.Name} (składniki dla {entry.ScaledRecipes.Count} osób)");
                            }
                            else
                            {
                                // Fallback to base recipe if scaled ingredients are empty
                                recipesByDay[dayIndex].Add(entry.Recipe);
                                Console.WriteLine($"      ⚠️ Dzień {dayIndex}: {entry.Recipe.Name} - brak przeskalowanych składników, używam bazowych");
                            }
                        }
                        else
                        {
                            // Use base recipe (no persons or no scaled recipes)
                            recipesByDay[dayIndex].Add(entry.Recipe);
                        }
                    }
                }

                dayIndex++;
            }

            if (recipesByDay.Count == 0 || recipesByDay.Values.All(recipes => recipes.Count == 0))
                return BadRequest(new { error = "Meal plan has no recipes" });

            var totalRecipes = recipesByDay.Values.Sum(recipes => recipes.Count);
            Console.WriteLine($"   Znaleziono {totalRecipes} przepisów w {recipesByDay.Count} dniach");

            // Create shopping list service using factory
            var shoppingListService = _shoppingListFactory.CreateShoppingListService();
            if (shoppingListService == null)
                return BadRequest(new { error = "Shopping list service not configured. Configure provider and API key in Settings." });

            // Generate shopping list using day-by-day chunking
            var shoppingList = await shoppingListService.GenerateShoppingListChunked(recipesByDay);

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
                recipeCount = totalRecipes,
                itemCount = shoppingList.Items.Count,
                generatedAt = savedList.GeneratedAt,
                items = shoppingList.Items,
                personsCount = plan.Persons?.Count ?? 0,
                usesScaledIngredients = hasPersons,
                persons = plan.Persons?.Select(p => new { p.Name, p.TargetCalories }).ToList()
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

            // Scale recipe for each person (including desserts)
            var scalingService = _scalingFactory.CreateScalingService();
            if (scalingService == null)
                return BadRequest(new { error = "Recipe scaling service not configured properly." });

            // Use MAX calories as baseline - recipes from DB are for max person
            // Others get scaled DOWN (scalingFactor <= 1.0)
            var maxCalories = persons.Max(p => p.TargetCalories);
            var baselineCalories = recipe.Calories;

            Console.WriteLine($"   📊 Bazowa kaloryczność (osoba z max): {maxCalories} kcal");

            var scaledCount = 0;

            foreach (var person in persons)
            {
                // Calculate scaling factor - max person gets 1.0, others get < 1.0
                var scalingFactor = (double)person.TargetCalories / maxCalories;

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
                scaledRecipes = scaledCount,
                persons = persons.Select(p => new
                {
                    name = p.Name,
                    targetCalories = p.TargetCalories
                })
            });
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

    /// <summary>
    /// Delete all scaled recipes for a meal plan (reset scaling)
    /// DELETE /api/mealplans/{planId}/scaled-recipes
    /// </summary>
    [HttpDelete("{planId}/scaled-recipes")]
    public ActionResult DeleteAllScaledRecipes(int planId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            Console.WriteLine($"🗑️ Usuwanie wszystkich przeskalowanych przepisów dla planu: {plan.Name}");

            // Get all scaled recipes for this plan
            var scaledRecipes = _db.GetAllScaledRecipesForPlan(planId);
            var deletedCount = 0;

            Console.WriteLine($"   Znaleziono {scaledRecipes.Count} przeskalowanych przepisów do usunięcia");

            foreach (var scaledRecipe in scaledRecipes)
            {
                var success = _db.DeleteMealPlanRecipe(scaledRecipe.Id);
                if (success)
                {
                    deletedCount++;
                }
            }

            Console.WriteLine($"✅ Usunięto {deletedCount} przeskalowanych przepisów");

            return Ok(new
            {
                message = $"Deleted {deletedCount} scaled recipes",
                deletedCount
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd usuwania przeskalowanych przepisów: {ex.Message}");
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
    public bool SkipScaling { get; set; } = false; // If true, skip automatic scaling (user can scale manually later)
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
