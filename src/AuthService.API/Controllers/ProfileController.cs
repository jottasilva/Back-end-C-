using System.Security.Claims;
using System.Text.RegularExpressions;
using AuthService.Application.Common;
using AuthService.Domain.Entities;
using AuthService.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/auth/me")]
public sealed partial class ProfileController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(
        [FromServices] IUserRepository users,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(users, cancellationToken);
        return Ok(UserProfileResponse.FromUser(user));
    }

    [HttpPut]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        [FromServices] IUserRepository users,
        CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(users, cancellationToken);
        ValidateAvatarUrl(request.AvatarUrl);

        user.UpdateProfile(request.Name, request.AvatarUrl);
        await users.UpdateAsync(user, cancellationToken);

        return Ok(UserProfileResponse.FromUser(user));
    }

    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        [FromServices] IUserRepository users,
        [FromServices] IPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        ValidatePassword(request.NewPassword);

        var user = await GetCurrentUserAsync(users, cancellationToken);
        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Senha atual invalida.");
        }

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));
        await users.UpdateAsync(user, cancellationToken);

        return NoContent();
    }

    private async Task<User> GetCurrentUserAsync(IUserRepository users, CancellationToken cancellationToken)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Token invalido.");
        }

        return await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Usuario nao encontrado.");
    }

    private static void ValidateAvatarUrl(string avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return;
        }

        var match = AvatarDataUrlRegex().Match(avatarUrl);
        if (!match.Success)
        {
            throw new InvalidOperationException("A foto deve ser PNG, JPG ou WEBP enviada como upload valido.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(match.Groups["payload"].Value);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("A foto enviada esta corrompida.");
        }

        if (bytes.Length > 1_000_000)
        {
            throw new InvalidOperationException("A foto deve ter no maximo 1MB.");
        }
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 8 || !UppercaseRegex().IsMatch(password) || !NumberRegex().IsMatch(password))
        {
            throw new InvalidOperationException("A nova senha deve ter no minimo 8 caracteres, 1 maiuscula e 1 numero.");
        }
    }

    [GeneratedRegex("^data:image/(?<type>png|jpeg|webp);base64,(?<payload>[A-Za-z0-9+/=]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AvatarDataUrlRegex();

    [GeneratedRegex("[A-Z]")]
    private static partial Regex UppercaseRegex();

    [GeneratedRegex("[0-9]")]
    private static partial Regex NumberRegex();
}

public sealed record UpdateProfileRequest(string Name, string AvatarUrl);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UserProfileResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string[] Permissions,
    string AvatarUrl)
{
    public static UserProfileResponse FromUser(User user)
    {
        return new UserProfileResponse(user.Id, user.Name, user.Email, user.Role, user.PermissionList, user.AvatarUrl);
    }
}
