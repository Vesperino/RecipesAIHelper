using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

public class PdfProcessorService
{
    // Configurable chunk size - domyślnie 3 strony z 1 stroną overlapu
    // Mniejsze chunki dla lepszej dokładności przy ekstrakcji przepisów
    // - Typowa strona PDF ≈ 1500 znaków ≈ 500 tokenów (polski tekst)
    // - 3 strony to ~1500 tokenów input + overlap zapewnia nieprzerwaną ekstrakcję
    // - Zostawiając dużo miejsca na szczegółowy output
    private readonly int _pagesPerChunk;
    private readonly int _overlapPages;

    public PdfProcessorService(int pagesPerChunk = 3, int overlapPages = 1)
    {
        _pagesPerChunk = pagesPerChunk;
        _overlapPages = overlapPages;
    }

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

    /// <summary>
    /// Extracts text from PDF in chunks with overlap to prevent losing recipes split across pages
    /// </summary>
    public List<PdfChunk> ExtractTextInChunks(string pdfPath)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        var chunks = new List<PdfChunk>();

        try
        {
            using var document = PdfDocument.Open(pdfPath);
            var totalPages = document.NumberOfPages;

            Console.WriteLine($"PDF has {totalPages} pages, processing in chunks of {_pagesPerChunk} with {_overlapPages} page overlap");

            // If PDF is small enough, process as single chunk
            if (totalPages <= _pagesPerChunk)
            {
                var text = string.Empty;
                foreach (Page page in document.GetPages())
                {
                    text += page.Text + "\n\n--- KONIEC STRONY ---\n\n";
                }

                chunks.Add(new PdfChunk
                {
                    ChunkNumber = 1,
                    StartPage = 1,
                    EndPage = totalPages,
                    TotalPages = totalPages,
                    Text = text,
                    HasOverlapFromPrevious = false
                });

                return chunks;
            }

            // Process in chunks with overlap
            int chunkNumber = 1;
            int currentPage = 1;

            while (currentPage <= totalPages)
            {
                var startPage = currentPage;
                var endPage = Math.Min(currentPage + _pagesPerChunk - 1, totalPages);

                // Add overlap from previous chunk (last page of previous chunk)
                var actualStartPage = startPage;
                var hasOverlap = false;

                if (chunkNumber > 1 && startPage > _overlapPages)
                {
                    actualStartPage = startPage - _overlapPages;
                    hasOverlap = true;
                }

                var chunkText = string.Empty;

                for (int pageNum = actualStartPage; pageNum <= endPage; pageNum++)
                {
                    var page = document.GetPage(pageNum);

                    if (pageNum == actualStartPage && hasOverlap)
                    {
                        chunkText += "=== STRONA Z POPRZEDNIEGO CHUNKA (dla kontekstu) ===\n\n";
                    }

                    chunkText += $"=== STRONA {pageNum} ===\n\n";
                    chunkText += page.Text + "\n\n";
                    chunkText += "--- KONIEC STRONY ---\n\n";
                }

                chunks.Add(new PdfChunk
                {
                    ChunkNumber = chunkNumber,
                    StartPage = startPage,
                    EndPage = endPage,
                    TotalPages = totalPages,
                    Text = chunkText,
                    HasOverlapFromPrevious = hasOverlap
                });

                Console.WriteLine($"  Chunk {chunkNumber}: pages {startPage}-{endPage} (overlap: {hasOverlap})");

                currentPage = endPage + 1;
                chunkNumber++;
            }

            return chunks;
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
