using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace RecipesAIHelper.Services;

public class PdfProcessorService
{
    public string ExtractTextFromPdf(string pdfPath)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        try
        {
            using var document = PdfDocument.Open(pdfPath);
            var text = string.Empty;

            foreach (Page page in document.GetPages())
            {
                text += page.Text + "\n\n";
            }

            return text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting text from PDF {pdfPath}: {ex.Message}");
            throw;
        }
    }

    public List<string> GetAllPdfFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        return Directory.GetFiles(directoryPath, "*.pdf", SearchOption.AllDirectories).ToList();
    }
}
