namespace BurhanNexus.HMS.Core.Abstractions;

public class ServiceResult
{
    public bool Succeeded { get; init; }
    public List<string> Errors { get; init; } = new();

    public static ServiceResult Ok() => new() { Succeeded = true };
    public static ServiceResult Fail(params string[] errors) => new() { Succeeded = false, Errors = errors.ToList() };

    public string ErrorText => string.Join(" ", Errors);
}

public sealed class ServiceResult<T> : ServiceResult
{
    public T? Value { get; init; }
    public static ServiceResult<T> Ok(T value) => new() { Succeeded = true, Value = value };
    public new static ServiceResult<T> Fail(params string[] errors) => new() { Succeeded = false, Errors = errors.ToList() };
}
