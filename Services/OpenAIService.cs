using OpenAI.Chat;
using RecipesAIHelper.Models;
using System.Text.Json;
using System.ClientModel;

namespace RecipesAIHelper.Services;

public class OpenAIService
{
    private readonly ChatClient _chatClient;

    public OpenAIService(string apiKey)
    {
        _chatClient = new ChatClient("gpt-4o", new ApiKeyCredential(apiKey));
    }

    public async Task<List<RecipeExtractionResult>> ExtractRecipesFromText(string text)
    {
        try
        {
            var systemPrompt = @"You are an expert recipe analyzer. Extract ALL recipes from the provided text.
For each recipe, provide:
- name: Recipe name
- description: Brief description
- ingredients: List of ingredients with quantities
- instructions: Step by step cooking instructions
- calories: Total calories per serving
- protein: Protein in grams
- carbohydrates: Carbohydrates in grams
- fat: Fat in grams
- mealType: One of: Breakfast, Lunch, Dinner, Dessert, Snack, Appetizer

Return ONLY valid JSON in this format:
{
  ""recipes"": [
    {
      ""name"": ""Recipe Name"",
      ""description"": ""Description"",
      ""ingredients"": [""ingredient 1"", ""ingredient 2""],
      ""instructions"": ""Step by step instructions"",
      ""calories"": 500,
      ""protein"": 25.5,
      ""carbohydrates"": 60.0,
      ""fat"": 15.5,
      ""mealType"": ""Lunch""
    }
  ]
}

If nutritional information is not explicitly stated, provide reasonable estimates based on typical ingredients and portions.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(text)
            };

            Console.WriteLine("Sending request to OpenAI...");
            var completion = await _chatClient.CompleteChatAsync(messages);

            var responseContent = completion.Value.Content[0].Text;
            Console.WriteLine($"Received response from OpenAI (length: {responseContent.Length} chars)");

            // Try to find JSON in the response
            var jsonStart = responseContent.IndexOf('{');
            var jsonEnd = responseContent.LastIndexOf('}');

            if (jsonStart == -1 || jsonEnd == -1)
            {
                Console.WriteLine("No valid JSON found in response");
                return new List<RecipeExtractionResult>();
            }

            var jsonContent = responseContent.Substring(jsonStart, jsonEnd - jsonStart + 1);

            var result = JsonSerializer.Deserialize<RecipeExtractionsResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Recipes ?? new List<RecipeExtractionResult>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling OpenAI API: {ex.Message}");
            throw;
        }
    }
}
