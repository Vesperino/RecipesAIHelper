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
                CreatedAt TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_recipes_mealtype ON Recipes(MealType);
        ";
        command.ExecuteNonQuery();
    }

    public void InsertRecipe(Recipe recipe)
    {
        var connection = GetConnection();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Recipes (Name, Description, Ingredients, Instructions, Calories, Protein, Carbohydrates, Fat, MealType, CreatedAt)
            VALUES (@name, @description, @ingredients, @instructions, @calories, @protein, @carbs, @fat, @mealType, @createdAt)
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

        command.ExecuteNonQuery();
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
                CreatedAt = DateTime.Parse(reader.GetString(10))
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
                CreatedAt = DateTime.Parse(reader.GetString(10))
            });
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
                    CreatedAt = DateTime.Parse(reader.GetString(10))
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
                CreatedAt = DateTime.Parse(reader.GetString(10))
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

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
