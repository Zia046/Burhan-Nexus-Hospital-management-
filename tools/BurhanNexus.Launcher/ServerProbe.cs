namespace BurhanNexus.Launcher;

/// <summary>Checks the HMS server's /health endpoint (no login needed, works before setup).</summary>
internal static class ServerProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static async Task<(bool Ok, string Message)> CheckAsync(string serverUrl)
    {
        try
        {
            using var r = await Http.GetAsync(serverUrl + "/health");
            return r.IsSuccessStatusCode ? (true, "Connected") : (false, $"Server answered {(int)r.StatusCode}");
        }
        catch (TaskCanceledException) { return (false, "No answer within 5 seconds"); }
        catch (HttpRequestException ex) { return (false, ex.Message); }
    }
}
