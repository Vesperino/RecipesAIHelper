namespace RecipesAIHelper.Models;

public class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Ingredients { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    // Nutrition information
    public int Calories { get; set; }
    public double Protein { get; set; }  // in grams
    public double Carbohydrates { get; set; }  // in grams
    public double Fat { get; set; }  // in grams

    // Meal category
    public MealType MealType { get; set; }

    public DateTime CreatedAt { get; set; }
}

public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Dessert,
    Snack,
    Appetizer
}
