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
        Require(Permissions is not null, "permissions");
        foreach (var permission in Permissions!)
            permission.Validate();
    }
}

public sealed class RolePermissionEntry
{
    [JsonPropertyName("permissionId")]
    public int PermissionId { get; init; }

    [JsonPropertyName("permissionName")]
    public string PermissionName { get; init; } = string.Empty;

    [JsonPropertyName("assignedAt")]
    public DateTime AssignedAt { get; init; }

    [JsonPropertyName("assignedByAdminId")]
    public int? AssignedByAdminId { get; init; }

    public void Validate()
    {
        Require(PermissionId > 0, "permissionId");
        Require(!string.IsNullOrWhiteSpace(PermissionName), "permissionName");
        Require(AssignedAt != default, "assignedAt");
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
        Require(Roles is not null, "roles");
        foreach (var role in Roles!)
            role.Validate();
    }
}

public sealed class UserRoleEntry
{
    [JsonPropertyName("roleId")]
    public int RoleId { get; init; }

    [JsonPropertyName("roleName")]
    public string RoleName { get; init; } = string.Empty;

    [JsonPropertyName("assignedAt")]
    public DateTime AssignedAt { get; init; }

    [JsonPropertyName("assignedByAdminId")]
    public int? AssignedByAdminId { get; init; }

    public void Validate()
    {
        Require(RoleId > 0, "roleId");
        Require(!string.IsNullOrWhiteSpace(RoleName), "roleName");
        Require(AssignedAt != default, "assignedAt");
    }

    private static void Require(bool condition, string propertyName)
    {
        if (!condition)
            throw new JsonException($"Required JSON property '{propertyName}' is missing or invalid.");
    }
}
