using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly string _appsettingsPath;

    public SettingsController(IConfiguration configuration)
    {
        _configuration = configuration;
        _appsettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
    }

    [HttpGet]
    public ActionResult GetSettings()
    {
        try
        {
            var settings = new
            {
                openAI = new
                {
                    apiKey = MaskApiKey(_configuration["OpenAI:ApiKey"]),
                    model = _configuration["OpenAI:Model"] ?? "gpt-5-nano-2025-08-07"
                },
                todoist = new
                {
                    apiKey = MaskApiKey(_configuration["Todoist:ApiKey"])
                },
                settings = new
                {
                    pdfSourceDirectory = _configuration["Settings:PdfSourceDirectory"] ?? @"C:\Users\Karolina\Downloads\Dieta",
                    databasePath = _configuration["Settings:DatabasePath"] ?? "recipes.db",
                    pagesPerChunk = int.TryParse(_configuration["Settings:PagesPerChunk"], out var ppc) ? ppc : 3,
                    overlapPages = int.TryParse(_configuration["Settings:OverlapPages"], out var op) ? op : 1,
                    delayBetweenChunksMs = int.TryParse(_configuration["Settings:DelayBetweenChunksMs"], out var delay) ? delay : 3000,
                    checkDuplicates = bool.TryParse(_configuration["Settings:CheckDuplicates"], out var checkDup) ? checkDup : true,
                    recentRecipesContext = int.TryParse(_configuration["Settings:RecentRecipesContext"], out var recentCtx) ? recentCtx : 10
                }
            };

            return Ok(settings);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPut]
    public ActionResult UpdateSettings([FromBody] SettingsUpdate update)
    {
        try
        {
            if (!System.IO.File.Exists(_appsettingsPath))
            {
                return NotFound(new { error = "appsettings.json not found" });
            }

            var json = System.IO.File.ReadAllText(_appsettingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (settings == null)
            {
                return BadRequest(new { error = "Failed to parse appsettings.json" });
            }

            // Update OpenAI settings
            if (update.OpenAI != null)
            {
                var openAI = settings.ContainsKey("OpenAI")
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(settings["OpenAI"].ToString()!)
                    : new Dictionary<string, string>();

                if (openAI != null)
                {
                    if (!string.IsNullOrEmpty(update.OpenAI.ApiKey) && update.OpenAI.ApiKey != "***")
                        openAI["ApiKey"] = update.OpenAI.ApiKey;

                    if (!string.IsNullOrEmpty(update.OpenAI.Model))
                        openAI["Model"] = update.OpenAI.Model;

                    settings["OpenAI"] = openAI;
                }
            }

            // Update Todoist settings
            if (update.Todoist != null)
            {
                var todoist = settings.ContainsKey("Todoist")
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(settings["Todoist"].ToString()!)
                    : new Dictionary<string, string>();

                if (todoist != null)
                {
                    if (!string.IsNullOrEmpty(update.Todoist.ApiKey) && update.Todoist.ApiKey != "***")
                        todoist["ApiKey"] = update.Todoist.ApiKey;

                    settings["Todoist"] = todoist;
                }
            }

            // Update Settings
            if (update.Settings != null)
            {
                var appSettings = settings.ContainsKey("Settings")
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(settings["Settings"].ToString()!)
                    : new Dictionary<string, object>();

                if (appSettings != null)
                {
                    if (!string.IsNullOrEmpty(update.Settings.PdfSourceDirectory))
                        appSettings["PdfSourceDirectory"] = update.Settings.PdfSourceDirectory;

                    if (!string.IsNullOrEmpty(update.Settings.DatabasePath))
                        appSettings["DatabasePath"] = update.Settings.DatabasePath;

                    if (update.Settings.PagesPerChunk.HasValue)
                        appSettings["PagesPerChunk"] = update.Settings.PagesPerChunk.Value;

                    if (update.Settings.OverlapPages.HasValue)
                        appSettings["OverlapPages"] = update.Settings.OverlapPages.Value;

                    if (update.Settings.DelayBetweenChunksMs.HasValue)
                        appSettings["DelayBetweenChunksMs"] = update.Settings.DelayBetweenChunksMs.Value;

                    if (update.Settings.CheckDuplicates.HasValue)
                        appSettings["CheckDuplicates"] = update.Settings.CheckDuplicates.Value;

                    if (update.Settings.RecentRecipesContext.HasValue)
                        appSettings["RecentRecipesContext"] = update.Settings.RecentRecipesContext.Value;

                    settings["Settings"] = appSettings;
                }
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = JsonSerializer.Serialize(settings, options);
            System.IO.File.WriteAllText(_appsettingsPath, updatedJson);

            return Ok(new { message = "Settings updated successfully. Restart application for changes to take effect." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 8)
            return "***";

        return apiKey.Substring(0, 4) + "***" + apiKey.Substring(apiKey.Length - 4);
    }
}

public class SettingsUpdate
{
    public OpenAISettings? OpenAI { get; set; }
    public TodoistSettings? Todoist { get; set; }
    public AppSettings? Settings { get; set; }
}

public class OpenAISettings
{
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
}

public class TodoistSettings
{
    public string? ApiKey { get; set; }
}

public class AppSettings
{
    public string? PdfSourceDirectory { get; set; }
    public string? DatabasePath { get; set; }
    public int? PagesPerChunk { get; set; }
    public int? OverlapPages { get; set; }
    public int? DelayBetweenChunksMs { get; set; }
    public bool? CheckDuplicates { get; set; }
    public int? RecentRecipesContext { get; set; }
}
