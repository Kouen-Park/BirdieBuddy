namespace BirdieBuddy.Services;

public interface ICurrentUser
{
    int? Id { get; }
    bool IsAuthenticated { get; }
}
