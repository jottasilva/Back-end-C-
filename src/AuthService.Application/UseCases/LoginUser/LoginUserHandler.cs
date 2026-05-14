using AuthService.Application.Common;
using AuthService.Domain.Interfaces;
using FluentValidation;

namespace AuthService.Application.UseCases.LoginUser;

public sealed class LoginUserHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IValidator<LoginUserCommand> _validator;

    public LoginUserHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IValidator<LoginUserCommand> validator)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _validator = validator;
    }

    public async Task<LoginUserResponse> HandleAsync(LoginUserCommand command, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !_passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        var token = _jwtService.CreateToken(user);
        return new LoginUserResponse(
            token.AccessToken,
            token.ExpiresIn,
            new LoginUserDto(user.Id, user.Name, user.Email, user.Role, user.PermissionList, user.AvatarUrl));
    }
}
