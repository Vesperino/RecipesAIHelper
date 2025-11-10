using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;
using RecipesAIHelper.Services;

namespace RecipesAIHelper;

class Program
{
    static async Task Main(string[] args)
    {
        // Check if running in console mode
        if (args.Contains("--console"))
        {
            await RunConsoleMode(args);
            return;
        }

        // Run web mode
        await RunWebMode(args);
    }

    static async Task RunWebMode(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configuration
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables();

        var openAiApiKey = builder.Configuration["OpenAI:ApiKey"] ??
            Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
            string.Empty;

        var openAiModel = builder.Configuration["OpenAI:Model"] ?? "gpt-5-nano-2025-08-07";
        var databasePath = builder.Configuration["Settings:DatabasePath"] ?? "recipes.db";
        var pagesPerChunk = int.TryParse(builder.Configuration["Settings:PagesPerChunk"], out var ppc) ? ppc : 30;
        var overlapPages = int.TryParse(builder.Configuration["Settings:OverlapPages"], out var op) ? op : 2;

        if (string.IsNullOrEmpty(openAiApiKey) || openAiApiKey == "YOUR_OPENAI_API_KEY_HERE")
        {
            Console.WriteLine("ERROR: OpenAI API key not configured!");
            Console.WriteLine("Set it in appsettings.json or OPENAI_API_KEY environment variable.");
            return;
        }

        // Add services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Register services
        builder.Services.AddSingleton(new RecipeDbContext(databasePath));
        builder.Services.AddSingleton(new PdfProcessorService(pagesPerChunk, overlapPages));
        builder.Services.AddSingleton(new OpenAIService(openAiApiKey, openAiModel));

        var app = builder.Build();

        // Initialize database
        var db = app.Services.GetRequiredService<RecipeDbContext>();
        db.InitializeDatabase();

        Console.WriteLine("=== Recipe AI Helper - Web Mode ===");
        Console.WriteLine($"Database: {databasePath}");
        Console.WriteLine($"Recipes in database: {db.GetRecipeCount()}");
        Console.WriteLine($"OpenAI Model: {openAiModel}");
        Console.WriteLine();

        // Configure middleware
        app.UseCors();
        app.UseStaticFiles();
        app.MapControllers();

        // Fallback to index.html
        app.MapFallbackToFile("index.html");

        Console.WriteLine("Web interface available at:");
        Console.WriteLine("  http://localhost:5000");
        Console.WriteLine("  https://localhost:5001");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop");

        await app.RunAsync("http://localhost:5000");
    }

