using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Services.Infrastructure.Json;

namespace Fruitables.Services.Identity.Rbac;

internal static class RbacAggregateJson
{
    public static UserRolesDocument ReadUserRoles(string json, IJsonDocumentSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            return new UserRolesDocument();
        return serializer.Deserialize<UserRolesDocument>(json);
    }

    public static RolePermissionsDocument ReadRolePermissions(string json, IJsonDocumentSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            return new RolePermissionsDocument();
        return serializer.Deserialize<RolePermissionsDocument>(json);
    }

    public static string Serialize(UserRolesDocument document, IJsonDocumentSerializer serializer) =>
        serializer.Serialize(document);

    public static string Serialize(RolePermissionsDocument document, IJsonDocumentSerializer serializer) =>
        serializer.Serialize(document);

    public static UserRolesDocument WithRoles(int userId, IEnumerable<UserRoleEntry> roles) =>
        new()
        {
            UserId = userId,
            Roles = roles.ToList()
        };

    public static RolePermissionsDocument WithPermissions(int roleId, IEnumerable<RolePermissionEntry> permissions) =>
        new()
        {
            RoleId = roleId,
            Permissions = permissions.ToList()
        };
}
