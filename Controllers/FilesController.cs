using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private static string? _customDirectory = null;

    public FilesController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string GetCurrentPdfDirectory()
    {
        return _customDirectory
            ?? _configuration["Settings:PdfSourceDirectory"]
            ?? @"C:\Users\Karolina\Downloads\Dieta";
    }

    [HttpGet("list")]
    public ActionResult<object> GetPdfFiles([FromQuery] string? directory = null)
    {
        try
        {
            var targetDirectory = directory ?? GetCurrentPdfDirectory();

            if (!Directory.Exists(targetDirectory))
            {
                return BadRequest(new { error = $"Folder nie istnieje: {targetDirectory}" });
            }

            var files = Directory.GetFiles(targetDirectory, "*.pdf", SearchOption.AllDirectories);
            var fileNames = files.Select(Path.GetFileName).ToList();

            return Ok(new
            {
                directory = targetDirectory,
                files = fileNames,
                count = fileNames.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("directory")]
    public ActionResult<object> GetPdfDirectory()
    {
        return Ok(new { directory = GetCurrentPdfDirectory() });
    }

    [HttpPost("directory")]
    public ActionResult<object> SetPdfDirectory([FromBody] SetDirectoryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Directory))
            {
                return BadRequest(new { error = "Ścieżka nie może być pusta" });
            }

            if (!Directory.Exists(request.Directory))
            {
                return BadRequest(new { error = $"Folder nie istnieje: {request.Directory}" });
            }

            _customDirectory = request.Directory;

            return Ok(new
            {
                message = "Folder zmieniony pomyślnie",
                directory = _customDirectory
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class SetDirectoryRequest
{
    public string Directory { get; set; } = string.Empty;
}
