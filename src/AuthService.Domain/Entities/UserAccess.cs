namespace AuthService.Domain.Entities;

public static class UserAccess
{
    public const string AdminRole = "admin";
    public const string UserRole = "user";

    public static readonly string[] AllPermissions =
    [
        "dashboard",
        "reservations",
        "rooms",
        "calendar",
        "reports",
        "settings",
        "users"
    ];

    public static readonly string[] UserPermissions =
    [
        "dashboard",
        "reservations",
        "calendar"
    ];

    public static string NormalizeRole(string role)
    {
        var normalized = role.Trim().ToLowerInvariant();
        return normalized == AdminRole ? AdminRole : UserRole;
    }

    public static string[] NormalizePermissions(string role, IEnumerable<string>? permissions)
    {
        var normalizedRole = NormalizeRole(role);
        if (normalizedRole == AdminRole)
        {
            return AllPermissions;
        }

        var allowed = UserPermissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (permissions ?? UserPermissions)
            .Select(permission => permission.Trim().ToLowerInvariant())
            .Where(permission => allowed.Contains(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .DefaultIfEmpty("dashboard")
            .ToArray();
    }
}
