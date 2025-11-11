using Microsoft.AspNetCore.Mvc;
using RecipesAIHelper.Data;
using RecipesAIHelper.Models;

namespace RecipesAIHelper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AIProvidersController : ControllerBase
{
    private readonly RecipeDbContext _db;

    public AIProvidersController(RecipeDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get all AI providers
    /// </summary>
    [HttpGet]
    public ActionResult<List<AIProviderDto>> GetAll()
    {
        try
        {
            var providers = _db.GetAllAIProviders();
            var dtos = providers.Select(p => new AIProviderDto
            {
                Id = p.Id,
                Name = p.Name,
                Model = p.Model,
                IsActive = p.IsActive,
                Priority = p.Priority,
                MaxPagesPerChunk = p.MaxPagesPerChunk,
                SupportsDirectPDF = p.SupportsDirectPDF,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get active AI provider
    /// </summary>
    [HttpGet("active")]
    public ActionResult<AIProviderDto> GetActive()
    {
        try
        {
            var provider = _db.GetActiveAIProvider();

            if (provider == null)
            {
                return NotFound(new { error = "No active provider configured" });
            }

            var dto = new AIProviderDto
            {
                Id = provider.Id,
                Name = provider.Name,
                Model = provider.Model,
                IsActive = provider.IsActive,
                Priority = provider.Priority,
                MaxPagesPerChunk = provider.MaxPagesPerChunk,
                SupportsDirectPDF = provider.SupportsDirectPDF,
                CreatedAt = provider.CreatedAt,
                UpdatedAt = provider.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific AI provider by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<AIProviderDto> GetById(int id)
    {
        try
        {
            var provider = _db.GetAIProvider(id);

            if (provider == null)
            {
                return NotFound(new { error = $"Provider with ID {id} not found" });
            }

            var dto = new AIProviderDto
            {
                Id = provider.Id,
                Name = provider.Name,
                Model = provider.Model,
                IsActive = provider.IsActive,
                Priority = provider.Priority,
                MaxPagesPerChunk = provider.MaxPagesPerChunk,
                SupportsDirectPDF = provider.SupportsDirectPDF,
                CreatedAt = provider.CreatedAt,
                UpdatedAt = provider.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new AI provider
    /// </summary>
    [HttpPost]
    public ActionResult<AIProviderDto> Create([FromBody] AIProviderCreateDto createDto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(createDto.Name))
            {
                return BadRequest(new { error = "Provider name is required" });
            }

            if (string.IsNullOrWhiteSpace(createDto.Model))
            {
                return BadRequest(new { error = "Model is required" });
            }

            var provider = new AIProvider
            {
                Name = createDto.Name,
                Model = createDto.Model,
                IsActive = createDto.IsActive,
                Priority = createDto.Priority,
                MaxPagesPerChunk = createDto.MaxPagesPerChunk ?? 3,
                SupportsDirectPDF = createDto.SupportsDirectPDF ?? false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var newId = _db.InsertAIProvider(provider);
            provider.Id = newId;

            // If this provider is set as active, deactivate others
            if (provider.IsActive)
            {
                _db.SetActiveAIProvider(provider.Id);
            }

            var dto = new AIProviderDto
            {
                Id = provider.Id,
                Name = provider.Name,
                Model = provider.Model,
                IsActive = provider.IsActive,
                Priority = provider.Priority,
                MaxPagesPerChunk = provider.MaxPagesPerChunk,
                SupportsDirectPDF = provider.SupportsDirectPDF,
                CreatedAt = provider.CreatedAt,
                UpdatedAt = provider.UpdatedAt
            };

            return CreatedAtAction(nameof(GetById), new { id = newId }, dto);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing AI provider
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult<AIProviderDto> Update(int id, [FromBody] AIProviderUpdateDto updateDto)
    {
        try
        {
            var provider = _db.GetAIProvider(id);

            if (provider == null)
            {
                return NotFound(new { error = $"Provider with ID {id} not found" });
            }

            // Update fields
            if (!string.IsNullOrWhiteSpace(updateDto.Name))
            {
                provider.Name = updateDto.Name;
            }

            if (!string.IsNullOrWhiteSpace(updateDto.Model))
            {
                provider.Model = updateDto.Model;
            }

            if (updateDto.Priority.HasValue)
            {
                provider.Priority = updateDto.Priority.Value;
            }

            if (updateDto.MaxPagesPerChunk.HasValue)
            {
                provider.MaxPagesPerChunk = updateDto.MaxPagesPerChunk.Value;
            }

            if (updateDto.SupportsDirectPDF.HasValue)
            {
                provider.SupportsDirectPDF = updateDto.SupportsDirectPDF.Value;
            }

            provider.UpdatedAt = DateTime.Now;

            _db.UpdateAIProvider(provider);

            // If this provider is set as active, deactivate others
            if (updateDto.IsActive.HasValue && updateDto.IsActive.Value)
            {
                _db.SetActiveAIProvider(provider.Id);
                provider.IsActive = true;
            }

            var dto = new AIProviderDto
            {
                Id = provider.Id,
                Name = provider.Name,
                Model = provider.Model,
                IsActive = provider.IsActive,
                Priority = provider.Priority,
                MaxPagesPerChunk = provider.MaxPagesPerChunk,
                SupportsDirectPDF = provider.SupportsDirectPDF,
                CreatedAt = provider.CreatedAt,
                UpdatedAt = provider.UpdatedAt
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete an AI provider
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult Delete(int id)
    {
        try
        {
            var provider = _db.GetAIProvider(id);

            if (provider == null)
            {
                return NotFound(new { error = $"Provider with ID {id} not found" });
            }

            if (provider.IsActive)
            {
                return BadRequest(new { error = "Cannot delete active provider. Activate another provider first." });
            }

            _db.DeleteAIProvider(id);

            return Ok(new { message = $"Provider '{provider.Name}' deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Activate a specific AI provider (and deactivate others)
    /// </summary>
    [HttpPut("{id}/activate")]
    public ActionResult Activate(int id)
    {
        try
        {
            var provider = _db.GetAIProvider(id);

            if (provider == null)
            {
                return NotFound(new { error = $"Provider with ID {id} not found" });
            }

            _db.SetActiveAIProvider(id);

            return Ok(new { message = $"Provider '{provider.Name}' activated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

// DTOs
public class AIProviderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public int MaxPagesPerChunk { get; set; }
    public bool SupportsDirectPDF { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AIProviderCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public int? MaxPagesPerChunk { get; set; }
    public bool? SupportsDirectPDF { get; set; }
}

public class AIProviderUpdateDto
{
    public string? Name { get; set; }
    public string? Model { get; set; }
    public bool? IsActive { get; set; }
    public int? Priority { get; set; }
    public int? MaxPagesPerChunk { get; set; }
    public bool? SupportsDirectPDF { get; set; }
}
