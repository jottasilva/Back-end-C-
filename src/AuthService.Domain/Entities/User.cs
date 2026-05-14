namespace AuthService.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Role { get; private set; } = UserAccess.UserRole;
    public string Permissions { get; private set; } = string.Join(',', UserAccess.UserPermissions);
    public string AvatarUrl { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private User()
    {
    }

    public string[] PermissionList => Permissions
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static User Create(
        string name,
        string email,
        string passwordHash,
        string role = UserAccess.UserRole,
        IEnumerable<string>? permissions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var normalizedRole = UserAccess.NormalizeRole(role);
        var normalizedPermissions = UserAccess.NormalizePermissions(normalizedRole, permissions);

        return new User
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = normalizedRole,
            Permissions = string.Join(',', normalizedPermissions),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateAccess(string name, string email, string role, IEnumerable<string> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedRole = UserAccess.NormalizeRole(role);
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        Role = normalizedRole;
        Permissions = string.Join(',', UserAccess.NormalizePermissions(normalizedRole, permissions));
    }

    public void UpdateProfile(string name, string avatarUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        AvatarUrl = avatarUrl.Trim();
    }

    public void ChangePassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }
}
