namespace RecipesAIHelper.Models;

/// <summary>
/// Response model for shopping list generation from AI
/// </summary>
public class ShoppingListResponse
{
    public List<ShoppingListItem> Items { get; set; } = new();
}

/// <summary>
/// Single item in a shopping list
/// </summary>
public class ShoppingListItem
{
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
