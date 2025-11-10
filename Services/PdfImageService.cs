using Docnet.Core;
using Docnet.Core.Models;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace RecipesAIHelper.Services;

public class PdfImageService
{
    public class PdfPageImage
    {
        public int PageNumber { get; set; }
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public string MimeType { get; set; } = "image/png";
    }

    public class PdfImageChunk
    {
        public int ChunkNumber { get; set; }
        public int StartPage { get; set; }
        public int EndPage { get; set; }
        public int TotalPages { get; set; }
        public List<PdfPageImage> Pages { get; set; } = new();
    }

    private readonly IDocLib _docLib;

    public PdfImageService()
    {
        _docLib = DocLib.Instance;
    }

    /// <summary>
    /// Renders PDF pages as PNG images in chunks
    /// </summary>
    /// <param name="pdfPath">Path to PDF file</param>
    /// <param name="pagesPerChunk">Number of pages per chunk (default 4 for Vision API)</param>
    /// <param name="dpi">DPI for rendering (default 1200 for ultra high quality OCR)</param>
    /// <param name="saveDebugImages">Save rendered images to 'zdj' folder for debugging</param>
    /// <param name="targetHeight">Optional: scale images to this height in pixels (preserves aspect ratio). If null, uses DPI setting.</param>
    /// <returns>List of chunks containing page images</returns>
    public List<PdfImageChunk> RenderPdfInChunks(string pdfPath, int pagesPerChunk = 4, int dpi = 1200, bool saveDebugImages = false, int? targetHeight = null)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        var chunks = new List<PdfImageChunk>();

        try
        {
            using var docReader = _docLib.GetDocReader(pdfPath, new PageDimensions(dpi, dpi));
            var totalPages = docReader.GetPageCount();
            var chunkCount = (int)Math.Ceiling((double)totalPages / pagesPerChunk);

            Console.WriteLine($"📄 PDF: {totalPages} stron, renderowanie w {chunkCount} chunkach po {pagesPerChunk} stron");

            for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var startPage = chunkIndex * pagesPerChunk;
                var endPage = Math.Min(startPage + pagesPerChunk - 1, totalPages - 1);

                var chunk = new PdfImageChunk
                {
                    ChunkNumber = chunkIndex + 1,
                    StartPage = startPage + 1,  // 1-indexed for display
                    EndPage = endPage + 1,
                    TotalPages = totalPages,
                    Pages = new List<PdfPageImage>()
                };

                // Render pages in this chunk
                for (int pageIndex = startPage; pageIndex <= endPage; pageIndex++)
                {
                    try
                    {
                        using var pageReader = docReader.GetPageReader(pageIndex);
                        var rawBytes = pageReader.GetImage();
                        var width = pageReader.GetPageWidth();
                        var height = pageReader.GetPageHeight();

                        // Convert raw BGRA bytes to bitmap
                        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        var bitmapData = bitmap.LockBits(
                            new Rectangle(0, 0, width, height),
                            ImageLockMode.WriteOnly,
                            bitmap.PixelFormat);

                        try
                        {
                            unsafe
                            {
                                byte* ptr = (byte*)bitmapData.Scan0;
                                int stride = bitmapData.Stride;

                                for (int y = 0; y < height; y++)
                                {
                                    for (int x = 0; x < width; x++)
                                    {
                                        int srcIndex = (y * width + x) * 4;
                                        int dstIndex = y * stride + x * 4;

                                        // BGRA to ARGB
                                        ptr[dstIndex + 0] = rawBytes[srcIndex + 0]; // B
                                        ptr[dstIndex + 1] = rawBytes[srcIndex + 1]; // G
                                        ptr[dstIndex + 2] = rawBytes[srcIndex + 2]; // R
                                        ptr[dstIndex + 3] = rawBytes[srcIndex + 3]; // A
                                    }
                                }
                            }
                        }
                        finally
                        {
                            bitmap.UnlockBits(bitmapData);
                        }

                        // Scale image if target height is specified
                        Bitmap finalBitmap = bitmap;
                        if (targetHeight.HasValue && height != targetHeight.Value)
                        {
                            var scale = (double)targetHeight.Value / height;
                            var newWidth = (int)(width * scale);
                            var newHeight = targetHeight.Value;

                            var scaledBitmap = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
                            using (var graphics = Graphics.FromImage(scaledBitmap))
                            {
                                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                graphics.DrawImage(bitmap, 0, 0, newWidth, newHeight);
                            }
                            finalBitmap = scaledBitmap;
                            Console.WriteLine($"    📐 Strona {pageIndex + 1}: {width}x{height}px → {newWidth}x{newHeight}px ({newWidth * newHeight / 1_000_000.0:F1}MP)");
                        }
                        else
                        {
                            Console.WriteLine($"    📐 Strona {pageIndex + 1}: {width}x{height} px ({width * height / 1_000_000.0:F1}MP)");
                        }

                        // Convert to RGB with white background (remove transparency)
                        // This ensures consistent appearance in Windows and for AI
                        var rgbBitmap = new Bitmap(finalBitmap.Width, finalBitmap.Height, PixelFormat.Format24bppRgb);
                        using (var graphics = Graphics.FromImage(rgbBitmap))
                        {
                            // Fill with white background
                            graphics.Clear(Color.White);
                            // Draw original image on top
                            graphics.DrawImage(finalBitmap, 0, 0);
                        }

                        // Clean up finalBitmap if it's different from original
                        if (finalBitmap != bitmap)
                        {
                            finalBitmap.Dispose();
                        }
                        finalBitmap = rgbBitmap;

                        // Increase contrast for better text readability
                        finalBitmap = IncreaseContrast(finalBitmap, 1.2f);

                        // Convert to PNG
                        using var ms = new MemoryStream();
                        finalBitmap.Save(ms, ImageFormat.Png);
                        var imageData = ms.ToArray();

                        // Clean up bitmaps
                        finalBitmap.Dispose();

                        // Save debug image if requested
                        if (saveDebugImages)
                        {
                            var debugDir = Path.Combine(Directory.GetCurrentDirectory(), "zdj");
                            Directory.CreateDirectory(debugDir);
                            var debugPath = Path.Combine(debugDir, $"page_{pageIndex + 1:D3}.png");
                            File.WriteAllBytes(debugPath, imageData);
                            Console.WriteLine($"    💾 Zapisano debug: {debugPath} ({imageData.Length / 1024.0 / 1024.0:F2} MB)");
                        }

                        chunk.Pages.Add(new PdfPageImage
                        {
                            PageNumber = pageIndex + 1,
                            ImageData = imageData,
                            MimeType = "image/png"
                        });
                    }
                    catch (Exception pageEx)
                    {
                        Console.WriteLine($"❌ Błąd renderowania strony {pageIndex + 1}: {pageEx.Message}");
                        throw;
                    }
                }

                chunks.Add(chunk);
                Console.WriteLine($"  ✅ Chunk {chunk.ChunkNumber}: strony {chunk.StartPage}-{chunk.EndPage} ({chunk.Pages.Count} obrazów)");
            }

