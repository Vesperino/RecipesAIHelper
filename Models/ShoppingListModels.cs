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

/// <summary>
/// Debug log for shopping list generation - stores both prompt and response
/// </summary>
public class ShoppingListDebugLog
{
    public DateTime Timestamp { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int RecipeCount { get; set; }
    public string PromptSent { get; set; } = string.Empty;
    public string ResponseReceived { get; set; } = string.Empty;
    public int? ItemsGenerated { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
