namespace RecipesAIHelper.Models;

public class ProcessedFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileChecksum { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime ProcessedAt { get; set; }
    public int RecipesExtracted { get; set; }
}
