using System.Security.Cryptography;
using System.Text;

namespace BirdieBuddy.Infrastructure;

public interface IAdminKeyValidator
{
    bool IsValid(HttpRequest request);
}

public sealed class AdminKeyValidator : IAdminKeyValidator
{
    private readonly IConfiguration _configuration;
    public AdminKeyValidator(IConfiguration configuration) => _configuration = configuration;

    public bool IsValid(HttpRequest request)
    {
        var configured = _configuration["Administration:ImportKey"];
        var supplied = request.Headers["X-Admin-Key"].ToString();
        if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrWhiteSpace(supplied)) return false;
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(configured)),
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
    }
}
