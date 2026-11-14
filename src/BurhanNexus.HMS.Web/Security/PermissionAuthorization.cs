using BurhanNexus.HMS.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BurhanNexus.HMS.Web.Security;

/// <summary>Requires a permission on a page or controller: [HasPermission(Perm.UsersView)]. Checked on the server for every request.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string Prefix = "Perm:";
    /// <summary>Requires the permission; with several permissions any one of them is enough.</summary>
    public HasPermissionAttribute(params string[] permissions) : base(Prefix + string.Join("|", permissions)) { }
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;
    /// <summary>One permission, or several separated by '|' (any of them).</summary>
    public string Permission { get; }
    public IReadOnlyList<string> AnyOf => Permission.Split('|');
}

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.Prefix, StringComparison.Ordinal))
        {
            var permission = policyName[HasPermissionAttribute.Prefix.Length..];
            foreach (var p in permission.Split('|'))
                if (!PermissionCatalog.AllKeys.Contains(p))
                    throw new InvalidOperationException($"Unknown permission '{p}'.");
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly UserPermissionContext _permissions;

    public PermissionHandler(UserPermissionContext permissions) => _permissions = permissions;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        foreach (var p in requirement.AnyOf)
            if (await _permissions.HasAsync(p))
            {
                context.Succeed(requirement);
                return;
            }
    }
}
