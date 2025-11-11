using Microsoft.Data.Sqlite;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Data;

public class RecipeDbContext : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public RecipeDbContext(string databasePath = "recipes.db")
    {
        _connectionString = $"Data Source={databasePath}";
    }

    private SqliteConnection GetConnection()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
        }
        return _connection;
    }

    public void InitializeDatabase()
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Recipes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                Ingredients TEXT NOT NULL,
                Instructions TEXT NOT NULL,
                Calories INTEGER NOT NULL,
                Protein REAL NOT NULL,
                Carbohydrates REAL NOT NULL,
                Fat REAL NOT NULL,
                MealType INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                ImagePath TEXT NULL,
                ImageUrl TEXT NULL,
                Servings INTEGER NULL,
                NutritionVariantsJson TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_recipes_mealtype ON Recipes(MealType);

            CREATE TABLE IF NOT EXISTS AIProviders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Model TEXT NOT NULL,
                IsActive INTEGER DEFAULT 0,
                Priority INTEGER DEFAULT 0,
                MaxPagesPerChunk INTEGER DEFAULT 3,
                SupportsDirectPDF INTEGER DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_aiproviders_active ON AIProviders(IsActive, Priority DESC);

            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                Type TEXT NOT NULL,
                Description TEXT,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS MealPlans (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                IsActive INTEGER DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_mealplans_active ON MealPlans(IsActive);

            CREATE TABLE IF NOT EXISTS MealPlanDays (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealPlanId INTEGER NOT NULL,
                DayOfWeek INTEGER NOT NULL,
                Date TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (MealPlanId) REFERENCES MealPlans(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_mealplandays_plan ON MealPlanDays(MealPlanId);
            CREATE INDEX IF NOT EXISTS idx_mealplandays_date ON MealPlanDays(Date);

            CREATE TABLE IF NOT EXISTS MealPlanEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealPlanDayId INTEGER NOT NULL,
                RecipeId INTEGER NOT NULL,
                MealType INTEGER NOT NULL,
                [Order] INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (MealPlanDayId) REFERENCES MealPlanDays(Id) ON DELETE CASCADE,
                FOREIGN KEY (RecipeId) REFERENCES Recipes(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_mealplanentries_day ON MealPlanEntries(MealPlanDayId);
            CREATE INDEX IF NOT EXISTS idx_mealplanentries_recipe ON MealPlanEntries(RecipeId);

            CREATE TABLE IF NOT EXISTS ShoppingLists (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MealPlanId INTEGER NOT NULL,
                ItemsJson TEXT NOT NULL,
                GeneratedAt TEXT NOT NULL,
                FOREIGN KEY (MealPlanId) REFERENCES MealPlans(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_shoppinglists_plan ON ShoppingLists(MealPlanId);

            CREATE TABLE IF NOT EXISTS ProcessedFiles (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileName TEXT NOT NULL,
                FileChecksum TEXT NOT NULL UNIQUE,
                FileSizeBytes INTEGER NOT NULL,
                ProcessedAt TEXT NOT NULL,
                RecipesExtracted INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_processedfiles_checksum ON ProcessedFiles(FileChecksum);
            CREATE INDEX IF NOT EXISTS idx_processedfiles_filename ON ProcessedFiles(FileName);
        ";
        command.ExecuteNonQuery();

        // Migrate existing data from Recipes table if ImagePath column doesn't exist
        MigrateRecipesTable();

        // Migrate AIProviders table - remove ApiKey column
        MigrateAIProvidersTable();

        // Initialize default settings if not exist
        InitializeDefaultSettings();
    }

    private void MigrateRecipesTable()
    {
        var connection = GetConnection();

        // Check if columns exist
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA table_info(Recipes)";

        var hasImagePath = false;
        var hasNutritionVariants = false;
        var hasServings = false;
        using (var reader = checkCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var columnName = reader.GetString(1);
                if (columnName == "ImagePath")
                    hasImagePath = true;
                if (columnName == "NutritionVariantsJson")
                    hasNutritionVariants = true;
                if (columnName == "Servings")
                    hasServings = true;
            }
        }

        // Add ImagePath columns if they don't exist (for existing databases)
        if (!hasImagePath)
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = @"
                ALTER TABLE Recipes ADD COLUMN ImagePath TEXT NULL;
                ALTER TABLE Recipes ADD COLUMN ImageUrl TEXT NULL;
            ";
            try
            {
                alterCommand.ExecuteNonQuery();
                Console.WriteLine("✅ Dodano kolumny ImagePath i ImageUrl do tabeli Recipes");
            }
            catch
            {
                // Columns might already exist, ignore error
            }
        }

        // Add NutritionVariantsJson column if it doesn't exist
        if (!hasNutritionVariants)
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Recipes ADD COLUMN NutritionVariantsJson TEXT NULL;";
            try
            {
                alterCommand.ExecuteNonQuery();
                Console.WriteLine("✅ Dodano kolumnę NutritionVariantsJson do tabeli Recipes");
            }
            catch
            {
                // Column might already exist, ignore error
            }
        }

        // Add Servings column if it doesn't exist
        if (!hasServings)
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Recipes ADD COLUMN Servings INTEGER NULL;";
            try
            {
                alterCommand.ExecuteNonQuery();
                Console.WriteLine("✅ Dodano kolumnę Servings do tabeli Recipes");
            }
            catch
            {
                // Column might already exist, ignore error
            }
        }
    }

    private void MigrateAIProvidersTable()
    {
        var connection = GetConnection();

        // Check if ApiKey column exists in AIProviders
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "PRAGMA table_info(AIProviders)";

        var hasApiKeyColumn = false;
        using (var reader = checkCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var columnName = reader.GetString(1);
                if (columnName == "ApiKey")
                {
                    hasApiKeyColumn = true;
                    break;
                }
            }
        }

        if (hasApiKeyColumn)
        {
            Console.WriteLine("🔄 Migracja AIProviders: przenoszenie kluczy API do Settings...");

            // Step 1: Read existing providers and their API keys
            var providerApiKeys = new Dictionary<string, string>();
            var readCommand = connection.CreateCommand();
            readCommand.CommandText = "SELECT Name, ApiKey FROM AIProviders WHERE ApiKey IS NOT NULL AND ApiKey != ''";
            using (var reader = readCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var apiKey = reader.GetString(1);
                    providerApiKeys[name.ToLowerInvariant()] = apiKey;
                }
            }

            // Step 2: Save API keys to Settings table
            foreach (var kvp in providerApiKeys)
            {
                var providerName = kvp.Key;
                var apiKey = kvp.Value;

                string settingsKey;
                string description;

                if (providerName == "openai")
                {
                    settingsKey = "OpenAI_ApiKey";
                    description = "Klucz API OpenAI (dla ekstrakcji przepisów i generowania obrazów)";
                }
                else if (providerName == "gemini" || providerName == "google")
                {
                    settingsKey = "Gemini_ApiKey";
                    description = "Klucz API Google Gemini (dla ekstrakcji przepisów i generowania obrazów)";
                }
                else
                {
                    continue; // Skip unknown providers
                }

                // Check if key already exists in Settings
                var checkKeyCommand = connection.CreateCommand();
                checkKeyCommand.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
                checkKeyCommand.Parameters.AddWithValue("@key", settingsKey);
                var existingKey = checkKeyCommand.ExecuteScalar() as string;

                // Only update if Settings doesn't have a key yet, or it's empty
                if (string.IsNullOrEmpty(existingKey))
                {
                    UpsertSetting(settingsKey, apiKey, "string", description);
                    Console.WriteLine($"   ✅ Przeniesiono klucz {settingsKey}");
                }
            }

            // Step 3: Recreate AIProviders table without ApiKey column
            // SQLite doesn't support DROP COLUMN, so we need to recreate the table
            try
            {
                var recreateCommand = connection.CreateCommand();
                recreateCommand.CommandText = @"
                    -- Create temporary table with new schema
                    CREATE TABLE AIProviders_new (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        Model TEXT NOT NULL,
                        IsActive INTEGER DEFAULT 0,
                        Priority INTEGER DEFAULT 0,
                        MaxPagesPerChunk INTEGER DEFAULT 3,
                        SupportsDirectPDF INTEGER DEFAULT 0,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );

                    -- Copy data (excluding ApiKey)
                    INSERT INTO AIProviders_new (Id, Name, Model, IsActive, Priority, MaxPagesPerChunk, SupportsDirectPDF, CreatedAt, UpdatedAt)
                    SELECT Id, Name, Model, IsActive, Priority, MaxPagesPerChunk, SupportsDirectPDF, CreatedAt, UpdatedAt
                    FROM AIProviders;

                    -- Drop old table
                    DROP TABLE AIProviders;

                    -- Rename new table
                    ALTER TABLE AIProviders_new RENAME TO AIProviders;

                    -- Recreate index
                    CREATE INDEX IF NOT EXISTS idx_aiproviders_active ON AIProviders(IsActive, Priority DESC);
                ";
                recreateCommand.ExecuteNonQuery();

                Console.WriteLine("   ✅ Usunięto kolumnę ApiKey z tabeli AIProviders");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Błąd podczas migracji AIProviders: {ex.Message}");
                // Don't fail the initialization, table might already be migrated
            }
        }
        else
        {
            Console.WriteLine("✅ Tabela AIProviders już jest zmigrowana (brak kolumny ApiKey)");
        }
    }

    private void InitializeDefaultSettings()
    {
        var connection = GetConnection();

        // Check if settings exist
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(*) FROM Settings";
        var count = Convert.ToInt32(checkCommand.ExecuteScalar());

        if (count == 0)
        {
            // Insert default settings
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO Settings (Key, Value, Type, Description, UpdatedAt) VALUES
                ('PdfSourceDirectory', 'C:\Users\Karolina\Downloads\Dieta', 'string', 'Katalog źródłowy z plikami PDF', datetime('now')),
                ('DatabasePath', 'recipes.db', 'string', 'Ścieżka do pliku bazy danych', datetime('now')),
                ('PagesPerChunk', '4', 'int', 'Liczba stron na chunk', datetime('now')),
                ('DelayBetweenChunksMs', '5000', 'int', 'Opóźnienie między chunkami (ms)', datetime('now')),
                ('CheckDuplicates', 'true', 'bool', 'Sprawdzanie duplikatów', datetime('now')),
                ('RecentRecipesContext', '2', 'int', 'Liczba ostatnich przepisów do kontekstu', datetime('now'))
            ";
            insertCommand.ExecuteNonQuery();
        }

        // Migrate image generation settings (if they don't exist)
        MigrateImageGenerationSettings();
    }

    private void MigrateImageGenerationSettings()
    {
        var connection = GetConnection();

        // Check if ImageGenerationProvider exists
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT COUNT(*) FROM Settings WHERE Key = 'ImageGenerationProvider'";
        var exists = Convert.ToInt32(checkCommand.ExecuteScalar()) > 0;

        if (!exists)
        {
            Console.WriteLine("🔄 Migracja ustawień generowania obrazów...");

            // Insert image generation settings with empty API keys (user will configure)
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO Settings (Key, Value, Type, Description, UpdatedAt) VALUES
                ('ImageGenerationProvider', 'OpenAI', 'string', 'Provider generowania obrazów (OpenAI lub Gemini)', datetime('now')),
                ('OpenAI_ApiKey', '', 'string', 'Klucz API OpenAI dla generowania obrazów', datetime('now')),
                ('OpenAI_ImageModel', 'gpt-image-1', 'string', 'Model OpenAI do generowania obrazów', datetime('now')),
                ('Gemini_ApiKey', '', 'string', 'Klucz API Google Gemini dla generowania obrazów', datetime('now')),
                ('Gemini_ImageModel', 'imagen-4.0-ultra-generate-001', 'string', 'Model Gemini do generowania obrazów', datetime('now'))
            ";
            insertCommand.ExecuteNonQuery();

            Console.WriteLine("✅ Dodano ustawienia generowania obrazów");
        }
    }

    public void InsertRecipe(Recipe recipe)
    {
        // Debug: Log what's being saved
        Console.WriteLine($"🔍 DEBUG InsertRecipe '{recipe.Name}':");
        Console.WriteLine($"   - NutritionVariantsJson parameter value: {(recipe.NutritionVariantsJson == null ? "NULL" : $"{recipe.NutritionVariantsJson.Length} znaków")}");
        if (recipe.NutritionVariantsJson != null && recipe.NutritionVariantsJson.Length < 500)
        {
            Console.WriteLine($"   - JSON: {recipe.NutritionVariantsJson}");
        }

        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Recipes (Name, Description, Ingredients, Instructions, Calories, Protein, Carbohydrates, Fat, MealType, CreatedAt, Servings, NutritionVariantsJson)
            VALUES (@name, @description, @ingredients, @instructions, @calories, @protein, @carbs, @fat, @mealType, @createdAt, @servings, @nutritionVariants)
        ";

        command.Parameters.AddWithValue("@name", recipe.Name);
        command.Parameters.AddWithValue("@description", recipe.Description);
        command.Parameters.AddWithValue("@ingredients", recipe.Ingredients);
        command.Parameters.AddWithValue("@instructions", recipe.Instructions);
        command.Parameters.AddWithValue("@calories", recipe.Calories);
        command.Parameters.AddWithValue("@protein", recipe.Protein);
        command.Parameters.AddWithValue("@carbs", recipe.Carbohydrates);
        command.Parameters.AddWithValue("@fat", recipe.Fat);
        command.Parameters.AddWithValue("@mealType", (int)recipe.MealType);
        command.Parameters.AddWithValue("@createdAt", recipe.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@servings", (object?)recipe.Servings ?? DBNull.Value);
        command.Parameters.AddWithValue("@nutritionVariants", (object?)recipe.NutritionVariantsJson ?? DBNull.Value);

        command.ExecuteNonQuery();

        // Debug: Verify what was saved
        var verifyCommand = connection.CreateCommand();
        verifyCommand.CommandText = "SELECT NutritionVariantsJson FROM Recipes WHERE Name = @name ORDER BY Id DESC LIMIT 1";
        verifyCommand.Parameters.AddWithValue("@name", recipe.Name);
        var savedValue = verifyCommand.ExecuteScalar() as string;
        Console.WriteLine($"   - Zweryfikowano wartość w bazie: {(savedValue == null ? "NULL" : $"{savedValue.Length} znaków")}");
        if (savedValue != null && savedValue.Length < 500)
        {
            Console.WriteLine($"   - Wartość z bazy: {savedValue}");
        }
    }

    public List<Recipe> GetRandomRecipesByMealType(MealType mealType, int count = 1)
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT * FROM Recipes
            WHERE MealType = @mealType
            ORDER BY RANDOM()
            LIMIT @count
        ";
        command.Parameters.AddWithValue("@mealType", (int)mealType);
        command.Parameters.AddWithValue("@count", count);

        var recipes = new List<Recipe>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            recipes.Add(new Recipe
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                Ingredients = reader.GetString(3),
                Instructions = reader.GetString(4),
                Calories = reader.GetInt32(5),
                Protein = reader.GetDouble(6),
                Carbohydrates = reader.GetDouble(7),
                Fat = reader.GetDouble(8),
                MealType = (MealType)reader.GetInt32(9),
                CreatedAt = DateTime.Parse(reader.GetString(10)),
                ImagePath = reader.IsDBNull(11) ? null : reader.GetString(11),
                ImageUrl = reader.IsDBNull(12) ? null : reader.GetString(12),
                Servings = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                NutritionVariantsJson = reader.IsDBNull(14) ? null : reader.GetString(14)
            });
        }

        return recipes;
    }

    /// <summary>
    /// Get recipes by meal type within a calorie range
    /// </summary>
    public List<Recipe> GetRecipesByCalorieRange(MealType mealType, int minCalories, int maxCalories)
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT * FROM Recipes
            WHERE MealType = @mealType
            AND Calories >= @minCalories
            AND Calories <= @maxCalories
            ORDER BY RANDOM()
        ";
        command.Parameters.AddWithValue("@mealType", (int)mealType);
        command.Parameters.AddWithValue("@minCalories", minCalories);
        command.Parameters.AddWithValue("@maxCalories", maxCalories);

        var recipes = new List<Recipe>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            recipes.Add(new Recipe
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                Ingredients = reader.GetString(3),
                Instructions = reader.GetString(4),
                Calories = reader.GetInt32(5),
                Protein = reader.GetDouble(6),
                Carbohydrates = reader.GetDouble(7),
                Fat = reader.GetDouble(8),
                MealType = (MealType)reader.GetInt32(9),
                CreatedAt = DateTime.Parse(reader.GetString(10)),
                ImagePath = reader.IsDBNull(11) ? null : reader.GetString(11),
                ImageUrl = reader.IsDBNull(12) ? null : reader.GetString(12),
                Servings = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                NutritionVariantsJson = reader.IsDBNull(14) ? null : reader.GetString(14)
            });
        }

        return recipes;
    }

    public List<Recipe> GetAllRecipes()
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Recipes ORDER BY CreatedAt DESC";

        var recipes = new List<Recipe>();
        using var reader = command.ExecuteReader();

        // Debug: Log column count
        var columnCount = reader.FieldCount;
        Console.WriteLine($"🔍 DEBUG GetAllRecipes: Liczba kolumn w zapytaniu = {columnCount}");

        while (reader.Read())
        {
            var recipe = new Recipe
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                Ingredients = reader.GetString(3),
                Instructions = reader.GetString(4),
                Calories = reader.GetInt32(5),
                Protein = reader.GetDouble(6),
                Carbohydrates = reader.GetDouble(7),
                Fat = reader.GetDouble(8),
                MealType = (MealType)reader.GetInt32(9),
                CreatedAt = DateTime.Parse(reader.GetString(10)),
                ImagePath = reader.IsDBNull(11) ? null : reader.GetString(11),
                ImageUrl = reader.IsDBNull(12) ? null : reader.GetString(12),
                Servings = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                NutritionVariantsJson = columnCount > 14 && !reader.IsDBNull(14) ? reader.GetString(14) : null
            };

            // Debug: Log first recipe's NutritionVariantsJson
            if (recipes.Count == 0)
            {
                Console.WriteLine($"🔍 DEBUG Pierwszy przepis '{recipe.Name}':");
                Console.WriteLine($"   - NutritionVariantsJson z bazy: {(recipe.NutritionVariantsJson == null ? "NULL" : $"{recipe.NutritionVariantsJson.Length} znaków")}");
                if (recipe.NutritionVariantsJson != null && recipe.NutritionVariantsJson.Length < 200)
                {
                    Console.WriteLine($"   - JSON: {recipe.NutritionVariantsJson}");
                }
            }

            recipes.Add(recipe);
        }

        return recipes;
    }

    public int GetRecipeCount()
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Recipes";

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public bool RecipeExists(string name)
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) FROM Recipes
            WHERE LOWER(TRIM(Name)) = LOWER(TRIM(@name))
        ";
        command.Parameters.AddWithValue("@name", name);

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public Recipe? FindSimilarRecipe(string name, double similarityThreshold = 0.8)
    {
        // Simple similarity check - can be enhanced with Levenshtein distance
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Recipes";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var existingName = reader.GetString(1);
            var similarity = CalculateSimilarity(name.ToLower().Trim(), existingName.ToLower().Trim());

            if (similarity >= similarityThreshold)
            {
                return new Recipe
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    Ingredients = reader.GetString(3),
                    Instructions = reader.GetString(4),
                    Calories = reader.GetInt32(5),
                    Protein = reader.GetDouble(6),
                    Carbohydrates = reader.GetDouble(7),
                    Fat = reader.GetDouble(8),
                    MealType = (MealType)reader.GetInt32(9),
                    CreatedAt = DateTime.Parse(reader.GetString(10)),
                    ImagePath = reader.IsDBNull(11) ? null : reader.GetString(11),
                    ImageUrl = reader.IsDBNull(12) ? null : reader.GetString(12),
                    Servings = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    NutritionVariantsJson = reader.IsDBNull(14) ? null : reader.GetString(14)
                };
            }
        }

        return null;
    }

    public List<Recipe> GetRecentRecipes(int count = 5)
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT * FROM Recipes
            ORDER BY CreatedAt DESC
            LIMIT @count
        ";
        command.Parameters.AddWithValue("@count", count);

        var recipes = new List<Recipe>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            recipes.Add(new Recipe
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                Ingredients = reader.GetString(3),
                Instructions = reader.GetString(4),
                Calories = reader.GetInt32(5),
                Protein = reader.GetDouble(6),
                Carbohydrates = reader.GetDouble(7),
                Fat = reader.GetDouble(8),
                MealType = (MealType)reader.GetInt32(9),
                CreatedAt = DateTime.Parse(reader.GetString(10)),
                ImagePath = reader.IsDBNull(11) ? null : reader.GetString(11),
                ImageUrl = reader.IsDBNull(12) ? null : reader.GetString(12),
                Servings = reader.IsDBNull(13) ? null : reader.GetInt32(13),
                NutritionVariantsJson = reader.IsDBNull(14) ? null : reader.GetString(14)
            });
        }

        return recipes;
    }

    private double CalculateSimilarity(string s1, string s2)
    {
        // Simple Jaccard similarity based on words
        var words1 = s1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = s2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (words1.Count == 0 && words2.Count == 0) return 1.0;
        if (words1.Count == 0 || words2.Count == 0) return 0.0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return (double)intersection / union;
    }

    public bool UpdateRecipe(Recipe recipe)
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Recipes
            SET Name = @name,
                Description = @description,
                Ingredients = @ingredients,
                Instructions = @instructions,
                Calories = @calories,
                Protein = @protein,
                Carbohydrates = @carbohydrates,
                Fat = @fat,
                MealType = @mealType,
                Servings = @servings,
                NutritionVariantsJson = @nutritionVariants
            WHERE Id = @id
        ";

        command.Parameters.AddWithValue("@id", recipe.Id);
        command.Parameters.AddWithValue("@name", recipe.Name);
        command.Parameters.AddWithValue("@description", recipe.Description ?? "");
        command.Parameters.AddWithValue("@ingredients", recipe.Ingredients);
        command.Parameters.AddWithValue("@instructions", recipe.Instructions ?? "");
        command.Parameters.AddWithValue("@calories", recipe.Calories);
        command.Parameters.AddWithValue("@protein", recipe.Protein);
        command.Parameters.AddWithValue("@carbohydrates", recipe.Carbohydrates);
        command.Parameters.AddWithValue("@fat", recipe.Fat);
        command.Parameters.AddWithValue("@mealType", (int)recipe.MealType);
        command.Parameters.AddWithValue("@servings", (object?)recipe.Servings ?? DBNull.Value);
        command.Parameters.AddWithValue("@nutritionVariants", (object?)recipe.NutritionVariantsJson ?? DBNull.Value);

        return command.ExecuteNonQuery() > 0;
    }

    public bool DeleteRecipe(int id)
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Recipes WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        return command.ExecuteNonQuery() > 0;
    }

    // ==================== AI PROVIDERS METHODS ====================

    public List<AIProvider> GetAllAIProviders()
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AIProviders ORDER BY Priority DESC, Name";

        var providers = new List<AIProvider>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            providers.Add(new AIProvider
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Model = reader.GetString(2),
                IsActive = reader.GetInt32(3) == 1,
                Priority = reader.GetInt32(4),
                MaxPagesPerChunk = reader.GetInt32(5),
                SupportsDirectPDF = reader.GetInt32(6) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                UpdatedAt = DateTime.Parse(reader.GetString(8))
            });
        }

        return providers;
    }

    public AIProvider? GetAIProvider(int id)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AIProviders WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new AIProvider
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Model = reader.GetString(2),
                IsActive = reader.GetInt32(3) == 1,
                Priority = reader.GetInt32(4),
                MaxPagesPerChunk = reader.GetInt32(5),
                SupportsDirectPDF = reader.GetInt32(6) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                UpdatedAt = DateTime.Parse(reader.GetString(8))
            };
        }

        return null;
    }

    public AIProvider? GetActiveAIProvider()
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AIProviders WHERE IsActive = 1 ORDER BY Priority DESC LIMIT 1";

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new AIProvider
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Model = reader.GetString(2),
                IsActive = reader.GetInt32(3) == 1,
                Priority = reader.GetInt32(4),
                MaxPagesPerChunk = reader.GetInt32(5),
                SupportsDirectPDF = reader.GetInt32(6) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                UpdatedAt = DateTime.Parse(reader.GetString(8))
            };
        }

        return null;
    }

    public int InsertAIProvider(AIProvider provider)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO AIProviders (Name, Model, IsActive, Priority, MaxPagesPerChunk, SupportsDirectPDF, CreatedAt, UpdatedAt)
            VALUES (@name, @model, @isActive, @priority, @maxPages, @supportsPdf, @createdAt, @updatedAt);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@name", provider.Name);
        command.Parameters.AddWithValue("@model", provider.Model);
        command.Parameters.AddWithValue("@isActive", provider.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@priority", provider.Priority);
        command.Parameters.AddWithValue("@maxPages", provider.MaxPagesPerChunk);
        command.Parameters.AddWithValue("@supportsPdf", provider.SupportsDirectPDF ? 1 : 0);
        command.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("O"));
        command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("O"));

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public bool UpdateAIProvider(AIProvider provider)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE AIProviders
            SET Name = @name,
                Model = @model,
                IsActive = @isActive,
                Priority = @priority,
                MaxPagesPerChunk = @maxPages,
                SupportsDirectPDF = @supportsPdf,
                UpdatedAt = @updatedAt
            WHERE Id = @id
        ";

        command.Parameters.AddWithValue("@id", provider.Id);
        command.Parameters.AddWithValue("@name", provider.Name);
        command.Parameters.AddWithValue("@model", provider.Model);
        command.Parameters.AddWithValue("@isActive", provider.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@priority", provider.Priority);
        command.Parameters.AddWithValue("@maxPages", provider.MaxPagesPerChunk);
        command.Parameters.AddWithValue("@supportsPdf", provider.SupportsDirectPDF ? 1 : 0);
        command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("O"));

        return command.ExecuteNonQuery() > 0;
    }

    public bool DeleteAIProvider(int id)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM AIProviders WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        return command.ExecuteNonQuery() > 0;
    }

    public bool SetActiveAIProvider(int id)
    {
        var connection = GetConnection();

        // Deactivate all providers
        var deactivateCommand = connection.CreateCommand();
        deactivateCommand.CommandText = "UPDATE AIProviders SET IsActive = 0";
        deactivateCommand.ExecuteNonQuery();

        // Activate selected provider
        var activateCommand = connection.CreateCommand();
        activateCommand.CommandText = "UPDATE AIProviders SET IsActive = 1, UpdatedAt = @updatedAt WHERE Id = @id";
        activateCommand.Parameters.AddWithValue("@id", id);
        activateCommand.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("O"));

        return activateCommand.ExecuteNonQuery() > 0;
    }

    // ==================== SETTINGS METHODS ====================

    public Dictionary<string, string> GetAllSettings()
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM Settings";

        var settings = new Dictionary<string, string>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            settings[reader.GetString(0)] = reader.GetString(1);
        }

        return settings;
    }

    public string? GetSetting(string key)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
        command.Parameters.AddWithValue("@key", key);

        var result = command.ExecuteScalar();
        return result?.ToString();
    }

    public bool UpsertSetting(string key, string value, string type = "string", string? description = null)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Settings (Key, Value, Type, Description, UpdatedAt)
            VALUES (@key, @value, @type, @description, @updatedAt)
            ON CONFLICT(Key) DO UPDATE SET
                Value = @value,
                Type = @type,
                Description = COALESCE(@description, Description),
                UpdatedAt = @updatedAt
        ";

        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@description", description ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("O"));

        return command.ExecuteNonQuery() > 0;
    }

    public bool UpdateRecipeImage(int recipeId, string? imagePath, string? imageUrl)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Recipes
            SET ImagePath = @imagePath,
                ImageUrl = @imageUrl
            WHERE Id = @id
        ";

        command.Parameters.AddWithValue("@id", recipeId);
        command.Parameters.AddWithValue("@imagePath", imagePath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@imageUrl", imageUrl ?? (object)DBNull.Value);

        return command.ExecuteNonQuery() > 0;
    }

    // ==================== MEAL PLANS ====================

    public int CreateMealPlan(MealPlan mealPlan)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO MealPlans (Name, StartDate, EndDate, IsActive, CreatedAt, UpdatedAt)
            VALUES (@name, @startDate, @endDate, @isActive, @createdAt, @updatedAt);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@name", mealPlan.Name);
        command.Parameters.AddWithValue("@startDate", mealPlan.StartDate.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@endDate", mealPlan.EndDate.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@isActive", mealPlan.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public List<MealPlan> GetAllMealPlans()
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM MealPlans ORDER BY IsActive DESC, CreatedAt DESC";

        var plans = new List<MealPlan>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                plans.Add(new MealPlan
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    StartDate = DateTime.Parse(reader.GetString(2)),
                    EndDate = DateTime.Parse(reader.GetString(3)),
                    IsActive = reader.GetInt32(4) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    UpdatedAt = DateTime.Parse(reader.GetString(6))
                });
            }
        }
        return plans;
    }

    public MealPlan? GetMealPlan(int id)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM MealPlans WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        MealPlan? mealPlan = null;

        using (var reader = command.ExecuteReader())
        {
            if (reader.Read())
            {
                mealPlan = new MealPlan
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    StartDate = DateTime.Parse(reader.GetString(2)),
                    EndDate = DateTime.Parse(reader.GetString(3)),
                    IsActive = reader.GetInt32(4) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    UpdatedAt = DateTime.Parse(reader.GetString(6))
                };
            }
        }

        if (mealPlan != null)
        {
            // Load days and entries
            mealPlan.Days = GetMealPlanDays(mealPlan.Id);
        }

        return mealPlan;
    }

    public bool UpdateMealPlan(MealPlan mealPlan)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE MealPlans
            SET Name = @name,
                StartDate = @startDate,
                EndDate = @endDate,
                IsActive = @isActive,
                UpdatedAt = @updatedAt
            WHERE Id = @id
        ";

        command.Parameters.AddWithValue("@id", mealPlan.Id);
        command.Parameters.AddWithValue("@name", mealPlan.Name);
        command.Parameters.AddWithValue("@startDate", mealPlan.StartDate.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@endDate", mealPlan.EndDate.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("@isActive", mealPlan.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return command.ExecuteNonQuery() > 0;
    }

    public bool DeleteMealPlan(int id)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM MealPlans WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        return command.ExecuteNonQuery() > 0;
    }

    // ==================== MEAL PLAN DAYS ====================

    public int CreateMealPlanDay(MealPlanDay day)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO MealPlanDays (MealPlanId, DayOfWeek, Date, CreatedAt)
            VALUES (@mealPlanId, @dayOfWeek, @date, @createdAt);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@mealPlanId", day.MealPlanId);
        command.Parameters.AddWithValue("@dayOfWeek", day.DayOfWeek);
        command.Parameters.AddWithValue("@date", day.Date.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public List<MealPlanDay> GetMealPlanDays(int mealPlanId)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM MealPlanDays WHERE MealPlanId = @mealPlanId ORDER BY Date";
        command.Parameters.AddWithValue("@mealPlanId", mealPlanId);

        var days = new List<MealPlanDay>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var day = new MealPlanDay
                {
                    Id = reader.GetInt32(0),
                    MealPlanId = reader.GetInt32(1),
                    DayOfWeek = reader.GetInt32(2),
                    Date = DateTime.Parse(reader.GetString(3)),
                    CreatedAt = DateTime.Parse(reader.GetString(4))
                };
                days.Add(day);
            }
        }

        // Load entries for each day
        foreach (var day in days)
        {
            day.Entries = GetMealPlanEntries(day.Id);
        }

        return days;
    }

    public bool DeleteMealPlanDay(int id)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM MealPlanDays WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        return command.ExecuteNonQuery() > 0;
    }

    // ==================== MEAL PLAN ENTRIES ====================

    public int CreateMealPlanEntry(MealPlanEntry entry)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO MealPlanEntries (MealPlanDayId, RecipeId, MealType, [Order], CreatedAt)
            VALUES (@mealPlanDayId, @recipeId, @mealType, @order, @createdAt);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@mealPlanDayId", entry.MealPlanDayId);
        command.Parameters.AddWithValue("@recipeId", entry.RecipeId);
        command.Parameters.AddWithValue("@mealType", (int)entry.MealType);
        command.Parameters.AddWithValue("@order", entry.Order);
        command.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public List<MealPlanEntry> GetMealPlanEntries(int mealPlanDayId)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT e.*, r.*
            FROM MealPlanEntries e
            INNER JOIN Recipes r ON e.RecipeId = r.Id
            WHERE e.MealPlanDayId = @mealPlanDayId
            ORDER BY e.[Order], e.MealType
        ";
        command.Parameters.AddWithValue("@mealPlanDayId", mealPlanDayId);

        var entries = new List<MealPlanEntry>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var entry = new MealPlanEntry
                {
                    Id = reader.GetInt32(0),
                    MealPlanDayId = reader.GetInt32(1),
                    RecipeId = reader.GetInt32(2),
                    MealType = (MealType)reader.GetInt32(3),
                    Order = reader.GetInt32(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    Recipe = new Recipe
                    {
                        Id = reader.GetInt32(6),
                        Name = reader.GetString(7),
                        Description = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        Ingredients = reader.GetString(9),
                        Instructions = reader.GetString(10),
                        Calories = reader.GetInt32(11),
                        Protein = reader.GetDouble(12),
                        Carbohydrates = reader.GetDouble(13),
                        Fat = reader.GetDouble(14),
                        MealType = (MealType)reader.GetInt32(15),
                        CreatedAt = DateTime.Parse(reader.GetString(16)),
                        ImagePath = reader.IsDBNull(17) ? null : reader.GetString(17),
                        ImageUrl = reader.IsDBNull(18) ? null : reader.GetString(18)
                    }
                };
                entries.Add(entry);
            }
        }
        return entries;
    }

    public bool DeleteMealPlanEntry(int id)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM MealPlanEntries WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        return command.ExecuteNonQuery() > 0;
    }

    public bool UpdateMealPlanEntryOrder(int entryId, int newOrder)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE MealPlanEntries SET [Order] = @order WHERE Id = @id";
        command.Parameters.AddWithValue("@id", entryId);
        command.Parameters.AddWithValue("@order", newOrder);

        return command.ExecuteNonQuery() > 0;
    }

    // Shopping Lists
    public ShoppingList? GetShoppingListByMealPlan(int mealPlanId)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ShoppingLists WHERE MealPlanId = @mealPlanId";
        command.Parameters.AddWithValue("@mealPlanId", mealPlanId);

        using (var reader = command.ExecuteReader())
        {
            if (reader.Read())
            {
                return new ShoppingList
                {
                    Id = reader.GetInt32(0),
                    MealPlanId = reader.GetInt32(1),
                    ItemsJson = reader.GetString(2),
                    GeneratedAt = DateTime.Parse(reader.GetString(3))
                };
            }
        }

        return null;
    }

    public ShoppingList SaveShoppingList(int mealPlanId, string itemsJson)
    {
        var connection = GetConnection();
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // Check if shopping list already exists for this meal plan
        var existing = GetShoppingListByMealPlan(mealPlanId);

        if (existing != null)
        {
            // Update existing
            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = @"
                UPDATE ShoppingLists
                SET ItemsJson = @itemsJson, GeneratedAt = @generatedAt
                WHERE Id = @id";
            updateCommand.Parameters.AddWithValue("@id", existing.Id);
            updateCommand.Parameters.AddWithValue("@itemsJson", itemsJson);
            updateCommand.Parameters.AddWithValue("@generatedAt", now);
            updateCommand.ExecuteNonQuery();

            existing.ItemsJson = itemsJson;
            existing.GeneratedAt = DateTime.Parse(now);
            return existing;
        }
        else
        {
            // Insert new
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO ShoppingLists (MealPlanId, ItemsJson, GeneratedAt)
                VALUES (@mealPlanId, @itemsJson, @generatedAt);
                SELECT last_insert_rowid();";
            insertCommand.Parameters.AddWithValue("@mealPlanId", mealPlanId);
            insertCommand.Parameters.AddWithValue("@itemsJson", itemsJson);
            insertCommand.Parameters.AddWithValue("@generatedAt", now);

            var id = Convert.ToInt32(insertCommand.ExecuteScalar());

            return new ShoppingList
            {
                Id = id,
                MealPlanId = mealPlanId,
                ItemsJson = itemsJson,
                GeneratedAt = DateTime.Parse(now)
            };
        }
    }

    public bool DeleteShoppingList(int id)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ShoppingLists WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        return command.ExecuteNonQuery() > 0;
    }

    // ==================== PROCESSED FILES ====================

    public bool IsFileProcessed(string checksum)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ProcessedFiles WHERE FileChecksum = @checksum";
        command.Parameters.AddWithValue("@checksum", checksum);

        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public ProcessedFile? GetProcessedFile(string checksum)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM ProcessedFiles WHERE FileChecksum = @checksum";
        command.Parameters.AddWithValue("@checksum", checksum);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new ProcessedFile
            {
                Id = reader.GetInt32(0),
                FileName = reader.GetString(1),
                FileChecksum = reader.GetString(2),
                FileSizeBytes = reader.GetInt64(3),
                ProcessedAt = DateTime.Parse(reader.GetString(4)),
                RecipesExtracted = reader.GetInt32(5)
            };
        }

        return null;
    }

    public int InsertProcessedFile(ProcessedFile file)
    {
        var connection = GetConnection();

        // First, check if record with this checksum already exists
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT RecipesExtracted FROM ProcessedFiles WHERE FileChecksum = @checksum";
        checkCommand.Parameters.AddWithValue("@checksum", file.FileChecksum);
        var existingRecipesCount = checkCommand.ExecuteScalar();

        bool isUpdate = existingRecipesCount != null;

        // Delete any existing record with the same checksum (in case of re-processing)
        if (isUpdate)
        {
            var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM ProcessedFiles WHERE FileChecksum = @checksum";
            deleteCommand.Parameters.AddWithValue("@checksum", file.FileChecksum);
            deleteCommand.ExecuteNonQuery();

            Console.WriteLine($"    🔄 Nadpisano checksum (poprzednia wartość: {existingRecipesCount} przepisów, nowa: {file.RecipesExtracted} przepisów)");
        }

        // Now insert the new record
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ProcessedFiles (FileName, FileChecksum, FileSizeBytes, ProcessedAt, RecipesExtracted)
            VALUES (@fileName, @checksum, @fileSize, @processedAt, @recipesExtracted);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@fileName", file.FileName);
        command.Parameters.AddWithValue("@checksum", file.FileChecksum);
        command.Parameters.AddWithValue("@fileSize", file.FileSizeBytes);
        command.Parameters.AddWithValue("@processedAt", file.ProcessedAt.ToString("O"));
        command.Parameters.AddWithValue("@recipesExtracted", file.RecipesExtracted);

        return Convert.ToInt32(command.ExecuteScalar());
    }

    public List<string> GetProcessedFileChecksums(List<string> checksums)
    {
        if (checksums == null || checksums.Count == 0)
            return new List<string>();

        var connection = GetConnection();
        var placeholders = string.Join(",", checksums.Select((_, i) => $"@checksum{i}"));

        var command = connection.CreateCommand();
        command.CommandText = $"SELECT FileChecksum FROM ProcessedFiles WHERE FileChecksum IN ({placeholders})";

        for (int i = 0; i < checksums.Count; i++)
        {
            command.Parameters.AddWithValue($"@checksum{i}", checksums[i]);
        }

        var processedChecksums = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            processedChecksums.Add(reader.GetString(0));
        }

        return processedChecksums;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
