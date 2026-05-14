using System.Security.Claims;
using System.Text.RegularExpressions;
using AuthService.Application.Common;
using AuthService.Domain.Entities;
using AuthService.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/auth/users")]
public sealed partial class UsersController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserAccessResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IUserRepository users,
        CancellationToken cancellationToken)
    {
        var response = await users.ListAsync(cancellationToken);
        return Ok(response.Select(UserAccessResponse.FromUser));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserAccessResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUserAccessRequest request,
        [FromServices] IUserRepository users,
        [FromServices] IPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Usuario nao encontrado.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await users.ExistsByEmailAsync(normalizedEmail, id, cancellationToken))
        {
            throw new InvalidOperationException("Email ja cadastrado para outro usuario.");
        }

        user.UpdateAccess(request.Name, normalizedEmail, request.Role, request.Permissions);
        if (request.AvatarUrl is not null)
        {
            ValidateAvatarUrl(request.AvatarUrl);
            user.UpdateProfile(request.Name, request.AvatarUrl);
        }

        var wantsPasswordChange = !string.IsNullOrWhiteSpace(request.NewPassword) || !string.IsNullOrWhiteSpace(request.ConfirmPassword);
        if (wantsPasswordChange)
        {
            if (request.NewPassword != request.ConfirmPassword)
            {
                throw new InvalidOperationException("A confirmacao da senha nao confere.");
            }

            ValidatePassword(request.NewPassword ?? string.Empty);
            user.ChangePassword(passwordHasher.Hash(request.NewPassword!));
        }

        await users.UpdateAsync(user, cancellationToken);

        return Ok(UserAccessResponse.FromUser(user));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] IUserRepository users,
        CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() == id)
        {
            throw new InvalidOperationException("O administrador logado nao pode excluir a propria conta.");
        }

        var user = await users.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Usuario nao encontrado.");

        await users.DeleteAsync(user, cancellationToken);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
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

public sealed record UpdateUserAccessRequest(
    string Name,
    string Email,
    string Role,
    string[] Permissions,
    string? AvatarUrl,
    string? NewPassword,
    string? ConfirmPassword);

public sealed record UserAccessResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string[] Permissions,
    string AvatarUrl,
    DateTime CreatedAt)
{
    public static UserAccessResponse FromUser(User user)
    {
        return new UserAccessResponse(user.Id, user.Name, user.Email, user.Role, user.PermissionList, user.AvatarUrl, user.CreatedAt);
    }
}
