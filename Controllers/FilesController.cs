using Microsoft.AspNetCore.Mvc;
using RecipesAIHelper.Services;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly PdfProcessorService _pdfProcessor;
    private readonly string _pdfDirectory;

    public FilesController(PdfProcessorService pdfProcessor, IConfiguration configuration)
    {
        _pdfProcessor = pdfProcessor;
        _pdfDirectory = configuration["Settings:PdfSourceDirectory"] ?? @"C:\Users\Karolina\Downloads\Dieta";
    }

    [HttpGet("list")]
    public ActionResult<List<string>> GetPdfFiles()
    {
        try
        {
            var files = _pdfProcessor.GetAllPdfFiles(_pdfDirectory);
            var fileNames = files.Select(Path.GetFileName).ToList();
            return Ok(fileNames);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("directory")]
    public ActionResult<string> GetPdfDirectory()
    {
        return Ok(new { directory = _pdfDirectory });
    }
}
