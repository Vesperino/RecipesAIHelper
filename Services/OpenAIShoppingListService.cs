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
        var debugLog = new ShoppingListDebugLog
        {
            Timestamp = DateTime.Now,
            Provider = "OpenAI",
            ModelName = _modelName,
            RecipeCount = recipes.Count
        };

        try
        {
            Console.WriteLine($"🛒 Generowanie listy zakupowej z {recipes.Count} przepisów...");

            var systemMessage = "Jesteś asystentem do tworzenia list zakupowych. Odpowiadaj TYLKO w formacie JSON, bez dodatkowego tekstu.";
            var userPrompt = BuildShoppingListPrompt(recipes);

            // Save full prompt with system message
            debugLog.PromptSent = $"[SYSTEM MESSAGE]\n{systemMessage}\n\n[USER PROMPT]\n{userPrompt}";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemMessage),
                new UserChatMessage(userPrompt)
            };

            var chatCompletion = await _chatClient.CompleteChatAsync(messages);
            var responseText = chatCompletion.Value.Content[0].Text.Trim();
            debugLog.ResponseReceived = responseText;

            if (string.IsNullOrEmpty(responseText))
            {
                Console.WriteLine("❌ Pusta odpowiedź od AI");
                debugLog.Success = false;
                debugLog.ErrorMessage = "Pusta odpowiedź od AI";
                SaveDebugLog(debugLog);
                return null;
            }

            // Remove markdown code blocks
            responseText = responseText
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            // Fallback: if AI returned array instead of object with "items", wrap it
            if (responseText.StartsWith("["))
            {
                Console.WriteLine("⚠️ AI zwróciło tablicę zamiast obiektu - naprawiam automatycznie");
                responseText = $"{{\"items\": {responseText}}}";
            }

            var shoppingList = JsonSerializer.Deserialize<ShoppingListResponse>(responseText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (shoppingList?.Items == null || shoppingList.Items.Count == 0)
            {
                Console.WriteLine("❌ Brak elementów na liście zakupowej");
                debugLog.Success = false;
                debugLog.ErrorMessage = "Brak elementów na liście zakupowej";
                SaveDebugLog(debugLog);
                return null;
            }

            debugLog.Success = true;
            debugLog.ItemsGenerated = shoppingList.Items.Count;
            SaveDebugLog(debugLog);

            Console.WriteLine($"✅ Wygenerowano listę zakupową: {shoppingList.Items.Count} pozycji");
            return shoppingList;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd generowania listy zakupowej: {ex.GetType().Name}");
            Console.WriteLine($"   Komunikat: {ex.Message}");
            debugLog.Success = false;
            debugLog.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            SaveDebugLog(debugLog);
            return null;
        }
    }

    /// <summary>
    /// Generates shopping list using day-by-day chunking approach
    /// </summary>
    public async Task<ShoppingListResponse?> GenerateShoppingListChunked(Dictionary<int, List<Recipe>> recipesByDay)
    {
        try
        {
            Console.WriteLine($"🛒 Generowanie listy zakupowej (chunking): {recipesByDay.Count} dni");

            var dailyLists = new List<DailyShoppingList>();

            // FAZA A: Generuj listę dla każdego dnia
            foreach (var (dayNumber, recipes) in recipesByDay.OrderBy(x => x.Key))
            {
                Console.WriteLine($"📅 Dzień {dayNumber}: {recipes.Count} przepisów");

                var dayDebugLog = new ShoppingListDebugLog
                {
                    Timestamp = DateTime.Now,
                    Provider = "OpenAI",
                    ModelName = _modelName,
                    RecipeCount = recipes.Count,
                    Phase = $"Day {dayNumber}"
                };

                try
                {
                    var systemMessage = "Jesteś asystentem do tworzenia list zakupowych. Odpowiadaj TYLKO w formacie JSON, bez dodatkowego tekstu.";
                    var userPrompt = PromptBuilder.BuildDailyShoppingListPrompt(recipes, dayNumber);

                    dayDebugLog.PromptSent = $"[SYSTEM MESSAGE]\n{systemMessage}\n\n[USER PROMPT]\n{userPrompt}";

                    var messages = new List<ChatMessage>
                    {
                        new SystemChatMessage(systemMessage),
                        new UserChatMessage(userPrompt)
                    };

                    var chatCompletion = await _chatClient.CompleteChatAsync(messages);
                    var responseText = chatCompletion.Value.Content[0].Text.Trim();
                    dayDebugLog.ResponseReceived = responseText;

                    // Remove markdown code blocks
                    responseText = responseText
                        .Replace("```json", "")
                        .Replace("```", "")
                        .Trim();

                    // Fallback: if AI returned array instead of object with "items", wrap it
                    if (responseText.StartsWith("["))
                    {
                        Console.WriteLine($"⚠️ Dzień {dayNumber}: AI zwróciło tablicę zamiast obiektu - naprawiam automatycznie");
                        responseText = $"{{\"items\": {responseText}}}";
                    }

                    var dayList = JsonSerializer.Deserialize<ShoppingListResponse>(responseText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (dayList?.Items == null || dayList.Items.Count == 0)
                    {
                        Console.WriteLine($"❌ Dzień {dayNumber}: Brak elementów na liście");
                        dayDebugLog.Success = false;
                        dayDebugLog.ErrorMessage = "Brak elementów na liście";
                        SaveDebugLog(dayDebugLog, $"shopping_list_debug_day{dayNumber}.json");
                        continue;
                    }

                    dayDebugLog.Success = true;
                    dayDebugLog.ItemsGenerated = dayList.Items.Count;
                    SaveDebugLog(dayDebugLog, $"shopping_list_debug_day{dayNumber}.json");

                    dailyLists.Add(new DailyShoppingList
                    {
                        Day = dayNumber,
                        Items = dayList.Items
                    });

                    Console.WriteLine($"   ✅ {dayList.Items.Count} pozycji");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Dzień {dayNumber}: Błąd - {ex.Message}");
                    dayDebugLog.Success = false;
                    dayDebugLog.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                    SaveDebugLog(dayDebugLog, $"shopping_list_debug_day{dayNumber}.json");
                }
            }

            if (dailyLists.Count == 0)
            {
                Console.WriteLine("❌ Nie wygenerowano żadnych list dziennych");
                return null;
            }

            // FAZA B: Merge wszystkich list dziennych
            Console.WriteLine($"🔀 Łączenie {dailyLists.Count} list dziennych...");

            var mergeDebugLog = new ShoppingListDebugLog
            {
                Timestamp = DateTime.Now,
                Provider = "OpenAI",
                ModelName = _modelName,
                RecipeCount = dailyLists.Sum(d => d.Items.Count),
                Phase = "Merge"
            };

            try
            {
                var systemMessage = "Jesteś asystentem do tworzenia list zakupowych. Odpowiadaj TYLKO w formacie JSON, bez dodatkowego tekstu.";
                var userPrompt = PromptBuilder.BuildMergeShoppingListsPrompt(dailyLists);

                mergeDebugLog.PromptSent = $"[SYSTEM MESSAGE]\n{systemMessage}\n\n[USER PROMPT]\n{userPrompt}";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemMessage),
                    new UserChatMessage(userPrompt)
                };

                var chatCompletion = await _chatClient.CompleteChatAsync(messages);
                var responseText = chatCompletion.Value.Content[0].Text.Trim();
                mergeDebugLog.ResponseReceived = responseText;

                // Remove markdown code blocks
                responseText = responseText
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                // Fallback: if AI returned array instead of object with "items", wrap it
                if (responseText.StartsWith("["))
                {
                    Console.WriteLine("⚠️ Merge: AI zwróciło tablicę zamiast obiektu - naprawiam automatycznie");
                    responseText = $"{{\"items\": {responseText}}}";
                }

                var finalList = JsonSerializer.Deserialize<ShoppingListResponse>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (finalList?.Items == null || finalList.Items.Count == 0)
                {
                    Console.WriteLine("❌ Merge: Brak elementów na finalnej liście");
                    mergeDebugLog.Success = false;
                    mergeDebugLog.ErrorMessage = "Brak elementów na finalnej liście";
                    SaveDebugLog(mergeDebugLog, "shopping_list_debug_merge.json");
                    return null;
                }

                mergeDebugLog.Success = true;
                mergeDebugLog.ItemsGenerated = finalList.Items.Count;
                SaveDebugLog(mergeDebugLog, "shopping_list_debug_merge.json");

                Console.WriteLine($"✅ Finalna lista: {finalList.Items.Count} pozycji");
                return finalList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Błąd podczas merge: {ex.Message}");
                mergeDebugLog.Success = false;
                mergeDebugLog.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
                SaveDebugLog(mergeDebugLog, "shopping_list_debug_merge.json");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Błąd generowania listy zakupowej (chunking): {ex.Message}");
            return null;
        }
    }

    private void SaveDebugLog(ShoppingListDebugLog log, string filename = "shopping_list_debug.json")
    {
        try
        {
            var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            var json = JsonSerializer.Serialize(log, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(debugPath, json);
            Console.WriteLine($"🔍 DEBUG: Zapisano log do: {debugPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Nie udało się zapisać debug logu: {ex.Message}");
        }
    }

    private string BuildShoppingListPrompt(List<Recipe> recipes)
    {
        var promptBuilder = new StringBuilder();

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
