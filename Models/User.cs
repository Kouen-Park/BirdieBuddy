namespace BirdieBuddy.Models;

public class User
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<Round> Rounds { get; set; } = new();

    public List<Course> Courses { get; set; } = new();
}
