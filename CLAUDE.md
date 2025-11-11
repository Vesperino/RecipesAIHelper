# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RecipesAIHelper is a .NET 9 ASP.NET Core application that extracts recipes from PDF files using AI services (OpenAI GPT and Google Gemini). It manages a recipe database and provides meal planning functionality through a web interface.

## Essential Commands

### Build & Run
```bash
# Restore dependencies
dotnet restore

# Run web mode (default - recommended)
dotnet run

# Run console mode
dotnet run --console

# Build only
dotnet build
```

The application runs on:
- **http://localhost:5000** (HTTP)
- https://localhost:5001 (HTTPS)

### Configuration
Create `appsettings.json` from `appsettings.example.json`:
```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-5-mini-2025-08-07"
  },
  "Settings": {
    "PdfSourceDirectory": "path/to/pdfs",
    "DatabasePath": "recipes.db"
  }
}
```

## Architecture & Design Patterns

### Multi-Provider AI Service Architecture

The application uses a **provider pattern** for AI services:

1. **IAIService Interface**: All AI providers implement this interface
   - `OpenAIService`: GPT-4o, GPT-5 models with Vision API
   - `GeminiService`: Google Gemini 2.5-flash with direct PDF support

2. **AIServiceFactory**: Creates service instances based on database configuration
   - AI providers are stored in SQLite `AIProviders` table with priority ordering
   - Factory selects active provider with highest priority
   - Supports runtime switching between providers

3. **PromptBuilder**: Centralized prompt management
   - **Critical**: Both OpenAI and Gemini services share the same prompts via `PromptBuilder`
   - When updating prompts for nutrition extraction, update `PromptBuilder.cs`, not individual services
   - Ensures consistency across all AI providers

### PDF Processing Pipeline

The app has **two PDF processing paths**:

#### Path 1: Direct PDF Upload (Gemini only)
```
PDF → Base64 → GeminiService → JSON response → Database
```
- Used by: GeminiService (via inline_data API)
- Service: `PdfDirectService` chunks PDFs and encodes to base64
- Benefit: Lower cost, faster processing

#### Path 2: PDF → Images → Vision API (OpenAI & Gemini)
```
PDF → Render at 1200 DPI → Scale to 2560px → Images → Vision API → JSON response → Database
```
- Used by: Both OpenAI and Gemini services
- Service: `PdfImageService` renders PDFs using Docnet.Core
- Quality: High DPI rendering ensures OCR accuracy
- Configuration: DPI and target height are tunable

**Important**: Controllers must check `SupportsDirectPDF()` to route requests correctly.

### Database Schema & JSON Serialization Pattern

**Critical Pattern**: Hybrid JSON storage with computed properties

```csharp
public class Recipe
{
    // Stored in DB as TEXT (JSON string)
    [JsonIgnore]
    public string? NutritionVariantsJson { get; set; }

    // Computed property - NOT stored in DB
    // Serialized to API responses
    public List<NutritionVariant>? NutritionVariants
    {
        get => JsonSerializer.Deserialize<List<NutritionVariant>>(NutritionVariantsJson);
        set => NutritionVariantsJson = JsonSerializer.Serialize(value);
    }
}
```

**Why this matters**:
- `NutritionVariantsJson` is `[JsonIgnore]` so it's NOT sent to frontend
- `NutritionVariants` getter/setter provides automatic serialization
- RecipeDbContext reads/writes the JSON column
- API responses automatically include the deserialized array

**Database Migration Pattern**: Check column existence before ALTER TABLE:
```csharp
var checkCommand = connection.CreateCommand();
checkCommand.CommandText = "PRAGMA table_info(Recipes)";
// Check if column exists, then ALTER TABLE if needed
```

### Multi-Variant Nutrition Data

**Key Feature**: Recipes can have multiple nutrition profiles (e.g., "całość", "porcja", "1/2 porcji")

1. **AI Extraction**: Prompts in `PromptBuilder` instruct AI to extract ALL rows from nutrition tables
2. **Storage**: Variants stored as JSON array in `NutritionVariantsJson` column
3. **Display**: UI shows main nutrition values + expandable variants section
4. **Optional Field**: `Servings` field captures "Liczba porcji: X" when present

**Prompt Update Protocol**:
- Nutrition extraction logic is in `PromptBuilder.cs` (lines ~265-556)
- Changes propagate to both OpenAI and Gemini automatically
- Test with both providers after prompt changes

## Important Implementation Details

### Chunking Strategy for Large PDFs

**Problem**: Large PDFs exceed AI context windows and cause data loss at page boundaries

**Solution**: Overlapping chunks
- Default: 3 pages per chunk with 1 page overlap
- Overlap ensures recipes spanning multiple pages aren't lost
- Page markers inserted: `=== STRONA {N} ===` and `--- KONIEC STRONY ---`
- Overlap markers: `=== STRONA Z POPRZEDNIEGO CHUNKA (dla kontekstu) ===`

**Configuration** (PdfProcessorService.cs):
```csharp
public PdfProcessorService(int pagesPerChunk = 3, int overlapPages = 1)
```

### Duplicate Detection System

**Multi-layer approach**:
1. **Exact match**: Case-insensitive name comparison in database
2. **Fuzzy matching**: Levenshtein distance similarity >80%
3. **AI context**: Recent recipes passed to AI to avoid re-extraction
4. **Per-file tracking**: List of processed recipe names prevents chunk duplicates

