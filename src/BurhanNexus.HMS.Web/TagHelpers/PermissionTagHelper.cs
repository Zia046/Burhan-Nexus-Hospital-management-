using BurhanNexus.HMS.Web.Security;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BurhanNexus.HMS.Web.TagHelpers;

/// <summary>
/// Removes an element from the HTML when the user lacks the permission:
/// &lt;a bn-permission="UserManagement.Add" ...&gt;. One of several: bn-permission="A|B".
/// The server still checks permissions independently on every request.
/// </summary>
[HtmlTargetElement(Attributes = AttributeName)]
public sealed class PermissionTagHelper : TagHelper
{
    public const string AttributeName = "bn-permission";
    private readonly UserPermissionContext _permissions;

    public PermissionTagHelper(UserPermissionContext permissions) => _permissions = permissions;

    [HtmlAttributeName(AttributeName)]
    public string Permission { get; set; } = string.Empty;

    public override int Order => int.MaxValue;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var keys = Permission.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keys.Length == 0 || !await _permissions.HasAnyAsync(keys))
            output.SuppressOutput();
    }
}
