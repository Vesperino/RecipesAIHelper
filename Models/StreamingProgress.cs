namespace RecipesAIHelper.Models;

/// <summary>
/// Progress information for streaming AI responses
/// </summary>
public class StreamingProgress
{
    /// <summary>
    /// Number of bytes (characters) received so far
    /// </summary>
    public int BytesReceived { get; set; }

    /// <summary>
    /// Human-readable progress message
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Elapsed time in seconds
    /// </summary>
    public double ElapsedSeconds { get; set; }
}
