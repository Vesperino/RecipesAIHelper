namespace RecipesAIHelper.Models;

public class PdfChunk
{
    public int ChunkNumber { get; set; }
    public int StartPage { get; set; }
    public int EndPage { get; set; }
    public int TotalPages { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool HasOverlapFromPrevious { get; set; }
}
