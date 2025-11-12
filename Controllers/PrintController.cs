using Microsoft.AspNetCore.Mvc;
using RecipesAIHelper.Data;
using System.Text;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrintController : ControllerBase
{
    private readonly RecipeDbContext _db;

    public PrintController(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Generate printable HTML for meal plan (table view - only recipe names)
    /// GET /api/print/meal-plan/{planId}
    /// </summary>
    [HttpGet("meal-plan/{planId}")]
    public ActionResult GetMealPlanPrintView(int planId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            if (plan.Days == null || plan.Days.Count == 0)
                return BadRequest(new { error = "Meal plan has no days" });

            var html = GenerateMealPlanTableHtml(plan);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate printable HTML for meal plan with full recipes
    /// GET /api/print/meal-plan/{planId}/full
    /// </summary>
    [HttpGet("meal-plan/{planId}/full")]
    public ActionResult GetMealPlanFullPrintView(int planId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            if (plan.Days == null || plan.Days.Count == 0)
                return BadRequest(new { error = "Meal plan has no days" });

            var html = GenerateMealPlanFullHtml(plan);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate printable HTML for single recipe
    /// GET /api/print/recipe/{recipeId}
    /// </summary>
    [HttpGet("recipe/{recipeId}")]
    public ActionResult GetRecipePrintView(int recipeId)
    {
        try
        {
            var recipes = _db.GetAllRecipes();
            var recipe = recipes.FirstOrDefault(r => r.Id == recipeId);

            if (recipe == null)
                return NotFound(new { error = "Recipe not found" });

            var html = GenerateRecipeHtml(recipe);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate printable HTML for shopping list
    /// GET /api/print/shopping-list/{planId}
    /// </summary>
    [HttpGet("shopping-list/{planId}")]
    public ActionResult GetShoppingListPrintView(int planId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            var shoppingList = _db.GetShoppingListByMealPlan(planId);
            if (shoppingList == null)
                return NotFound(new { error = "No shopping list found for this meal plan" });

            var html = GenerateShoppingListHtml(plan.Name, shoppingList);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate printable HTML for meal plan for specific person (with scaled recipes)
    /// GET /api/print/meal-plan/{planId}/person/{personId}
    /// </summary>
    [HttpGet("meal-plan/{planId}/person/{personId}")]
    public ActionResult GetMealPlanPersonPrintView(int planId, int personId)
    {
        try
        {
            var plan = _db.GetMealPlan(planId);
            if (plan == null)
                return NotFound(new { error = "Meal plan not found" });

            if (plan.Days == null || plan.Days.Count == 0)
                return BadRequest(new { error = "Meal plan has no days" });

            // Get person
            var person = _db.GetMealPlanPersons(planId).FirstOrDefault(p => p.Id == personId);
            if (person == null)
                return NotFound(new { error = "Person not found" });

            var html = GenerateMealPlanPersonHtml(plan, person);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // HTML Generation Methods

    private string GenerateMealPlanTableHtml(Models.MealPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset='utf-8'>");
        sb.AppendLine($"    <title>{plan.Name}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        @media print { ");
        sb.AppendLine("            @page { size: landscape; margin: 1cm; }");
        sb.AppendLine("            * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; color-adjust: exact !important; }");
        sb.AppendLine("        }");
        sb.AppendLine("        body { font-family: Arial, sans-serif; padding: 20px; color: #1a3a6b; }");
        sb.AppendLine("        h1 { text-align: center; margin-bottom: 10px; color: #2196F3; }");
        sb.AppendLine("        .date-range { text-align: center; color: #5a5a8a; margin-bottom: 20px; }");
        sb.AppendLine("        table { width: 100%; border-collapse: collapse; }");
        sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
        sb.AppendLine("        th { background-color: #4CAF50; color: white; font-weight: bold; }");
        sb.AppendLine("        td { vertical-align: top; }");
        sb.AppendLine("        .recipe-item { margin: 5px 0; padding: 5px; background: #f9f9f9; border-radius: 3px; }");
        sb.AppendLine("        .meal-type { font-weight: bold; color: #2196F3; font-size: 0.85em; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"    <h1>{plan.Name}</h1>");
        sb.AppendLine($"    <div class='date-range'>{plan.StartDate:dd.MM.yyyy} - {plan.EndDate:dd.MM.yyyy}</div>");
        sb.AppendLine("    <table>");
        sb.AppendLine("        <tr>");

        var dayNames = new[] { "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota", "Niedziela" };
        foreach (var dayName in dayNames)
        {
            sb.AppendLine($"            <th>{dayName}</th>");
        }

        sb.AppendLine("        </tr>");
        sb.AppendLine("        <tr>");

        // Create a dictionary for quick lookup of days by DayOfWeek
        var daysByDayOfWeek = plan.Days.ToDictionary(d => d.DayOfWeek);

        // Iterate through all 7 days of the week (0 = Monday, 6 = Sunday)
        for (int dayOfWeek = 0; dayOfWeek <= 6; dayOfWeek++)
        {
            sb.AppendLine("            <td>");

            if (daysByDayOfWeek.TryGetValue(dayOfWeek, out var day))
            {
                // Day exists in plan - show date and recipes
                sb.AppendLine($"                <div style='font-weight: bold; margin-bottom: 8px;'>{day.Date:dd.MM}</div>");

                if (day.Entries != null && day.Entries.Any())
                {
                    var groupedByMealType = day.Entries
                        .OrderBy(e => e.MealType)
                        .GroupBy(e => e.MealType);

                    foreach (var group in groupedByMealType)
                    {
                        foreach (var entry in group)
                        {
                            if (entry.Recipe != null)
                            {
                                sb.AppendLine($"                <div class='recipe-item'>");
                                sb.AppendLine($"                    <div class='meal-type'>{GetMealTypeName(entry.MealType)}</div>");
                                sb.AppendLine($"                    {entry.Recipe.Name}");
                                sb.AppendLine($"                </div>");
                            }
                        }
                    }
                }
            }
            else
            {
                // Day doesn't exist in plan - show empty cell
                sb.AppendLine("                &nbsp;");
            }

            sb.AppendLine("            </td>");
        }

        sb.AppendLine("        </tr>");
        sb.AppendLine("    </table>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GenerateMealPlanFullHtml(Models.MealPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset='utf-8'>");
        sb.AppendLine($"    <title>{plan.Name} - Pełne Przepisy</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        @media print { ");
        sb.AppendLine("            @page { margin: 1.5cm; }");
        sb.AppendLine("            * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; color-adjust: exact !important; }");
        sb.AppendLine("            .recipe-page { page-break-after: always; }");
        sb.AppendLine("        }");
        sb.AppendLine("        body { font-family: Arial, sans-serif; padding: 20px; color: #1a3a6b; }");
        sb.AppendLine("        .plan-header { text-align: center; margin-bottom: 30px; }");
        sb.AppendLine("        .recipe-page { margin-bottom: 40px; }");
        sb.AppendLine("        .recipe-header { background: #4CAF50; color: white; padding: 15px; margin-bottom: 20px; }");
        sb.AppendLine("        .recipe-title { font-size: 24px; font-weight: bold; margin: 0; }");
        sb.AppendLine("        .recipe-meta { font-size: 14px; margin-top: 5px; }");
        sb.AppendLine("        .section { margin-bottom: 20px; }");
        sb.AppendLine("        .section-title { font-size: 18px; font-weight: bold; color: #2196F3; margin-bottom: 10px; border-bottom: 2px solid #2196F3; }");
        sb.AppendLine("        .nutrition { display: flex; gap: 20px; flex-wrap: wrap; }");
        sb.AppendLine("        .nutrition-item { background: #e8f5e9; padding: 10px; border-radius: 5px; min-width: 120px; }");
        sb.AppendLine("        .nutrition-label { font-size: 12px; color: #5a5a8a; }");
        sb.AppendLine("        .nutrition-value { font-size: 20px; font-weight: bold; color: #4CAF50; }");
        sb.AppendLine("        .ingredients { line-height: 1.8; color: #2d5a2d; }");
        sb.AppendLine("        .instructions { line-height: 1.8; white-space: pre-wrap; color: #2d4a6d; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"    <div class='plan-header'>");
        sb.AppendLine($"        <h1>{plan.Name}</h1>");
        sb.AppendLine($"        <div>{plan.StartDate:dd.MM.yyyy} - {plan.EndDate:dd.MM.yyyy}</div>");
        sb.AppendLine($"    </div>");

        // Collect all unique recipes
        var allRecipes = new HashSet<int>();
        foreach (var day in plan.Days)
        {
            if (day.Entries != null)
            {
                foreach (var entry in day.Entries)
                {
                    if (entry.Recipe != null)
                    {
                        allRecipes.Add(entry.RecipeId);
                    }
                }
            }
        }

        // Get recipes and generate pages
        var recipes = _db.GetAllRecipes().Where(r => allRecipes.Contains(r.Id)).OrderBy(r => r.Name).ToList();
        foreach (var recipe in recipes)
        {
            sb.AppendLine(GenerateRecipeContent(recipe));
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GenerateRecipeHtml(Models.Recipe recipe)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset='utf-8'>");
        sb.AppendLine($"    <title>{recipe.Name}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        @media print { ");
        sb.AppendLine("            @page { margin: 1.5cm; }");
        sb.AppendLine("            * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; color-adjust: exact !important; }");
        sb.AppendLine("        }");
        sb.AppendLine("        body { font-family: Arial, sans-serif; padding: 20px; max-width: 800px; margin: 0 auto; color: #1a3a6b; }");
        sb.AppendLine("        .recipe-header { background: #4CAF50; color: white; padding: 20px; margin: -20px -20px 30px -20px; }");
        sb.AppendLine("        .recipe-title { font-size: 28px; font-weight: bold; margin: 0; }");
        sb.AppendLine("        .recipe-meta { font-size: 14px; margin-top: 10px; }");
        sb.AppendLine("        .section { margin-bottom: 25px; }");
        sb.AppendLine("        .section-title { font-size: 20px; font-weight: bold; color: #2196F3; margin-bottom: 15px; border-bottom: 2px solid #2196F3; padding-bottom: 5px; }");
        sb.AppendLine("        .nutrition { display: flex; gap: 20px; flex-wrap: wrap; }");
        sb.AppendLine("        .nutrition-item { background: #e8f5e9; padding: 15px; border-radius: 8px; min-width: 130px; text-align: center; }");
        sb.AppendLine("        .nutrition-label { font-size: 13px; color: #5a5a8a; text-transform: uppercase; }");
        sb.AppendLine("        .nutrition-value { font-size: 24px; font-weight: bold; color: #4CAF50; margin-top: 5px; }");
        sb.AppendLine("        .ingredients { line-height: 2; white-space: pre-wrap; color: #2d5a2d; }");
        sb.AppendLine("        .instructions { line-height: 1.8; white-space: pre-wrap; color: #2d4a6d; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(GenerateRecipeContent(recipe));
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GenerateRecipeContent(Models.Recipe recipe)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    <div class='recipe-page'>");
        sb.AppendLine("        <div class='recipe-header'>");
        sb.AppendLine($"            <div class='recipe-title'>{recipe.Name}</div>");
        sb.AppendLine($"            <div class='recipe-meta'>{GetMealTypeName(recipe.MealType)}");
        if (recipe.Servings.HasValue)
        {
            sb.Append($" • Liczba porcji: {recipe.Servings}");
        }
        sb.AppendLine("</div>");
        sb.AppendLine("        </div>");

        // Description
        if (!string.IsNullOrEmpty(recipe.Description))
        {
            sb.AppendLine("        <div class='section'>");
            sb.AppendLine("            <div class='section-title'>Opis</div>");
            sb.AppendLine($"            <div>{recipe.Description}</div>");
            sb.AppendLine("        </div>");
        }

        // Nutrition
        sb.AppendLine("        <div class='section'>");
        sb.AppendLine("            <div class='section-title'>Wartości Odżywcze</div>");
        sb.AppendLine("            <div class='nutrition'>");
        sb.AppendLine("                <div class='nutrition-item'>");
        sb.AppendLine("                    <div class='nutrition-label'>Kalorie</div>");
        sb.AppendLine($"                    <div class='nutrition-value'>{recipe.Calories}</div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("                <div class='nutrition-item'>");
        sb.AppendLine("                    <div class='nutrition-label'>Białko</div>");
        sb.AppendLine($"                    <div class='nutrition-value'>{recipe.Protein}g</div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("                <div class='nutrition-item'>");
        sb.AppendLine("                    <div class='nutrition-label'>Węglowodany</div>");
        sb.AppendLine($"                    <div class='nutrition-value'>{recipe.Carbohydrates}g</div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("                <div class='nutrition-item'>");
        sb.AppendLine("                    <div class='nutrition-label'>Tłuszcze</div>");
        sb.AppendLine($"                    <div class='nutrition-value'>{recipe.Fat}g</div>");
        sb.AppendLine("                </div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        // Ingredients
        sb.AppendLine("        <div class='section'>");
        sb.AppendLine("            <div class='section-title'>Składniki</div>");
        sb.AppendLine($"            <div class='ingredients'>{recipe.Ingredients}</div>");
        sb.AppendLine("        </div>");

        // Instructions
        sb.AppendLine("        <div class='section'>");
        sb.AppendLine("            <div class='section-title'>Przygotowanie</div>");
        sb.AppendLine($"            <div class='instructions'>{recipe.Instructions}</div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("    </div>");

        return sb.ToString();
    }

    private string GenerateShoppingListHtml(string planName, Models.ShoppingList shoppingList)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset='utf-8'>");
        sb.AppendLine($"    <title>Lista Zakupowa - {planName}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        @media print { ");
        sb.AppendLine("            @page { margin: 1.5cm; }");
        sb.AppendLine("            * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; color-adjust: exact !important; }");
        sb.AppendLine("        }");
        sb.AppendLine("        body { font-family: Arial, sans-serif; padding: 20px; max-width: 800px; margin: 0 auto; color: #1a3a6b; }");
        sb.AppendLine("        h1 { text-align: center; color: #4CAF50; margin-bottom: 10px; }");
        sb.AppendLine("        .subtitle { text-align: center; color: #5a5a8a; margin-bottom: 30px; }");
        sb.AppendLine("        .category { margin-bottom: 30px; }");
        sb.AppendLine("        .category-title { font-size: 20px; font-weight: bold; color: #2196F3; margin-bottom: 15px; padding-bottom: 5px; border-bottom: 2px solid #2196F3; }");
        sb.AppendLine("        .item { display: flex; padding: 12px; border-bottom: 1px solid #b3d9ff; }");
        sb.AppendLine("        .item:last-child { border-bottom: none; }");
        sb.AppendLine("        .checkbox { width: 30px; height: 30px; border: 2px solid #4CAF50; border-radius: 4px; margin-right: 15px; flex-shrink: 0; }");
        sb.AppendLine("        .item-name { flex: 1; font-size: 16px; color: #2d4a6d; }");
        sb.AppendLine("        .item-quantity { font-weight: bold; color: #4CAF50; min-width: 100px; text-align: right; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"    <h1>Lista Zakupowa</h1>");
        sb.AppendLine($"    <div class='subtitle'>{planName} • {shoppingList.GeneratedAt:dd.MM.yyyy HH:mm}</div>");

        if (shoppingList.Items != null && shoppingList.Items.Any())
        {
            // Group by category
            var groupedItems = shoppingList.Items
                .GroupBy(i => i.Category)
                .OrderBy(g => g.Key);

            foreach (var group in groupedItems)
            {
                sb.AppendLine("    <div class='category'>");
                sb.AppendLine($"        <div class='category-title'>{CapitalizeFirst(group.Key)}</div>");

                foreach (var item in group.OrderBy(i => i.Name))
                {
                    sb.AppendLine("        <div class='item'>");
                    sb.AppendLine("            <div class='checkbox'></div>");
                    sb.AppendLine($"            <div class='item-name'>{item.Name}</div>");
                    sb.AppendLine($"            <div class='item-quantity'>{item.Quantity}</div>");
                    sb.AppendLine("        </div>");
                }

                sb.AppendLine("    </div>");
            }
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string GetMealTypeName(Models.MealType mealType)
    {
        return mealType switch
        {
            Models.MealType.Sniadanie => "Śniadanie",
            Models.MealType.Obiad => "Obiad",
            Models.MealType.Kolacja => "Kolacja",
            Models.MealType.Deser => "Deser",
            Models.MealType.Napoj => "Napój",
            _ => mealType.ToString()
        };
    }

    private string CapitalizeFirst(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        return char.ToUpper(text[0]) + text.Substring(1);
    }

    private string GenerateMealPlanPersonHtml(Models.MealPlan plan, Models.MealPlanPerson person)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset='utf-8'>");
        sb.AppendLine($"    <title>{plan.Name} - {person.Name}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        @media print { ");
        sb.AppendLine("            @page { margin: 1.5cm; }");
        sb.AppendLine("            * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; color-adjust: exact !important; }");
        sb.AppendLine("            .recipe-section { page-break-inside: avoid; }");
        sb.AppendLine("        }");
        sb.AppendLine("        body { font-family: Arial, sans-serif; padding: 20px; max-width: 900px; margin: 0 auto; color: #1a3a6b; }");
        sb.AppendLine("        .plan-header { text-align: center; margin-bottom: 30px; padding: 20px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; border-radius: 10px; }");
        sb.AppendLine("        .plan-title { font-size: 32px; font-weight: bold; margin: 0 0 10px 0; }");
        sb.AppendLine("        .person-info { font-size: 18px; margin: 5px 0; }");
        sb.AppendLine("        .date-range { font-size: 14px; opacity: 0.9; }");
        sb.AppendLine("        .day-section { margin-bottom: 40px; border: 2px solid #b3d9ff; border-radius: 8px; overflow: hidden; }");
        sb.AppendLine("        .day-header { background: #e3f2fd; padding: 15px 20px; border-bottom: 2px solid #90caf9; }");
        sb.AppendLine("        .day-title { font-size: 22px; font-weight: bold; color: #1565c0; margin: 0; }");
        sb.AppendLine("        .day-date { font-size: 14px; color: #5a5a8a; margin-top: 5px; }");
        sb.AppendLine("        .recipe-section { padding: 20px; border-bottom: 1px solid #b3d9ff; }");
        sb.AppendLine("        .recipe-section:last-child { border-bottom: none; }");
        sb.AppendLine("        .recipe-header { background: #4CAF50; color: white; padding: 12px 15px; margin: -20px -20px 15px -20px; }");
        sb.AppendLine("        .recipe-title { font-size: 20px; font-weight: bold; margin: 0; }");
        sb.AppendLine("        .recipe-meta { font-size: 13px; margin-top: 5px; opacity: 0.9; }");
        sb.AppendLine("        .scaling-badge { display: inline-block; background: #FF9800; color: white; padding: 3px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; margin-left: 10px; }");
        sb.AppendLine("        .section { margin-bottom: 20px; }");
        sb.AppendLine("        .section-title { font-size: 16px; font-weight: bold; color: #2196F3; margin-bottom: 10px; border-bottom: 2px solid #2196F3; padding-bottom: 5px; }");
        sb.AppendLine("        .nutrition { display: flex; gap: 15px; flex-wrap: wrap; margin-bottom: 15px; }");
        sb.AppendLine("        .nutrition-item { background: #e8f5e9; padding: 12px; border-radius: 6px; min-width: 100px; text-align: center; }");
        sb.AppendLine("        .nutrition-label { font-size: 11px; color: #5a5a8a; text-transform: uppercase; }");
        sb.AppendLine("        .nutrition-value { font-size: 20px; font-weight: bold; color: #4CAF50; margin-top: 5px; }");
        sb.AppendLine("        .ingredients { line-height: 1.8; white-space: pre-wrap; color: #2d5a2d; }");
        sb.AppendLine("        .instructions { line-height: 1.6; white-space: pre-wrap; color: #2d4a6d; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("    <div class='plan-header'>");
        sb.AppendLine($"        <div class='plan-title'>{plan.Name}</div>");
        sb.AppendLine($"        <div class='person-info'>👤 {person.Name} | 🎯 {person.TargetCalories} kcal/dzień</div>");
        sb.AppendLine($"        <div class='date-range'>{plan.StartDate:dd.MM.yyyy} - {plan.EndDate:dd.MM.yyyy}</div>");
        sb.AppendLine("    </div>");

        // Iterate through days
        if (plan.Days != null)
        {
            foreach (var day in plan.Days.OrderBy(d => d.Date))
            {
                sb.AppendLine("    <div class='day-section'>");
                sb.AppendLine("        <div class='day-header'>");
                sb.AppendLine($"            <div class='day-title'>{GetDayOfWeekName(day.DayOfWeek)}</div>");
                sb.AppendLine($"            <div class='day-date'>{day.Date:dd.MM.yyyy}</div>");
                sb.AppendLine("        </div>");

                if (day.Entries != null && day.Entries.Count > 0)
                {
                    foreach (var entry in day.Entries.OrderBy(e => e.MealType))
                    {
                        if (entry.Recipe == null) continue;

                        // Find scaled recipe for this person
                        var scaledRecipe = entry.ScaledRecipes?.FirstOrDefault(sr => sr.PersonId == person.Id);

                        sb.AppendLine("        <div class='recipe-section'>");
                        sb.AppendLine("            <div class='recipe-header'>");
                        sb.AppendLine($"                <div class='recipe-title'>");
                        sb.AppendLine($"                    {entry.Recipe.Name}");

                        if (scaledRecipe != null && scaledRecipe.ScalingFactor != 1.0)
                        {
                            var percentage = (int)Math.Round((scaledRecipe.ScalingFactor - 1.0) * 100);
                            var sign = percentage > 0 ? "+" : "";
                            sb.AppendLine($"                    <span class='scaling-badge'>Przeskalowano {sign}{percentage}%</span>");
                        }

                        sb.AppendLine("                </div>");
                        sb.AppendLine($"                <div class='recipe-meta'>{GetMealTypeName(entry.MealType)}");
                        if (entry.Recipe.Servings.HasValue)
                        {
                            sb.AppendLine($" • {entry.Recipe.Servings} porcji");
                        }
                        sb.AppendLine("</div>");
                        sb.AppendLine("            </div>");

                        // Nutrition (scaled if available)
                        sb.AppendLine("            <div class='section'>");
                        sb.AppendLine("                <div class='section-title'>🔥 Wartości odżywcze (Twoja porcja)</div>");
                        sb.AppendLine("                <div class='nutrition'>");

                        if (scaledRecipe != null)
                        {
                            sb.AppendLine("                    <div class='nutrition-item'>");
                            sb.AppendLine("                        <div class='nutrition-label'>Kalorie</div>");
                            sb.AppendLine($"                        <div class='nutrition-value'>{scaledRecipe.ScaledCalories}</div>");
                            sb.AppendLine("                    </div>");
                            sb.AppendLine("                    <div class='nutrition-item'>");
                            sb.AppendLine("                        <div class='nutrition-label'>Białko</div>");
                            sb.AppendLine($"                        <div class='nutrition-value'>{scaledRecipe.ScaledProtein:F1}g</div>");
                            sb.AppendLine("                    </div>");
                            sb.AppendLine("                    <div class='nutrition-item'>");
                            sb.AppendLine("                        <div class='nutrition-label'>Węglowodany</div>");
                            sb.AppendLine($"                        <div class='nutrition-value'>{scaledRecipe.ScaledCarbs:F1}g</div>");
                            sb.AppendLine("                    </div>");
                            sb.AppendLine("                    <div class='nutrition-item'>");
                            sb.AppendLine("                        <div class='nutrition-label'>Tłuszcze</div>");
                            sb.AppendLine($"                        <div class='nutrition-value'>{scaledRecipe.ScaledFat:F1}g</div>");
                            sb.AppendLine("                    </div>");
                        }
                        else
                        {
                            // Fallback to base recipe
                            sb.AppendLine("                    <div class='nutrition-item'>");
                            sb.AppendLine("                        <div class='nutrition-label'>Kalorie</div>");
                            sb.AppendLine($"                        <div class='nutrition-value'>{entry.Recipe.Calories}</div>");
                            sb.AppendLine("                    </div>");
                            sb.AppendLine("                    <div class='nutrition-item'>");
                            sb.AppendLine("                        <div class='nutrition-label'>Białko</div>");
                            sb.AppendLine($"                        <div class='nutrition-value'>{entry.Recipe.Protein:F1}g</div>");
                            sb.AppendLine("                    </div>");
                            sb.AppendLine("                    <div class='nutrition-item'>");
                            sb.AppendLine("                        <div class='nutrition-label'>Węglowodany</div>");
                            sb.AppendLine($"                        <div class='nutrition-value'>{entry.Recipe.Carbohydrates:F1}g</div>");
                            sb.AppendLine("                    </div>");
                            sb.AppendLine("                    <div class='nutrition-item'>");
                            sb.AppendLine("                        <div class='nutrition-label'>Tłuszcze</div>");
                            sb.AppendLine($"                        <div class='nutrition-value'>{entry.Recipe.Fat:F1}g</div>");
                            sb.AppendLine("                    </div>");
                        }

                        sb.AppendLine("                </div>");
                        sb.AppendLine("            </div>");

                        // Ingredients (scaled if available)
                        if (!string.IsNullOrEmpty(entry.Recipe.Ingredients))
                        {
                            sb.AppendLine("            <div class='section'>");
                            sb.AppendLine("                <div class='section-title'>🥘 Składniki (Twoja porcja)</div>");

                            if (scaledRecipe?.ScaledIngredients != null && scaledRecipe.ScaledIngredients.Count > 0)
                            {
                                // Use scaled ingredients
                                var scaledIngredientsText = string.Join("\n", scaledRecipe.ScaledIngredients);
                                sb.AppendLine($"                <div class='ingredients'>{System.Security.SecurityElement.Escape(scaledIngredientsText)}</div>");
                            }
                            else
                            {
                                // Fallback to base ingredients
                                sb.AppendLine($"                <div class='ingredients'>{System.Security.SecurityElement.Escape(entry.Recipe.Ingredients)}</div>");
                            }

                            sb.AppendLine("            </div>");
                        }

                        // Instructions (always from base recipe)
                        if (!string.IsNullOrEmpty(entry.Recipe.Instructions))
                        {
                            sb.AppendLine("            <div class='section'>");
                            sb.AppendLine("                <div class='section-title'>👨‍🍳 Sposób przygotowania</div>");
                            sb.AppendLine($"                <div class='instructions'>{System.Security.SecurityElement.Escape(entry.Recipe.Instructions)}</div>");
                            sb.AppendLine("            </div>");
                        }

                        sb.AppendLine("        </div>");
                    }
                }
                else
                {
                    sb.AppendLine("        <div class='recipe-section'>");
                    sb.AppendLine("            <p style='text-align: center; color: #999;'>Brak przepisów na ten dzień</p>");
                    sb.AppendLine("        </div>");
                }

                sb.AppendLine("    </div>");
            }
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
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
