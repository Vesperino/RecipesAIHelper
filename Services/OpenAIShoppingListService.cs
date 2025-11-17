using System.Text;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Services;

/// <summary>
/// Service for generating shopping lists using OpenAI (GPT models)
/// </summary>
public class OpenAIShoppingListService : IShoppingListService
{
    private readonly ChatClient _chatClient;
    private readonly string _modelName;

    public OpenAIShoppingListService(string apiKey, string modelName = "gpt-5-mini-2025-08-07")
    {
        _modelName = modelName;

        // Create client with extended timeout
        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromMinutes(2)
        };
        _chatClient = new ChatClient(modelName, new ApiKeyCredential(apiKey), clientOptions);

        Console.WriteLine($"✅ OpenAIShoppingListService zainicjalizowany ({modelName})");
    }

    /// <summary>
    /// Generates a shopping list from meal plan recipes
    /// </summary>
    public async Task<ShoppingListResponse?> GenerateShoppingListAsync(List<Recipe> recipes)
    {
        try
        {
            Console.WriteLine($"🛒 Generowanie listy zakupowej z {recipes.Count} przepisów...");

            var prompt = BuildShoppingListPrompt(recipes);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("Jesteś ekspertem do tworzenia list zakupowych. Odpowiadaj TYLKO w formacie JSON, bez dodatkowego tekstu."),
                new UserChatMessage(prompt)
            };

            var chatCompletion = await _chatClient.CompleteChatAsync(messages);
            var responseText = chatCompletion.Value.Content[0].Text.Trim();

            if (string.IsNullOrEmpty(responseText))
            {
                Console.WriteLine("❌ Pusta odpowiedź od AI");
                return null;
            }

            // Remove markdown code blocks
            responseText = responseText
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            // Debug: Save response
            var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shopping_list_debug.json");
            File.WriteAllText(debugPath, responseText);
            Console.WriteLine($"🔍 DEBUG: Zapisano odpowiedź do: {debugPath}");

            var shoppingList = JsonSerializer.Deserialize<ShoppingListResponse>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (shoppingList?.Items == null || shoppingList.Items.Count == 0)
            {
                Console.WriteLine("❌ Brak elementów na liście zakupowej");
                return null;
            }

            Console.WriteLine($"✅ Wygenerowano listę zakupową: {shoppingList.Items.Count} pozycji");
            return shoppingList;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd generowania listy zakupowej: {ex.GetType().Name}");
            Console.WriteLine($"   Komunikat: {ex.Message}");
            return null;
        }
    }

    private string BuildShoppingListPrompt(List<Recipe> recipes)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("Jesteś asystentem do tworzenia list zakupowych.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**ZADANIE:**");
        promptBuilder.AppendLine("Na podstawie poniższych przepisów wygeneruj zagregowaną listę zakupów.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**ZASADY AGREGACJI:**");
        promptBuilder.AppendLine("1. **Łącz tylko identyczne składniki** - np. 'pierś z kurczaka' ≠ 'udko z kurczaka' (nie łącz!)");
        promptBuilder.AppendLine("2. **Rozpoznawaj jednostki** i sumuj je:");
        promptBuilder.AppendLine("   - gramy (g) sumuj do gramów, powyżej 1000g zamień na kilogramy (kg)");
        promptBuilder.AppendLine("   - sztuki (szt) sumuj");
        promptBuilder.AppendLine("   - łyżki/łyżeczki sumuj");
        promptBuilder.AppendLine("   - mililitry (ml) sumuj, powyżej 1000ml zamień na litry (l)");
        promptBuilder.AppendLine("3. **Jeśli nie jesteś pewien** czy składniki są identyczne - **zostaw osobno!**");
        promptBuilder.AppendLine("4. **Grupuj według kategorii** - wybierz najbardziej odpowiednią:");
        promptBuilder.AppendLine("   - **warzywa** - świeże warzywa (pomidory, ogórki, papryka itp.)");
        promptBuilder.AppendLine("   - **owoce** - świeże i suszone owoce");
        promptBuilder.AppendLine("   - **mięso i wędliny** - mięso, drób, wędliny");
        promptBuilder.AppendLine("   - **ryby** - ryby i owoce morza");
        promptBuilder.AppendLine("   - **nabiał** - mleko, sery, jogurty, masło");
        promptBuilder.AppendLine("   - **pieczywo** - chleb, bułki, pita");
        promptBuilder.AppendLine("   - **makarony i kasze** - makaron, ryż, kasza, płatki");
        promptBuilder.AppendLine("   - **spożywka** - oleje, mąki, cukier, sól, musztarda, ketchup, dodatki");
        promptBuilder.AppendLine("   - **przyprawy** - przyprawy i zioła");
        promptBuilder.AppendLine("   - **napoje** - soki, woda, napoje");
        promptBuilder.AppendLine("   - **chemia** - środki czystości, papier toaletowy, ręczniki papierowe");
        promptBuilder.AppendLine("   - **inne** - wszystko co nie pasuje do innych kategorii");
        promptBuilder.AppendLine("5. **Zaokrąglaj** ilości do praktycznych wartości (np. 125g → 125g, 1250g → 1.25kg)");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**PRZEPISY DO PRZETWORZENIA:**");
        promptBuilder.AppendLine();

        int recipeNumber = 1;
        foreach (var recipe in recipes)
        {
            promptBuilder.AppendLine($"## Przepis {recipeNumber}: {recipe.Name}");
            promptBuilder.AppendLine("**Składniki:**");
            promptBuilder.AppendLine(recipe.Ingredients);
            promptBuilder.AppendLine();
            recipeNumber++;
        }

        promptBuilder.AppendLine("**FORMAT ODPOWIEDZI:**");
        promptBuilder.AppendLine("Zwróć odpowiedź w formacie JSON:");
        promptBuilder.AppendLine(@"{
  ""items"": [
    {
      ""name"": ""nazwa składnika"",
      ""quantity"": ""ilość z jednostką"",
      ""category"": ""kategoria""
    }
  ]
}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("**PRZYKŁAD:**");
        promptBuilder.AppendLine(@"{
  ""items"": [
    {""name"": ""cebula"", ""quantity"": ""2 szt"", ""category"": ""warzywa""},
    {""name"": ""pierś z kurczaka"", ""quantity"": ""500g"", ""category"": ""mięso i wędliny""},
    {""name"": ""udko z kurczaka"", ""quantity"": ""300g"", ""category"": ""mięso i wędliny""},
    {""name"": ""mąka pszenna"", ""quantity"": ""1kg"", ""category"": ""spożywka""},
    {""name"": ""chleb"", ""quantity"": ""1 szt"", ""category"": ""pieczywo""},
    {""name"": ""olej rzepakowy"", ""quantity"": ""500ml"", ""category"": ""spożywka""},
    {""name"": ""płyn do mycia naczyń"", ""quantity"": ""1 szt"", ""category"": ""chemia""}
  ]
}");

        return promptBuilder.ToString();
    }
}
