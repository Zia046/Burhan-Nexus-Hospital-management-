using System.ComponentModel.DataAnnotations;

namespace BurhanNexus.HMS.Core.Domain;

public class User
{
    public int Id { get; set; }

    [MaxLength(150)] public string FullName { get; set; } = string.Empty;
    [MaxLength(64)] public string Username { get; set; } = string.Empty;
    /// <summary>Upper-invariant username used for unique, case-insensitive lookups.</summary>
    [MaxLength(64)] public string NormalizedUsername { get; set; } = string.Empty;
    [MaxLength(100)] public string? Designation { get; set; }
    [MaxLength(50)] public string? ContactNumber { get; set; }
    [MaxLength(200)] public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The primary administrator created by the setup wizard. Always holds every permission
    /// and cannot be deactivated, so the hospital can never lock itself out.
    /// </summary>
    public bool IsSystemAdministrator { get; set; }

    public DateTime CreatedUtc { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public uint RowVersion { get; set; }

    public UserCredential? Credential { get; set; }
    public List<UserDepartment> Departments { get; set; } = new();
    public List<UserRole> Roles { get; set; } = new();
    public List<UserPermission> Permissions { get; set; } = new();

    public static string Normalize(string username) => username.Trim().ToUpperInvariant();
}

public class UserCredential
{
    public int UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(500)] public string PasswordHash { get; set; } = string.Empty;
    public DateTime PasswordChangedUtc { get; set; }
    public bool MustChangePassword { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    /// <summary>Changes whenever credentials, status or permissions change; invalidates sessions.</summary>
    [MaxLength(64)] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
}

public class Department
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(20)] public string? Code { get; set; }
    [MaxLength(300)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }

    public List<UserDepartment> Users { get; set; } = new();
}

public class UserDepartment
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public DateTime AssignedUtc { get; set; }
    public int? AssignedByUserId { get; set; }
}

/// <summary>A permission definition (module + action). Seeded from <see cref="Security.PermissionCatalog"/>.</summary>
public class Permission
{
    public int Id { get; set; }
    [MaxLength(100)] public string Key { get; set; } = string.Empty;
    [MaxLength(50)] public string Module { get; set; } = string.Empty;
    [MaxLength(50)] public string Action { get; set; } = string.Empty;
    [MaxLength(100)] public string ModuleName { get; set; } = string.Empty;
    [MaxLength(100)] public string ActionName { get; set; } = string.Empty;
    [MaxLength(300)] public string? Description { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// A "responsibility" – a reusable, admin-defined bundle of permissions (e.g. "Receptionist").
/// Users may hold several responsibilities. Nothing about roles is hard-coded.
/// </summary>
public class Role
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(300)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public uint RowVersion { get; set; }

    public List<RolePermission> Permissions { get; set; } = new();
    public List<UserRole> Users { get; set; } = new();
}

public class RolePermission
{
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }
}

public class UserRole
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public DateTime AssignedUtc { get; set; }
    public int? AssignedByUserId { get; set; }
}

/// <summary>
/// Individual permission override for one user. IsGranted = true grants the permission,
/// IsGranted = false explicitly denies it (deny always wins over responsibilities).
/// </summary>
public class UserPermission
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }
    public bool IsGranted { get; set; }
    public DateTime AssignedUtc { get; set; }
    public int? AssignedByUserId { get; set; }
}
