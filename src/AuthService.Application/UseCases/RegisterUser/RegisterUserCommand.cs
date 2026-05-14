namespace AuthService.Application.UseCases.RegisterUser;

public sealed record RegisterUserCommand(string Name, string Email, string Password);

public sealed record RegisterUserResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string[] Permissions,
    string AvatarUrl,
    DateTime CreatedAt);
