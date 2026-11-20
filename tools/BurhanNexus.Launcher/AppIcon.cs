namespace BurhanNexus.Launcher;

internal static class AppIcon
{
    private static Icon? _icon;

    public static Icon? Get()
    {
        if (_icon is not null) return _icon;
        using var s = typeof(AppIcon).Assembly.GetManifestResourceStream("app.ico");
        return _icon = s is null ? null : new Icon(s);
    }
}
