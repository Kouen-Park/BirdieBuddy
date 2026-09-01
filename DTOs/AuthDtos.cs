namespace BirdieBuddy.DTOs;

public record RegisterDto(string Email, string DisplayName, string Password);

public record LoginDto(string Email, string Password);

public record CurrentUserDto(int Id, string Email, string DisplayName);
