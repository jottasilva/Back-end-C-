using AuthService.Application.Common;
using AuthService.Domain.Entities;
using AuthService.Domain.Interfaces;
using FluentValidation;

namespace AuthService.Application.UseCases.RegisterUser;

public sealed class RegisterUserHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<RegisterUserCommand> _validator;

    public RegisterUserHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IValidator<RegisterUserCommand> validator)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _validator = validator;
    }

    public async Task<RegisterUserResponse> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        if (await _users.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Email already registered.");
        }

        var passwordHash = _passwordHasher.Hash(command.Password);
        var user = User.Create(command.Name, normalizedEmail, passwordHash);

        await _users.AddAsync(user, cancellationToken);

        return new RegisterUserResponse(user.Id, user.Name, user.Email, user.Role, user.PermissionList, user.AvatarUrl, user.CreatedAt);
    }
}
