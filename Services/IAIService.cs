using RecipesAIHelper.Models;
using static RecipesAIHelper.Services.PdfImageService;
using static RecipesAIHelper.Services.PdfDirectService;

namespace RecipesAIHelper.Services;

/// <summary>
/// Interface for AI service providers (OpenAI, Gemini, etc.)
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Gets the provider name (e.g., "OpenAI", "Gemini")
    /// </summary>
    string GetProviderName();

    /// <summary>
    /// Gets the model name being used
    /// </summary>
    string GetModelName();

    /// <summary>
    /// Gets maximum number of pages that can be processed per chunk
    /// </summary>
    int GetMaxPagesPerChunk();

    /// <summary>
    /// Whether this provider supports direct PDF upload (vs image conversion)
    /// </summary>
    bool SupportsDirectPDF();

    /// <summary>
    /// Extracts recipes from PDF file directly (sends PDF as Base64)
    /// </summary>
    Task<List<RecipeExtractionResult>> ExtractRecipesFromPdf(
        PdfFileChunk pdfChunk,
        List<Recipe>? recentRecipes = null,
        IProgress<StreamingProgress>? progress = null);

    /// <summary>
    /// Extracts recipes from PDF page images using Vision API
    /// </summary>
    Task<List<RecipeExtractionResult>> ExtractRecipesFromImages(
        PdfImageChunk imageChunk,
        List<Recipe>? recentRecipes = null,
        List<string>? alreadyProcessedInPdf = null,
        IProgress<StreamingProgress>? progress = null);
}
