using Microsoft.Extensions.Configuration;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;
using RecipesAIHelper.Services;

namespace RecipesAIHelper;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Recipe AI Helper ===\n");

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var openAiApiKey = configuration["OpenAI:ApiKey"] ??
            Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
            string.Empty;

        var pdfDirectory = configuration["Settings:PdfSourceDirectory"] ??
            @"C:\Users\Karolina\Downloads\Dieta";

        var databasePath = configuration["Settings:DatabasePath"] ?? "recipes.db";

        if (string.IsNullOrEmpty(openAiApiKey) || openAiApiKey == "YOUR_OPENAI_API_KEY_HERE")
        {
            Console.WriteLine("ERROR: OpenAI API key not configured!");
            Console.WriteLine("Set it in appsettings.json or OPENAI_API_KEY environment variable.");
            return;
        }

        // Initialize database
        using var db = new RecipeDbContext(databasePath);
        db.InitializeDatabase();
        Console.WriteLine($"Database initialized: {databasePath}");
        Console.WriteLine($"Current recipes in database: {db.GetRecipeCount()}\n");

        // Show menu
        while (true)
        {
            Console.WriteLine("\nChoose an option:");
            Console.WriteLine("1. Process PDFs and extract recipes");
            Console.WriteLine("2. Get random meal suggestions");
            Console.WriteLine("3. View all recipes");
            Console.WriteLine("4. Exit");
            Console.Write("\nYour choice: ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await ProcessPdfs(pdfDirectory, openAiApiKey, db);
                    break;
                case "2":
                    GetRandomMeals(db);
                    break;
                case "3":
                    ViewAllRecipes(db);
                    break;
                case "4":
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid choice. Try again.");
                    break;
            }
        }
    }

    static async Task ProcessPdfs(string pdfDirectory, string apiKey, RecipeDbContext db)
    {
        Console.WriteLine($"\nProcessing PDFs from: {pdfDirectory}");

        var pdfProcessor = new PdfProcessorService();
        var openAiService = new OpenAIService(apiKey);

        try
        {
            var pdfFiles = pdfProcessor.GetAllPdfFiles(pdfDirectory);
            Console.WriteLine($"Found {pdfFiles.Count} PDF files\n");

            if (pdfFiles.Count == 0)
            {
                Console.WriteLine("No PDF files found in the directory.");
                return;
            }

            var totalRecipesExtracted = 0;

            foreach (var pdfFile in pdfFiles)
            {
                Console.WriteLine($"\nProcessing: {Path.GetFileName(pdfFile)}");

                try
                {
                    // Extract text from PDF
                    var text = pdfProcessor.ExtractTextFromPdf(pdfFile);
                    Console.WriteLine($"Extracted {text.Length} characters from PDF");

                    // Send to OpenAI for recipe extraction
                    var recipes = await openAiService.ExtractRecipesFromText(text);
                    Console.WriteLine($"Extracted {recipes.Count} recipes");

                    // Save to database
                    foreach (var recipeData in recipes)
                    {
                        var recipe = new Recipe
                        {
                            Name = recipeData.Name,
                            Description = recipeData.Description,
                            Ingredients = string.Join("\n", recipeData.Ingredients),
                            Instructions = recipeData.Instructions,
                            Calories = recipeData.Calories,
                            Protein = recipeData.Protein,
                            Carbohydrates = recipeData.Carbohydrates,
                            Fat = recipeData.Fat,
                            MealType = Enum.TryParse<MealType>(recipeData.MealType, out var mealType)
                                ? mealType
                                : MealType.Lunch,
                            CreatedAt = DateTime.Now
                        };

                        db.InsertRecipe(recipe);
                        Console.WriteLine($"  - Saved: {recipe.Name}");
                        totalRecipesExtracted++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {Path.GetFileName(pdfFile)}: {ex.Message}");
                }

                // Add a small delay to avoid rate limiting
                await Task.Delay(1000);
            }

            Console.WriteLine($"\n✓ Processing complete! Total recipes extracted: {totalRecipesExtracted}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void GetRandomMeals(RecipeDbContext db)
    {
        Console.WriteLine("\n=== Random Daily Meal Plan ===\n");

        try
        {
            var breakfast = db.GetRandomRecipesByMealType(MealType.Breakfast, 1);
            var lunch = db.GetRandomRecipesByMealType(MealType.Lunch, 1);
            var dinner = db.GetRandomRecipesByMealType(MealType.Dinner, 1);

            if (breakfast.Count > 0)
            {
                Console.WriteLine("BREAKFAST:");
                PrintRecipe(breakfast[0]);
            }
            else
            {
                Console.WriteLine("BREAKFAST: No breakfast recipes found");
            }

            if (lunch.Count > 0)
            {
                Console.WriteLine("\nLUNCH:");
                PrintRecipe(lunch[0]);
            }
            else
            {
                Console.WriteLine("\nLUNCH: No lunch recipes found");
            }

            if (dinner.Count > 0)
            {
                Console.WriteLine("\nDINNER:");
                PrintRecipe(dinner[0]);
            }
            else
            {
                Console.WriteLine("\nDINNER: No dinner recipes found");
            }

            // Calculate daily totals
            var totalCalories = breakfast.Sum(r => r.Calories) + lunch.Sum(r => r.Calories) + dinner.Sum(r => r.Calories);
            var totalProtein = breakfast.Sum(r => r.Protein) + lunch.Sum(r => r.Protein) + dinner.Sum(r => r.Protein);
            var totalCarbs = breakfast.Sum(r => r.Carbohydrates) + lunch.Sum(r => r.Carbohydrates) + dinner.Sum(r => r.Carbohydrates);
            var totalFat = breakfast.Sum(r => r.Fat) + lunch.Sum(r => r.Fat) + dinner.Sum(r => r.Fat);

            Console.WriteLine("\n=== DAILY TOTALS ===");
            Console.WriteLine($"Calories: {totalCalories} kcal");
            Console.WriteLine($"Protein: {totalProtein:F1}g");
            Console.WriteLine($"Carbohydrates: {totalCarbs:F1}g");
            Console.WriteLine($"Fat: {totalFat:F1}g");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void ViewAllRecipes(RecipeDbContext db)
    {
        Console.WriteLine("\n=== All Recipes ===\n");

        try
        {
            var recipes = db.GetAllRecipes();

            if (recipes.Count == 0)
            {
                Console.WriteLine("No recipes in database yet. Process some PDFs first!");
                return;
            }

            foreach (var recipe in recipes)
            {
                Console.WriteLine($"\n[{recipe.MealType}] {recipe.Name}");
                Console.WriteLine($"Calories: {recipe.Calories} | P: {recipe.Protein}g | C: {recipe.Carbohydrates}g | F: {recipe.Fat}g");
                Console.WriteLine(new string('-', 60));
            }

            Console.WriteLine($"\nTotal recipes: {recipes.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void PrintRecipe(Recipe recipe)
    {
        Console.WriteLine($"{recipe.Name}");
        Console.WriteLine($"Calories: {recipe.Calories} kcal | Protein: {recipe.Protein}g | Carbs: {recipe.Carbohydrates}g | Fat: {recipe.Fat}g");
        if (!string.IsNullOrEmpty(recipe.Description))
        {
            Console.WriteLine($"Description: {recipe.Description}");
        }
    }
}
