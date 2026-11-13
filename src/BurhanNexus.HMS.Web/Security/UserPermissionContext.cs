using BurhanNexus.HMS.Infrastructure.Security;

namespace BurhanNexus.HMS.Web.Security;

/// <summary>Effective permissions of the signed-in user, loaded once per request from the central database.</summary>
public sealed class UserPermissionContext
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IPermissionService _permissions;
    private IReadOnlySet<string>? _loaded;

    public UserPermissionContext(IHttpContextAccessor accessor, IPermissionService permissions)
    {
        _accessor = accessor;
        _permissions = permissions;
    }

    public async Task<IReadOnlySet<string>> GetAsync()
    {
        if (_loaded is not null) return _loaded;
        var userId = _accessor.HttpContext?.User.GetUserId();
        _loaded = userId is int id ? await _permissions.GetEffectivePermissionsAsync(id) : new HashSet<string>();
        return _loaded;
    }

    public async Task<bool> HasAsync(string permission) => (await GetAsync()).Contains(permission);

    public async Task<bool> HasAnyAsync(params string[] permissions)
    {
        var set = await GetAsync();
        return permissions.Any(set.Contains);
    }
}
