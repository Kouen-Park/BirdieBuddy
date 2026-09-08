namespace BirdieBuddy.Models;

// A short, user-entered record of a practice block suggested by the insights engine.
public class PracticeSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string FocusCode { get; set; } = string.Empty;
    public string DrillTitle { get; set; } = string.Empty;
    public int Minutes { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? Notes { get; set; }
}
