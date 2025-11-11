using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipesAIHelper.Services;

/// <summary>
/// Service for exporting shopping lists to Todoist
/// </summary>
public class TodoistService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiToken;
    private const string BaseUrl = "https://api.todoist.com/rest/v2";

    public TodoistService(string apiToken)
    {
        if (string.IsNullOrWhiteSpace(apiToken))
            throw new ArgumentException("Todoist API token is required", nameof(apiToken));

        _apiToken = apiToken;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
    }

    /// <summary>
    /// Export shopping list to Todoist as a project with tasks
    /// </summary>
    /// <param name="mealPlanName">Name of the meal plan</param>
    /// <param name="startDate">Start date of meal plan</param>
    /// <param name="endDate">End date of meal plan</param>
    /// <param name="items">Shopping list items</param>
    /// <returns>Project ID and URL</returns>
    public async Task<TodoistExportResult?> ExportShoppingListAsync(
        string mealPlanName,
        DateTime startDate,
        DateTime endDate,
        List<ShoppingListItem> items)
    {
        try
        {
            Console.WriteLine($"📋 Eksportowanie listy zakupowej do Todoist...");
            Console.WriteLine($"   Plan: {mealPlanName}");
            Console.WriteLine($"   Okres: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
            Console.WriteLine($"   Liczba pozycji: {items.Count}");

            // Step 1: Create project (shopping list)
            var projectName = $"🛒 {mealPlanName} ({startDate:dd.MM} - {endDate:dd.MM})";
            var project = await CreateProjectAsync(projectName);

            if (project == null)
            {
                Console.WriteLine("❌ Nie udało się utworzyć projektu w Todoist");
                return null;
            }

            Console.WriteLine($"✅ Projekt utworzony: {project.Name} (ID: {project.Id})");

            // Step 2: Group items by category
            var itemsByCategory = items
                .GroupBy(i => i.Category)
                .OrderBy(g => g.Key)
                .ToList();

            // Step 3: Create sections for each category and add tasks
            int successCount = 0;
            int totalItems = items.Count;

            foreach (var categoryGroup in itemsByCategory)
            {
                var category = categoryGroup.Key;
                Console.WriteLine($"   📦 Tworzenie sekcji: {category}");

                // Create section for this category
                var section = await CreateSectionAsync(project.Id, GetCategoryDisplayName(category));

                if (section == null)
                {
                    Console.WriteLine($"   ⚠️ Nie udało się utworzyć sekcji dla {category}, dodaję zadania bez sekcji");
                }

                // Add tasks to this section
                foreach (var item in categoryGroup)
                {
                    var taskContent = $"{item.Name} - {item.Quantity}";

                    bool success;
                    if (section != null)
                    {
                        success = await CreateTaskInSectionAsync(section.Id, taskContent);
                    }
                    else
                    {
                        success = await CreateTaskAsync(project.Id, taskContent, $"Kategoria: {category}");
                    }

                    if (success)
                    {
                        successCount++;
                    }
                }
            }

            Console.WriteLine($"✅ Eksport zakończony: {successCount}/{totalItems} pozycji dodanych");

            return new TodoistExportResult
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectUrl = project.Url,
                TasksCreated = successCount,
                TotalItems = totalItems
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd eksportu do Todoist: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Create a new project in Todoist
    /// </summary>
    private async Task<TodoistProject?> CreateProjectAsync(string name)
    {
        try
        {
            var request = new
            {
                name = name,
                color = "blue",
                is_favorite = false
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/projects", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var project = JsonSerializer.Deserialize<TodoistProject>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return project;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd tworzenia projektu Todoist: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Create a new section in a project
    /// </summary>
    private async Task<TodoistSection?> CreateSectionAsync(string projectId, string name)
    {
        try
        {
            var request = new
            {
                project_id = projectId,
                name = name
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/sections", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var section = JsonSerializer.Deserialize<TodoistSection>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return section;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd tworzenia sekcji '{name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Create a new task in a section
    /// </summary>
    private async Task<bool> CreateTaskInSectionAsync(string sectionId, string content)
    {
        try
        {
            var request = new
            {
                content = content,
                section_id = sectionId
            };

            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/tasks", httpContent);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd tworzenia zadania w sekcji '{content}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Create a new task in a project
    /// </summary>
    private async Task<bool> CreateTaskAsync(string projectId, string content, string? description = null)
    {
        try
        {
            var request = new
            {
                content = content,
                project_id = projectId,
                description = description ?? ""
            };

            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/tasks", httpContent);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd tworzenia zadania '{content}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get display name for category with emoji
    /// </summary>
    private string GetCategoryDisplayName(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "warzywa" => "🥬 Warzywa",
            "owoce" => "🍎 Owoce",
            "mięso" => "🍖 Mięso",
            "nabiał" => "🥛 Nabiał",
            "pieczywo" => "🍞 Pieczywo",
            "przyprawy" => "🧂 Przyprawy",
            "słodycze" => "🍫 Słodycze",
            "napoje" => "🥤 Napoje",
            "inne" => "📦 Inne",
            _ => $"📦 {category}"
        };
    }
}

// Response models
public class TodoistProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;
}

public class TodoistSection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class TodoistExportResult
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectUrl { get; set; } = string.Empty;
    public int TasksCreated { get; set; }
    public int TotalItems { get; set; }
}
