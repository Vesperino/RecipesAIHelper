namespace RecipesAIHelper.Models;

/// <summary>
/// Represents an AI provider configuration for recipe extraction.
/// Note: API keys are stored in Settings table (OpenAI_ApiKey, Gemini_ApiKey)
/// and shared with image generation services.
/// </summary>
public class AIProvider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public int MaxPagesPerChunk { get; set; } = 3;
    public bool SupportsDirectPDF { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
