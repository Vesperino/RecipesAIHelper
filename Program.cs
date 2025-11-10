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

        var openAiModel = configuration["OpenAI:Model"] ?? "gpt-5-nano-2025-08-07";

        var pdfDirectory = configuration["Settings:PdfSourceDirectory"] ??
            @"C:\Users\Karolina\Downloads\Dieta";

        var databasePath = configuration["Settings:DatabasePath"] ?? "recipes.db";

        var pagesPerChunk = int.TryParse(configuration["Settings:PagesPerChunk"], out var ppc) ? ppc : 10;
        var overlapPages = int.TryParse(configuration["Settings:OverlapPages"], out var op) ? op : 1;

        if (string.IsNullOrEmpty(openAiApiKey) || openAiApiKey == "YOUR_OPENAI_API_KEY_HERE")
        {
            Console.WriteLine("ERROR: OpenAI API key not configured!");
            Console.WriteLine("Set it in appsettings.json or OPENAI_API_KEY environment variable.");
            return;
        }

        Console.WriteLine($"Configuration:");
        Console.WriteLine($"  - OpenAI Model: {openAiModel}");
        Console.WriteLine($"  - Pages per chunk: {pagesPerChunk}");
        Console.WriteLine($"  - Overlap pages: {overlapPages}");

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
                    await ProcessPdfs(pdfDirectory, openAiApiKey, openAiModel, pagesPerChunk, overlapPages, db);
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

    static async Task ProcessPdfs(string pdfDirectory, string apiKey, string modelName, int pagesPerChunk, int overlapPages, RecipeDbContext db)
    {
        Console.WriteLine($"\nProcessing PDFs from: {pdfDirectory}");
        Console.WriteLine($"Using chunked processing with {pagesPerChunk} pages per chunk and {overlapPages} page overlap\n");

        var pdfProcessor = new PdfProcessorService(pagesPerChunk, overlapPages);
        var openAiService = new OpenAIService(apiKey, modelName);

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
            var totalChunksProcessed = 0;

            foreach (var pdfFile in pdfFiles)
            {
                Console.WriteLine($"\n{'=',-60}");
                Console.WriteLine($"Processing: {Path.GetFileName(pdfFile)}");
                Console.WriteLine($"{'=',-60}");

                try
                {
                    // Extract text from PDF in chunks with overlap
                    var chunks = pdfProcessor.ExtractTextInChunks(pdfFile);

                    foreach (var chunk in chunks)
                    {
                        try
                        {
                            // Send chunk to OpenAI for recipe extraction
                            var recipes = await openAiService.ExtractRecipesFromChunk(chunk);

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
                                        : MealType.Obiad,
                                    CreatedAt = DateTime.Now
                                };

                                db.InsertRecipe(recipe);
                                Console.WriteLine($"  ✓ Saved: {recipe.Name} ({recipe.MealType})");
                                totalRecipesExtracted++;
                            }

                            totalChunksProcessed++;

                            // Add delay between chunks to avoid rate limiting
                            await Task.Delay(2000);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ✗ Error processing chunk {chunk.ChunkNumber}: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"\nCompleted {Path.GetFileName(pdfFile)}: {chunks.Count} chunks processed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error processing {Path.GetFileName(pdfFile)}: {ex.Message}");
                }
            }

            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"✓ Processing complete!");
            Console.WriteLine($"Total chunks processed: {totalChunksProcessed}");
            Console.WriteLine($"Total recipes extracted: {totalRecipesExtracted}");
            Console.WriteLine($"{'=',-60}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void GetRandomMeals(RecipeDbContext db)
    {
        Console.WriteLine("\n=== Plan Posiłków na Dziś ===\n");

        try
        {
            var sniadanie = db.GetRandomRecipesByMealType(MealType.Sniadanie, 1);
            var obiad = db.GetRandomRecipesByMealType(MealType.Obiad, 1);
            var kolacja = db.GetRandomRecipesByMealType(MealType.Kolacja, 1);

            if (sniadanie.Count > 0)
            {
                Console.WriteLine("ŚNIADANIE:");
                PrintRecipe(sniadanie[0]);
            }
            else
            {
                Console.WriteLine("ŚNIADANIE: Brak przepisów w bazie danych");
            }

            if (obiad.Count > 0)
            {
                Console.WriteLine("\nOBIAD:");
                PrintRecipe(obiad[0]);
            }
            else
            {
                Console.WriteLine("\nOBIAD: Brak przepisów w bazie danych");
            }

            if (kolacja.Count > 0)
            {
                Console.WriteLine("\nKOLACJA:");
                PrintRecipe(kolacja[0]);
            }
            else
            {
                Console.WriteLine("\nKOLACJA: Brak przepisów w bazie danych");
            }

            // Calculate daily totals
            var totalCalories = sniadanie.Sum(r => r.Calories) + obiad.Sum(r => r.Calories) + kolacja.Sum(r => r.Calories);
            var totalProtein = sniadanie.Sum(r => r.Protein) + obiad.Sum(r => r.Protein) + kolacja.Sum(r => r.Protein);
            var totalCarbs = sniadanie.Sum(r => r.Carbohydrates) + obiad.Sum(r => r.Carbohydrates) + kolacja.Sum(r => r.Carbohydrates);
            var totalFat = sniadanie.Sum(r => r.Fat) + obiad.Sum(r => r.Fat) + kolacja.Sum(r => r.Fat);

            Console.WriteLine("\n=== PODSUMOWANIE DNIA ===");
            Console.WriteLine($"Kalorie: {totalCalories} kcal");
            Console.WriteLine($"Białko: {totalProtein:F1}g");
            Console.WriteLine($"Węglowodany: {totalCarbs:F1}g");
            Console.WriteLine($"Tłuszcze: {totalFat:F1}g");
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
