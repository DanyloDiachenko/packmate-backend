using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;

namespace UserService.Services;

public interface IVerificationCodeService
{
    Task<string> GenerateAndSaveCodeAsync(string email, string purpose, TimeSpan? expiry = null, CancellationToken ct = default);
    Task<bool> ValidateCodeAsync(string email, string code, string purpose, CancellationToken ct = default);
}

public class VerificationCodeService : IVerificationCodeService
{
    private readonly IDistributedCache _cache;

    public VerificationCodeService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string> GenerateAndSaveCodeAsync(string email, string purpose, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var key = BuildKey(email, purpose);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10)
        };

        await _cache.SetStringAsync(key, code, options, ct);
        return code;
    }

    public async Task<bool> ValidateCodeAsync(string email, string code, string purpose, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;

        var key = BuildKey(email, purpose);
        var storedCode = await _cache.GetStringAsync(key, ct);

        if (string.IsNullOrEmpty(storedCode) || storedCode != code.Trim())
        {
            return false;
        }

        await _cache.RemoveAsync(key, ct);
        return true;
    }

    private static string BuildKey(string email, string purpose) =>
        $"auth_code:{purpose.ToLowerInvariant()}:{email.Trim().ToLowerInvariant()}";
}
