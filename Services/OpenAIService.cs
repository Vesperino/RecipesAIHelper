using OpenAI.Chat;
using RecipesAIHelper.Models;
using System.ClientModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RecipesAIHelper.Services;

public class OpenAIService
{
    private readonly ChatClient _chatClient;
    private readonly IDeserializer _yamlDeserializer;
    private readonly string _modelName;

    public OpenAIService(string apiKey, string modelName = "gpt-5-nano-2025-08-07")
    {
        _modelName = modelName;
        _chatClient = new ChatClient(modelName, new ApiKeyCredential(apiKey));
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        Console.WriteLine($"Using OpenAI model: {_modelName}");
    }

    public async Task<List<RecipeExtractionResult>> ExtractRecipesFromChunk(PdfChunk chunk, List<Recipe>? recentRecipes = null)
    {
        try
        {
            var recentRecipesContext = "";
            if (recentRecipes != null && recentRecipes.Count > 0)
            {
                recentRecipesContext = "\n\nOSTATNIO DODANE PRZEPISY DO BAZY (NIE ekstraktuj ich ponownie!):\n";
                foreach (var recipe in recentRecipes)
                {
                    recentRecipesContext += $"- {recipe.Name} ({recipe.MealType})\n";
                }
            }

            var systemPrompt = $@"Jesteś ekspertem w analizie przepisów kulinarnych.

WAŻNE ZASADY:
1. Wyciągnij WSZYSTKIE kompletne przepisy z dostarczonego tekstu
2. Jeśli widzisz oznaczenie '=== STRONA Z POPRZEDNIEGO CHUNKA ===' - użyj jej TYLKO jako kontekstu do dokończenia przepisu ze strony następnej, NIE ekstraktuj tych samych przepisów ponownie
3. Każdy przepis MUSI zawierać: nazwę, składniki (z ilościami), sposób wykonania, wartości odżywcze (kalorie, białko, węglowodany, tłuszcze)
4. Jeśli przepis jest rozłożony na dwie strony - połącz wszystkie informacje w jeden kompletny przepis
5. Jeśli wartości odżywcze nie są podane wprost - oszacuj je na podstawie składników
6. Kategorie: Sniadanie, Obiad, Kolacja, Deser, Napoj
7. UNIKAJ DUPLIKATÓW - jeśli przepis już został dodany do bazy (lista poniżej), pomiń go{recentRecipesContext}

ZWRÓĆ TYLKO YAML w tym formacie (bez żadnych dodatkowych komentarzy, bez markdown code blocks):

przepisy:
  - nazwa: Nazwa przepisu
    opis: Krótki opis
    kategoria: Sniadanie
    skladniki:
      - 200g mąki
      - 2 jajka
      - 250ml mleka
    wykonanie: |
      Krok po kroku instrukcje przygotowania.
      Można w wielu liniach.
    kalorie: 450
    bialko: 25.5
    weglowodany: 60.0
    tluszcze: 15.5
  - nazwa: Kolejny przepis
    opis: Opis
    kategoria: Obiad
    skladniki:
      - składnik 1
      - składnik 2
    wykonanie: Instrukcje
    kalorie: 600
    bialko: 35.0
    weglowodany: 70.0
    tluszcze: 20.0

PAMIĘTAJ:
- Zwróć TYLKO YAML, nic więcej
- NIE używaj markdown code blocks (```yaml)
- Kategoria MUSI być jedną z: Sniadanie, Obiad, Kolacja, Deser, Napoj
- Wartości odżywcze jako liczby (bez jednostek w wartości)
- Składniki jako lista z ilościami";

            var userMessage = chunk.HasOverlapFromPrevious
                ? $@"To jest chunk {chunk.ChunkNumber} z {chunk.TotalPages} stron PDF.
Zawiera overlap z poprzedniej strony - użyj go tylko jako kontekstu do dokończenia przepisów, które mogły się zacząć na poprzedniej stronie.
NIE zwracaj przepisów, które już były w poprzednim chunku.

Strony {chunk.StartPage}-{chunk.EndPage}:

{chunk.Text}"
                : $@"To jest chunk {chunk.ChunkNumber} ze stron {chunk.StartPage}-{chunk.EndPage} (z {chunk.TotalPages} stron PDF).

{chunk.Text}";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            Console.WriteLine($"Sending chunk {chunk.ChunkNumber} to OpenAI (pages {chunk.StartPage}-{chunk.EndPage})...");
            var completion = await _chatClient.CompleteChatAsync(messages);

            var responseContent = completion.Value.Content[0].Text.Trim();
            Console.WriteLine($"Received response (length: {responseContent.Length} chars)");

            // Remove markdown code blocks if present
            responseContent = responseContent
                .Replace("```yaml", "")
                .Replace("```yml", "")
                .Replace("```", "")
                .Trim();

            // Parse YAML
            var yamlResponse = _yamlDeserializer.Deserialize<YamlRecipeResponse>(responseContent);

            if (yamlResponse?.Przepisy == null || yamlResponse.Przepisy.Count == 0)
            {
                Console.WriteLine("Warning: No recipes extracted from this chunk");
                return new List<RecipeExtractionResult>();
            }

            Console.WriteLine($"Extracted {yamlResponse.Przepisy.Count} recipes from chunk {chunk.ChunkNumber}");

            // Convert to RecipeExtractionResult
            return yamlResponse.Przepisy.Select(r => new RecipeExtractionResult
            {
                Name = r.Nazwa ?? "Unknown",
                Description = r.Opis ?? "",
                Ingredients = r.Skladniki ?? new List<string>(),
                Instructions = r.Wykonanie ?? "",
                Calories = r.Kalorie,
                Protein = r.Bialko,
                Carbohydrates = r.Weglowodany,
                Fat = r.Tluszcze,
                MealType = r.Kategoria ?? "Obiad"
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing chunk {chunk.ChunkNumber}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            return new List<RecipeExtractionResult>();
        }
    }
}

// Helper class for YAML deserialization
public class YamlRecipeResponse
{
    public List<YamlRecipe> Przepisy { get; set; } = new();
}

public class YamlRecipe
{
    public string? Nazwa { get; set; }
    public string? Opis { get; set; }
    public string? Kategoria { get; set; }
    public List<string>? Skladniki { get; set; }
    public string? Wykonanie { get; set; }
    public int Kalorie { get; set; }
    public double Bialko { get; set; }
    public double Weglowodany { get; set; }
    public double Tluszcze { get; set; }
}
