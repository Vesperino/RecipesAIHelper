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
                ApiKey TEXT NOT NULL,
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
        ";
        command.ExecuteNonQuery();

        // Migrate existing data from Recipes table if ImagePath column doesn't exist
        MigrateRecipesTable();

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
                ApiKey = reader.GetString(2),
                Model = reader.GetString(3),
                IsActive = reader.GetInt32(4) == 1,
                Priority = reader.GetInt32(5),
                MaxPagesPerChunk = reader.GetInt32(6),
                SupportsDirectPDF = reader.GetInt32(7) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(8)),
                UpdatedAt = DateTime.Parse(reader.GetString(9))
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
                ApiKey = reader.GetString(2),
                Model = reader.GetString(3),
                IsActive = reader.GetInt32(4) == 1,
                Priority = reader.GetInt32(5),
                MaxPagesPerChunk = reader.GetInt32(6),
                SupportsDirectPDF = reader.GetInt32(7) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(8)),
                UpdatedAt = DateTime.Parse(reader.GetString(9))
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
                ApiKey = reader.GetString(2),
                Model = reader.GetString(3),
                IsActive = reader.GetInt32(4) == 1,
                Priority = reader.GetInt32(5),
                MaxPagesPerChunk = reader.GetInt32(6),
                SupportsDirectPDF = reader.GetInt32(7) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(8)),
                UpdatedAt = DateTime.Parse(reader.GetString(9))
            };
        }

        return null;
    }

    public int InsertAIProvider(AIProvider provider)
    {
        var connection = GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO AIProviders (Name, ApiKey, Model, IsActive, Priority, MaxPagesPerChunk, SupportsDirectPDF, CreatedAt, UpdatedAt)
            VALUES (@name, @apiKey, @model, @isActive, @priority, @maxPages, @supportsPdf, @createdAt, @updatedAt);
            SELECT last_insert_rowid();
        ";

        command.Parameters.AddWithValue("@name", provider.Name);
        command.Parameters.AddWithValue("@apiKey", provider.ApiKey);
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
                ApiKey = @apiKey,
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
        command.Parameters.AddWithValue("@apiKey", provider.ApiKey);
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

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
