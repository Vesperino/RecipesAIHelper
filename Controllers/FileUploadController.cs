using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;
using System.Security.Cryptography;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileUploadController : ControllerBase
{
    private readonly RecipeDbContext _db;
    private readonly IWebHostEnvironment _env;

    public FileUploadController(RecipeDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpPost("upload")]
    public async Task<ActionResult> UploadFile(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Nie wybrano pliku" });

            // Validate file type
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".pdf" && extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            {
                return BadRequest(new { error = "Dozwolone formaty: PDF, JPG, PNG" });
            }

            // Calculate checksum
            using var stream = file.OpenReadStream();
            var checksum = await CalculateChecksumAsync(stream);
            stream.Position = 0; // Reset stream for potential reuse

            // Check if already processed
            var existingFile = _db.GetProcessedFile(checksum);
            if (existingFile != null)
            {
                return Ok(new
                {
                    alreadyProcessed = true,
                    fileName = file.FileName,
                    checksum = checksum,
                    processedFile = existingFile
                });
            }

            // Save file temporarily
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return Ok(new
            {
                alreadyProcessed = false,
                fileName = file.FileName,
                checksum = checksum,
                tempFilePath = filePath,
                uniqueFileName = uniqueFileName,
                fileSizeBytes = file.Length
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("check-files")]
    public ActionResult<object> CheckFiles([FromBody] CheckFilesRequest request)
    {
        try
        {
            if (request.Files == null || request.Files.Count == 0)
                return Ok(new { processedFiles = new Dictionary<string, ProcessedFile?>() });

            var directory = request.Directory;
            if (!Directory.Exists(directory))
                return BadRequest(new { error = $"Folder nie istnieje: {directory}" });

            var result = new Dictionary<string, ProcessedFile?>();

            foreach (var fileName in request.Files)
            {
                var filePath = Path.Combine(directory, fileName);
                if (!System.IO.File.Exists(filePath))
                {
                    result[fileName] = null;
                    continue;
                }

                var checksum = CalculateFileChecksum(filePath);
                var processedFile = _db.GetProcessedFile(checksum);
                result[fileName] = processedFile;
            }

            return Ok(new { processedFiles = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("check-uploaded")]
    public ActionResult<object> CheckUploadedFile([FromBody] CheckUploadedRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Checksum))
                return BadRequest(new { error = "Brak sumy kontrolnej" });

            var processedFile = _db.GetProcessedFile(request.Checksum);

            return Ok(new
            {
                alreadyProcessed = processedFile != null,
                processedFile = processedFile
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<string> CalculateChecksumAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes);
    }

    private string CalculateFileChecksum(string filePath)
    {
        using var stream = System.IO.File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }
}

public class CheckFilesRequest
{
    public string Directory { get; set; } = string.Empty;
    public List<string> Files { get; set; } = new();
}

public class CheckUploadedRequest
{
    public string Checksum { get; set; } = string.Empty;
}
