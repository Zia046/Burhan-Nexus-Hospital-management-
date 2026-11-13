namespace BurhanNexus.HMS.Core.Abstractions;

/// <summary>Information about the person and computer performing the current operation.</summary>
public interface IRequestContext
{
    int? UserId { get; }
    string? Username { get; }
    string? IpAddress { get; }
    string? Workstation { get; }
    string? UserAgent { get; }
}

public sealed class SystemRequestContext : IRequestContext
{
    public int? UserId => null;
    public string? Username => "SYSTEM";
    public string? IpAddress => null;
    public string? Workstation => Environment.MachineName;
    public string? UserAgent => null;
}
