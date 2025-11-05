namespace Fuse.Core.Helpers;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    ServerError
}

public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public ErrorType? ErrorType { get; init; }
    
    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error, ErrorType errorType = Helpers.ErrorType.Validation) 
        => new() { IsSuccess = false, Error = error, ErrorType = errorType };
}

public record Result
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public ErrorType? ErrorType { get; init; }
    
    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(string error, ErrorType errorType = Helpers.ErrorType.Validation) 
        => new() { IsSuccess = false, Error = error, ErrorType = errorType };
}