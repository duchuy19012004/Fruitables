using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fruitables.Models.Json;

public sealed class RolePermissionsDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["roleId", "permissions"];

    [JsonPropertyName("roleId")]
    public int RoleId { get; init; }

    [JsonPropertyName("permissions")]
    public List<RolePermissionEntry> Permissions { get; init; } = [];

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(RoleId > 0, "roleId");
        var permissions = Permissions ?? throw JsonDocumentValidation.Invalid("permissions");
        foreach (var permission in permissions)
        {
            if (permission is null)
                throw JsonDocumentValidation.Invalid("permissions", "a null child");
            permission.Validate();
        }
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        var rawPermissions = JsonDocumentValidation.RequireArray(json, "permissions");
        var permissions = Permissions ?? throw JsonDocumentValidation.Invalid("permissions");
        if (permissions.Count != rawPermissions.GetArrayLength())
            throw JsonDocumentValidation.Invalid("permissions", "an invalid child collection");

        for (var index = 0; index < rawPermissions.GetArrayLength(); index++)
        {
            if (permissions[index] is null)
                throw JsonDocumentValidation.Invalid("permissions", "a null child");
            permissions[index].Validate(rawPermissions[index]);
        }
    }
}

public sealed class RolePermissionEntry
{
    private static readonly string[] RequiredPropertyNames = ["permissionId", "permissionName", "assignedAt"];

    [JsonPropertyName("permissionId")]
    public int PermissionId { get; init; }

    [JsonPropertyName("permissionName")]
    public string PermissionName { get; init; } = string.Empty;

    [JsonPropertyName("assignedAt")]
    public DateTime AssignedAt { get; init; }

    [JsonPropertyName("assignedByAdminId")]
    public int? AssignedByAdminId { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        Require(PermissionId > 0, "permissionId");
        Require(!string.IsNullOrWhiteSpace(PermissionName), "permissionName");
        Require(AssignedAt != default, "assignedAt");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "role permission");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireNumber(json, "permissionId");
        JsonDocumentValidation.RequireString(json, "permissionName");
        JsonDocumentValidation.RequireString(json, "assignedAt");
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}

public sealed class UserRolesDocument : VersionedJsonDocument
{
    private static readonly string[] RequiredPropertyNames = ["userId", "roles"];

    [JsonPropertyName("userId")]
    public int UserId { get; init; }

    [JsonPropertyName("roles")]
    public List<UserRoleEntry> Roles { get; init; } = [];

    [JsonIgnore]
    public override IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public override void Validate()
    {
        base.Validate();
        Require(UserId > 0, "userId");
        var roles = Roles ?? throw JsonDocumentValidation.Invalid("roles");
        foreach (var role in roles)
        {
            if (role is null)
                throw JsonDocumentValidation.Invalid("roles", "a null child");
            role.Validate();
        }
    }

    internal override void Validate(JsonElement json)
    {
        base.Validate(json);
        var rawRoles = JsonDocumentValidation.RequireArray(json, "roles");
        var roles = Roles ?? throw JsonDocumentValidation.Invalid("roles");
        if (roles.Count != rawRoles.GetArrayLength())
            throw JsonDocumentValidation.Invalid("roles", "an invalid child collection");

        for (var index = 0; index < rawRoles.GetArrayLength(); index++)
        {
            if (roles[index] is null)
                throw JsonDocumentValidation.Invalid("roles", "a null child");
            roles[index].Validate(rawRoles[index]);
        }
    }
}

public sealed class UserRoleEntry
{
    private static readonly string[] RequiredPropertyNames = ["roleId", "roleName", "assignedAt"];

    [JsonPropertyName("roleId")]
    public int RoleId { get; init; }

    [JsonPropertyName("roleName")]
    public string RoleName { get; init; } = string.Empty;

    [JsonPropertyName("assignedAt")]
    public DateTime AssignedAt { get; init; }

    [JsonPropertyName("assignedByAdminId")]
    public int? AssignedByAdminId { get; init; }

    [JsonIgnore]
    public IReadOnlyCollection<string> RequiredProperties => RequiredPropertyNames;

    public void Validate()
    {
        Require(RoleId > 0, "roleId");
        Require(!string.IsNullOrWhiteSpace(RoleName), "roleName");
        Require(AssignedAt != default, "assignedAt");
    }

    internal void Validate(JsonElement json)
    {
        JsonDocumentValidation.RequireObject(json, "user role");
        JsonDocumentValidation.RequireProperties(json, RequiredProperties);
        JsonDocumentValidation.RequireNumber(json, "roleId");
        JsonDocumentValidation.RequireString(json, "roleName");
        JsonDocumentValidation.RequireString(json, "assignedAt");
        Validate();
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