    static async Task RunConsoleMode(string[] args)
    {
        Console.WriteLine("=== Recipe AI Helper - Console Mode ===\n");

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

        var pagesPerChunk = int.TryParse(configuration["Settings:PagesPerChunk"], out var ppc) ? ppc : 30;
        var overlapPages = int.TryParse(configuration["Settings:OverlapPages"], out var op) ? op : 2;
        var delayBetweenChunks = int.TryParse(configuration["Settings:DelayBetweenChunksMs"], out var delay) ? delay : 3000;
        var checkDuplicates = bool.TryParse(configuration["Settings:CheckDuplicates"], out var checkDup) ? checkDup : true;
        var recentRecipesContext = int.TryParse(configuration["Settings:RecentRecipesContext"], out var recentCtx) ? recentCtx : 10;

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
        Console.WriteLine($"  - Delay between chunks: {delayBetweenChunks}ms");
        Console.WriteLine($"  - Check duplicates: {checkDuplicates}");
        Console.WriteLine($"  - Recent recipes context: {recentRecipesContext}");

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
                    await ProcessPdfs(pdfDirectory, openAiApiKey, openAiModel, pagesPerChunk, overlapPages,
                        delayBetweenChunks, checkDuplicates, recentRecipesContext, db);
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

    static async Task ProcessPdfs(string pdfDirectory, string apiKey, string modelName, int pagesPerChunk,
        int overlapPages, int delayMs, bool checkDuplicates, int recentRecipesContext, RecipeDbContext db)
    {
        Console.WriteLine($"\n{'=',-80}");
        Console.WriteLine($"ROZPOCZĘCIE PRZETWARZANIA PDF");
        Console.WriteLine($"{'=',-80}");
        Console.WriteLine($"Folder: {pdfDirectory}");
        Console.WriteLine($"Chunking: {pagesPerChunk} stron per chunk, {overlapPages} stron overlap");
        Console.WriteLine($"Rate limiting: {delayMs}ms opóźnienia między chunkami");
        Console.WriteLine($"Sprawdzanie duplikatów: {(checkDuplicates ? "TAK" : "NIE")}");
        Console.WriteLine($"{'=',-80}\n");

        var pdfProcessor = new PdfProcessorService(pagesPerChunk, overlapPages);
        var openAiService = new OpenAIService(apiKey, modelName);

        try
        {
            var pdfFiles = pdfProcessor.GetAllPdfFiles(pdfDirectory);
            Console.WriteLine($"📄 Znaleziono {pdfFiles.Count} plików PDF\n");

            if (pdfFiles.Count == 0)
            {
                Console.WriteLine("❌ Brak plików PDF w katalogu.");
                return;
            }

            var totalRecipesExtracted = 0;
            var totalRecipesSaved = 0;
            var totalDuplicatesSkipped = 0;
            var totalChunksProcessed = 0;
            var totalErrors = 0;

            foreach (var pdfFile in pdfFiles)
            {
                Console.WriteLine($"\n{'=',-80}");
                Console.WriteLine($"📋 Przetwarzanie: {Path.GetFileName(pdfFile)}");
                Console.WriteLine($"{'=',-80}");

                var fileRecipesExtracted = 0;
                var fileRecipesSaved = 0;
                var fileDuplicatesSkipped = 0;

                try
                {
                    // Extract text from PDF in chunks with overlap
                    var chunks = pdfProcessor.ExtractTextInChunks(pdfFile);
                    Console.WriteLine($"📊 PDF podzielony na {chunks.Count} chunków\n");

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        var chunk = chunks[i];

                        try
                        {
                            Console.WriteLine($"[Chunk {chunk.ChunkNumber}/{chunks.Count}] Strony {chunk.StartPage}-{chunk.EndPage}");
                            Console.WriteLine($"  Rozmiar tekstu: {chunk.Text.Length} znaków");

                            // Get recent recipes for context (to avoid duplicates)
                            var recentRecipes = checkDuplicates
                                ? db.GetRecentRecipes(recentRecipesContext)
                                : null;

                            if (recentRecipes != null && recentRecipes.Count > 0)
                            {
                                Console.WriteLine($"  Kontekst: {recentRecipes.Count} ostatnich przepisów w bazie");
                            }

                            // Send chunk to OpenAI for recipe extraction
                            Console.WriteLine($"  ⏳ Wysyłanie do OpenAI ({modelName})...");
                            var startTime = DateTime.Now;
                            var recipes = await openAiService.ExtractRecipesFromChunk(chunk, recentRecipes);
                            var processingTime = (DateTime.Now - startTime).TotalSeconds;

                            Console.WriteLine($"  ✅ Otrzymano {recipes.Count} przepisów (czas: {processingTime:F1}s)");
                            fileRecipesExtracted += recipes.Count;

                            // Save to database with duplicate checking
                            foreach (var recipeData in recipes)
                            {
                                try
                                {
                                    // Validate recipe data
                                    if (string.IsNullOrWhiteSpace(recipeData.Name))
                                    {
                                        Console.WriteLine($"    ⚠️  Pominięto przepis bez nazwy");
                                        continue;
                                    }

                                    if (recipeData.Ingredients == null || recipeData.Ingredients.Count == 0)
                                    {
                                        Console.WriteLine($"    ⚠️  Pominięto '{recipeData.Name}' - brak składników");
                                        continue;
                                    }

                                    // Check for duplicates
                                    if (checkDuplicates)
                                    {
                                        if (db.RecipeExists(recipeData.Name))
                                        {
                                            Console.WriteLine($"    ⏭️  Pominięto '{recipeData.Name}' - duplikat (dokładne dopasowanie)");
                                            fileDuplicatesSkipped++;
                                            continue;
                                        }

                                        var similarRecipe = db.FindSimilarRecipe(recipeData.Name, 0.8);
                                        if (similarRecipe != null)
                                        {
                                            Console.WriteLine($"    ⏭️  Pominięto '{recipeData.Name}' - podobny do '{similarRecipe.Name}'");
                                            fileDuplicatesSkipped++;
                                            continue;
                                        }
                                    }

                                    // Save recipe
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
                                    Console.WriteLine($"    ✅ Zapisano: {recipe.Name} ({recipe.MealType}) - {recipe.Calories} kcal");
                                    fileRecipesSaved++;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"    ❌ Błąd zapisu '{recipeData.Name}': {ex.Message}");
                                    totalErrors++;
                                }
                            }

                            totalChunksProcessed++;

                            // Progress indicator
                            var progress = (float)(i + 1) / chunks.Count * 100;
                            Console.WriteLine($"  📈 Postęp pliku: {progress:F0}%\n");

                            // Add delay between chunks to avoid rate limiting
                            if (i < chunks.Count - 1) // Don't delay after last chunk
                            {
                                Console.WriteLine($"  ⏸️  Oczekiwanie {delayMs}ms przed następnym chunkiem...\n");
                                await Task.Delay(delayMs);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ❌ Błąd przetwarzania chunku {chunk.ChunkNumber}: {ex.Message}");
                            if (ex.InnerException != null)
                            {
                                Console.WriteLine($"     Szczegóły: {ex.InnerException.Message}");
                            }
                            totalErrors++;
                        }
                    }

                    // File summary
                    Console.WriteLine($"\n{'─',-80}");
                    Console.WriteLine($"✅ Zakończono plik: {Path.GetFileName(pdfFile)}");
                    Console.WriteLine($"   Chunków przetworzonych: {chunks.Count}");
                    Console.WriteLine($"   Przepisów wyekstrahowanych: {fileRecipesExtracted}");
                    Console.WriteLine($"   Przepisów zapisanych: {fileRecipesSaved}");
                    Console.WriteLine($"   Duplikatów pominiętych: {fileDuplicatesSkipped}");
                    Console.WriteLine($"{'─',-80}\n");

                    totalRecipesExtracted += fileRecipesExtracted;
                    totalRecipesSaved += fileRecipesSaved;
                    totalDuplicatesSkipped += fileDuplicatesSkipped;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Błąd przetwarzania pliku {Path.GetFileName(pdfFile)}: {ex.Message}");
                    totalErrors++;
                }
            }

            // Final summary
            Console.WriteLine($"\n{'=',-80}");
            Console.WriteLine($"🎉 PRZETWARZANIE ZAKOŃCZONE");
            Console.WriteLine($"{'=',-80}");
            Console.WriteLine($"📁 Plików przetworzonych: {pdfFiles.Count}");
            Console.WriteLine($"📦 Chunków przetworzonych: {totalChunksProcessed}");
            Console.WriteLine($"📋 Przepisów wyekstrahowanych: {totalRecipesExtracted}");
            Console.WriteLine($"✅ Przepisów zapisanych: {totalRecipesSaved}");
            Console.WriteLine($"⏭️  Duplikatów pominiętych: {totalDuplicatesSkipped}");
            Console.WriteLine($"❌ Błędów: {totalErrors}");
            Console.WriteLine($"📊 Obecna liczba przepisów w bazie: {db.GetRecipeCount()}");
            Console.WriteLine($"{'=',-80}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Krytyczny błąd: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Szczegóły: {ex.InnerException.Message}");
            }
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