**Implementation** (RecipeDbContext.cs):
```csharp
public Recipe? FindSimilarRecipe(string name, double threshold = 0.8)
```

### Retry & Error Handling with Polly

Both AI services use Polly retry policies:
- 3 retries with exponential backoff (2s, 4s, 8s)
- Detailed console logging of failures
- Does NOT retry on `OperationCanceledException` (timeouts are fatal)

**Timeout Configuration**:
- OpenAI: 5-minute network timeout (extended from default 100s)
- Gemini: Uses default Mscc.GenerativeAI timeouts

### Controllers & API Endpoints

**ProcessingController.cs**: Main PDF processing endpoint
- POST `/api/processing/process-selected-files`
- Handles both direct PDF and image-based processing
- Routes to appropriate service based on `SupportsDirectPDF()`
- Returns real-time progress updates

**RecipesController.cs**: Recipe CRUD operations
- GET `/api/recipes` - List all recipes
- PUT `/api/recipes/{id}` - Update recipe
- DELETE `/api/recipes/{id}` - Delete recipe

**AIProvidersController.cs**: Multi-provider management
- GET `/api/aiproviders` - List providers with priority
- PUT `/api/aiproviders/{id}` - Update provider config
- POST `/api/aiproviders/{id}/toggle` - Enable/disable provider

### Frontend Architecture

**Stack**: Alpine.js + Tailwind CSS (no build step)
- `wwwroot/index.html`: Single-page app
- `wwwroot/app.js`: Alpine.js data/methods
- Recipe modal displays nutrition variants dynamically

**Key UI Pattern**: Conditional variant display
```html
<template x-if="selectedRecipe?.nutritionVariants && selectedRecipe.nutritionVariants.length > 0">
```

## Common Pitfalls

1. **Prompt Changes**: Always update `PromptBuilder.cs`, not individual service files
2. **JSON Properties**: Remember `NutritionVariantsJson` has `[JsonIgnore]`, not `NutritionVariants`
3. **PDF Provider Check**: Always check `SupportsDirectPDF()` before routing
4. **Database Migrations**: Use PRAGMA table_info pattern, don't assume columns exist
5. **Image DPI**: Default 1200 DPI is critical for OCR quality - don't lower without testing

## Polish Language Context

This application is designed for Polish recipes:
- All prompts are in Polish
- MealType enum uses Polish names (Sniadanie, Obiad, Kolacja, Deser, Napoj)
- UI text is in Polish
- Ingredient parsing expects Polish text format

## Image Generation System

### Overview
The application can generate recipe images using AI:
- **OpenAI Models**: gpt-image-1 (default), gpt-image-1-mini, dall-e-3, dall-e-2
- **Google Gemini**: imagen-4.0-ultra-generate-001

### Architecture
1. **IImageGenerationService** - interface for all image providers
2. **OpenAIImageGenerationService** - DALL-E/GPT Image implementation
3. **GeminiImageGenerationService** - Google Imagen implementation
4. **ImageGenerationServiceFactory** - selects provider based on database settings

### Configuration Storage
- **API keys and settings** are stored in SQLite `Settings` table (NOT appsettings.json)
- Settings: `ImageGenerationProvider`, `OpenAI_ApiKey`, `OpenAI_ImageModel`, `Gemini_ApiKey`, `Gemini_ImageModel`
- UI: ⚙️ Ustawienia tab provides full management interface

### Key Features

#### 1. Masked API Keys (Backend)
When updating settings via API, if the API key value is `"***"` (masked), the key is **not updated**. This allows changing models or providers without re-entering API keys.

```csharp
// In ImageSettingsController.cs
if (!string.IsNullOrEmpty(update.OpenAI.ApiKey) && update.OpenAI.ApiKey != "***")
{
    // Only update if not masked
    _db.UpsertSetting("OpenAI_ApiKey", update.OpenAI.ApiKey, ...);
}
```

#### 2. Auto-Save Models (Frontend)
The UI automatically saves model selection changes without requiring a "Save" button click:
- `@change="saveModelSettings('openAI')"` on model select in HTML
- `saveModelSettings()` function sends only model update (with apiKey: "***")
- API keys are saved separately with dedicated "Zapisz Klucze API" button
- Brief "✓ Zapisano automatycznie" indicator shows for 2 seconds after model change

### API Endpoints
- `GET /api/imagesettings` - Get settings (keys masked)
- `PUT /api/imagesettings` - Update settings
- `POST /api/imagesettings/switch-provider` - Switch between OpenAI/Gemini
- `POST /api/imagesettings/test` - Test image generation
- `POST /api/images/generate/{recipeId}` - Generate image for recipe
- `POST /api/images/generate-all-missing` - Batch generate for recipes without images

### Important Notes
- Both providers use Polish prompts for recipe images
- OpenAI uses PNG format (1024x1024)
- Google/Gemini uses JPEG format (1024x1024)
- Factory pattern automatically selects active provider from Settings
- UI displays provider cards as "OpenAI" and "Google" (models selected separately)
- **DALL-E 2 compatibility**: `OpenAIImageGenerationService` conditionally includes `quality` parameter only for dall-e-3 and newer models (dall-e-2 doesn't support it)
- See `IMAGE_GENERATION_INFO.md` for detailed usage guide