            return chunks;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd renderowania PDF: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets total page count from PDF
    /// </summary>
    public int GetPageCount(string pdfPath)
    {
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"PDF file not found: {pdfPath}");
        }

        using var docReader = _docLib.GetDocReader(pdfPath, new PageDimensions(72, 72));
        return docReader.GetPageCount();
    }

    /// <summary>
    /// Increases image contrast for better text readability
    /// </summary>
    /// <param name="original">Original bitmap</param>
    /// <param name="contrast">Contrast factor (1.0 = no change, >1.0 = more contrast)</param>
    /// <returns>New bitmap with increased contrast</returns>
    private Bitmap IncreaseContrast(Bitmap original, float contrast)
    {
        var result = new Bitmap(original.Width, original.Height, original.PixelFormat);

        var contrastAdjustment = (contrast - 1.0f) * 255.0f;
        var factor = (259.0f * (contrastAdjustment + 255.0f)) / (255.0f * (259.0f - contrastAdjustment));

        using (var graphics = Graphics.FromImage(result))
        {
            var colorMatrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
            {
                new float[] {factor, 0, 0, 0, 0},
                new float[] {0, factor, 0, 0, 0},
                new float[] {0, 0, factor, 0, 0},
                new float[] {0, 0, 0, 1, 0},
                new float[] {(1 - factor) / 2, (1 - factor) / 2, (1 - factor) / 2, 0, 1}
            });

            var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(colorMatrix);

            graphics.DrawImage(original,
                new Rectangle(0, 0, original.Width, original.Height),
                0, 0, original.Width, original.Height,
                GraphicsUnit.Pixel, attributes);
        }

        original.Dispose();
        return result;
    }
}
