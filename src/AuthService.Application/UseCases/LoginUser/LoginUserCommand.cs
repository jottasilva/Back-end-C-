using AuthService.Application.Common;

namespace AuthService.Application.UseCases.LoginUser;

public sealed record LoginUserCommand(string Email, string Password);

public sealed record LoginUserResponse(string AccessToken, int ExpiresIn, LoginUserDto User);

public sealed record LoginUserDto(Guid Id, string Name, string Email, string Role, string[] Permissions, string AvatarUrl);
