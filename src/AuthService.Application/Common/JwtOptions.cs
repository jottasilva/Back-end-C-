namespace AuthService.Application.Common;

public sealed class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 8;
}
