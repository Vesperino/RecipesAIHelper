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
        sb.AppendLine("        @media print { @page { size: landscape; margin: 1cm; } }");
        sb.AppendLine("        body { font-family: Arial, sans-serif; padding: 20px; }");
        sb.AppendLine("        h1 { text-align: center; margin-bottom: 10px; }");
        sb.AppendLine("        .date-range { text-align: center; color: #666; margin-bottom: 20px; }");
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
        sb.AppendLine("        @media print { @page { margin: 1.5cm; } .recipe-page { page-break-after: always; } }");
        sb.AppendLine("        body { font-family: Arial, sans-serif; padding: 20px; }");
        sb.AppendLine("        .plan-header { text-align: center; margin-bottom: 30px; }");
        sb.AppendLine("        .recipe-page { margin-bottom: 40px; }");
        sb.AppendLine("        .recipe-header { background: #4CAF50; color: white; padding: 15px; margin-bottom: 20px; }");
        sb.AppendLine("        .recipe-title { font-size: 24px; font-weight: bold; margin: 0; }");
        sb.AppendLine("        .recipe-meta { font-size: 14px; margin-top: 5px; }");
        sb.AppendLine("        .section { margin-bottom: 20px; }");
        sb.AppendLine("        .section-title { font-size: 18px; font-weight: bold; color: #2196F3; margin-bottom: 10px; border-bottom: 2px solid #2196F3; }");
        sb.AppendLine("        .nutrition { display: flex; gap: 20px; flex-wrap: wrap; }");
        sb.AppendLine("        .nutrition-item { background: #f0f0f0; padding: 10px; border-radius: 5px; min-width: 120px; }");
        sb.AppendLine("        .nutrition-label { font-size: 12px; color: #666; }");
        sb.AppendLine("        .nutrition-value { font-size: 20px; font-weight: bold; color: #4CAF50; }");
        sb.AppendLine("        .ingredients { line-height: 1.8; }");
        sb.AppendLine("        .instructions { line-height: 1.8; white-space: pre-wrap; }");
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
        sb.AppendLine("        @media print { @page { margin: 1.5cm; } }");
        sb.AppendLine("        body { font-family: Arial, sans-serif; padding: 20px; max-width: 800px; margin: 0 auto; }");
        sb.AppendLine("        .recipe-header { background: #4CAF50; color: white; padding: 20px; margin: -20px -20px 30px -20px; }");
        sb.AppendLine("        .recipe-title { font-size: 28px; font-weight: bold; margin: 0; }");
        sb.AppendLine("        .recipe-meta { font-size: 14px; margin-top: 10px; }");
        sb.AppendLine("        .section { margin-bottom: 25px; }");
        sb.AppendLine("        .section-title { font-size: 20px; font-weight: bold; color: #2196F3; margin-bottom: 15px; border-bottom: 2px solid #2196F3; padding-bottom: 5px; }");
        sb.AppendLine("        .nutrition { display: flex; gap: 20px; flex-wrap: wrap; }");
        sb.AppendLine("        .nutrition-item { background: #f0f0f0; padding: 15px; border-radius: 8px; min-width: 130px; text-align: center; }");
        sb.AppendLine("        .nutrition-label { font-size: 13px; color: #666; text-transform: uppercase; }");
        sb.AppendLine("        .nutrition-value { font-size: 24px; font-weight: bold; color: #4CAF50; margin-top: 5px; }");
        sb.AppendLine("        .ingredients { line-height: 2; white-space: pre-wrap; }");
        sb.AppendLine("        .instructions { line-height: 1.8; white-space: pre-wrap; }");
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
        sb.AppendLine("        @media print { @page { margin: 1.5cm; } }");
        sb.AppendLine("        body { font-family: Arial, sans-serif; padding: 20px; max-width: 800px; margin: 0 auto; }");
        sb.AppendLine("        h1 { text-align: center; color: #4CAF50; margin-bottom: 10px; }");
        sb.AppendLine("        .subtitle { text-align: center; color: #666; margin-bottom: 30px; }");
        sb.AppendLine("        .category { margin-bottom: 30px; }");
        sb.AppendLine("        .category-title { font-size: 20px; font-weight: bold; color: #2196F3; margin-bottom: 15px; padding-bottom: 5px; border-bottom: 2px solid #2196F3; }");
        sb.AppendLine("        .item { display: flex; padding: 12px; border-bottom: 1px solid #eee; }");
        sb.AppendLine("        .item:last-child { border-bottom: none; }");
        sb.AppendLine("        .checkbox { width: 30px; height: 30px; border: 2px solid #4CAF50; border-radius: 4px; margin-right: 15px; flex-shrink: 0; }");
        sb.AppendLine("        .item-name { flex: 1; font-size: 16px; }");
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
}
