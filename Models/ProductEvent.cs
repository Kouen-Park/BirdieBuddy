namespace BirdieBuddy.Models;

public class ProductEvent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? RoundId { get; set; }
    public Round? Round { get; set; }
    public Guid ClientEventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? DurationMs { get; set; }
    public DateTime OccurredAt { get; set; }
}
