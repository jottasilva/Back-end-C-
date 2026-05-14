using AuthService.Application.UseCases.LoginUser;
using AuthService.Application.UseCases.RegisterUser;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserCommand command,
        [FromServices] RegisterUserHandler handler,
        CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(command, cancellationToken);
        return Created($"/api/auth/users/{response.Id}", response);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(
        [FromBody] LoginUserCommand command,
        [FromServices] LoginUserHandler handler,
        CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(command, cancellationToken);
        return Ok(response);
    }
}
