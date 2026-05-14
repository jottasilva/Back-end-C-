using AuthService.Application.Common;
using AuthService.Application.UseCases.RegisterUser;
using AuthService.Domain.Entities;
using AuthService.Domain.Interfaces;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace AuthService.UnitTests;

public sealed class RegisterUserHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests()
    {
        _handler = new RegisterUserHandler(_users, _passwordHasher, new RegisterUserValidator());
    }

    [Fact]
    public async Task HandleAsync_WhenCommandIsValid_CreatesUserWithNormalizedEmailAndHashedPassword()
    {
        User? savedUser = null;
        _users.ExistsByEmailAsync("teste@example.com", Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash("Senha123").Returns("hashed-password");
        _users
            .AddAsync(Arg.Do<User>(user => savedUser = user), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleAsync(
            new RegisterUserCommand("Usuario Teste", "  Teste@Example.COM ", "Senha123"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("Usuario Teste", response.Name);
        Assert.Equal("teste@example.com", response.Email);
        Assert.Equal(UserAccess.UserRole, response.Role);
        Assert.Contains("reservations", response.Permissions);
        Assert.NotNull(savedUser);
        Assert.Equal("teste@example.com", savedUser.Email);
        Assert.Equal("hashed-password", savedUser.PasswordHash);
        await _users.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenEmailAlreadyExists_ThrowsConflictException()
    {
        _users.ExistsByEmailAsync("teste@example.com", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleAsync(new RegisterUserCommand("Usuario Teste", "teste@example.com", "Senha123"), CancellationToken.None));

        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenPasswordIsWeak_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(new RegisterUserCommand("Usuario Teste", "teste@example.com", "senha"), CancellationToken.None));

        _passwordHasher.DidNotReceive().Hash(Arg.Any<string>());
    }
}
