namespace AuthService.Application.Common;

public sealed record JwtTokenResult(string AccessToken, int ExpiresIn);
