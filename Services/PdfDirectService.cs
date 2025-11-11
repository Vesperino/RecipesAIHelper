namespace RecipesAIHelper.Services;

/// <summary>
/// Service for sending PDFs directly to OpenAI without pre-rendering
/// </summary>
public class PdfDirectService
{
    public class PdfFileChunk
    {
        public int ChunkNumber { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Base64Data { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    /// <summary>
    /// Reads PDF file and converts to Base64
    /// </summary>
    public PdfFileChunk PreparePdfForApi(string pdfPath, int chunkNumber = 1)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        var fileInfo = new FileInfo(pdfPath);
        var fileBytes = File.ReadAllBytes(pdfPath);
        var base64String = Convert.ToBase64String(fileBytes);

        Console.WriteLine($"📄 PDF: {fileInfo.Name}");
        Console.WriteLine($"   Rozmiar: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine($"   Base64 length: {base64String.Length:N0} znaków");

        return new PdfFileChunk
        {
            ChunkNumber = chunkNumber,
            FilePath = pdfPath,
            FileName = fileInfo.Name,
            Base64Data = base64String,
            FileSize = fileInfo.Length
        };
    }

    /// <summary>
    /// Validates PDF file size (OpenAI limit: 50MB per file)
    /// </summary>
    public bool ValidatePdfSize(string pdfPath, long maxSizeBytes = 50 * 1024 * 1024)
    {
        var fileInfo = new FileInfo(pdfPath);
        return fileInfo.Length <= maxSizeBytes;
    }
}
