using AuthService.Application.Common;
using AuthService.Application.UseCases.LoginUser;
using AuthService.Domain.Entities;
using AuthService.Domain.Interfaces;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace AuthService.UnitTests;

public sealed class LoginUserHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtService _jwtService = Substitute.For<IJwtService>();
    private readonly LoginUserHandler _handler;

    public LoginUserHandlerTests()
    {
        _handler = new LoginUserHandler(_users, _passwordHasher, _jwtService, new LoginUserValidator());
    }

    [Fact]
    public async Task HandleAsync_WhenCredentialsAreValid_ReturnsJwtAndUserData()
    {
        var user = User.Create("Usuario Teste", "teste@example.com", "stored-hash");
        _users.GetByEmailAsync("teste@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Senha123", "stored-hash").Returns(true);
        _jwtService.CreateToken(user).Returns(new JwtTokenResult("jwt-token", 28800));

        var response = await _handler.HandleAsync(new LoginUserCommand(" TESTE@example.com ", "Senha123"), CancellationToken.None);

        Assert.Equal("jwt-token", response.AccessToken);
        Assert.Equal(28800, response.ExpiresIn);
        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal("teste@example.com", response.User.Email);
        Assert.Contains("reservations", response.User.Permissions);
    }

    [Fact]
    public async Task HandleAsync_WhenEmailDoesNotExist_ThrowsGenericUnauthorized()
    {
        _users.GetByEmailAsync("missing@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.HandleAsync(new LoginUserCommand("missing@example.com", "Senha123"), CancellationToken.None));

        _jwtService.DidNotReceive().CreateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task HandleAsync_WhenPasswordDoesNotMatch_ThrowsGenericUnauthorized()
    {
        var user = User.Create("Usuario Teste", "teste@example.com", "stored-hash");
        _users.GetByEmailAsync("teste@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Senha123", "stored-hash").Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.HandleAsync(new LoginUserCommand("teste@example.com", "Senha123"), CancellationToken.None));

        _jwtService.DidNotReceive().CreateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task HandleAsync_WhenEmailIsInvalid_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.HandleAsync(new LoginUserCommand("not-an-email", "Senha123"), CancellationToken.None));
    }
}
