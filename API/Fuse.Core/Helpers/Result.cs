namespace Fuse.Core.Helpers;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    ServerError
}

public interface IResult
{
    bool IsSuccess { get; }
    string? Error { get; }
    ErrorType? ErrorType { get; }
    string? InnerError { get; }
}

public record Result<T> : IResult
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public ErrorType? ErrorType { get; init; }
    public string? InnerError { get; init; }
    
    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error, ErrorType errorType = Helpers.ErrorType.Validation) 
        => new() { IsSuccess = false, Error = error, ErrorType = errorType };

    public static Result<T> Failure(string error, IResult innerResult)
        => new()
        {
            IsSuccess = false,
            Error = error,
            ErrorType = innerResult.ErrorType ?? Helpers.ErrorType.ServerError,
            InnerError = BuildInnerError(innerResult)
        };

    private static string? BuildInnerError(IResult innerResult)
        => string.IsNullOrWhiteSpace(innerResult.InnerError)
            ? innerResult.Error
            : $"{innerResult.Error}. {innerResult.InnerError}";
}

public record Result : IResult
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public ErrorType? ErrorType { get; init; }
    public string? InnerError { get; init; }
    
    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(string error, ErrorType errorType = Helpers.ErrorType.Validation) 
        => new() { IsSuccess = false, Error = error, ErrorType = errorType };

    public static Result Failure(string error, IResult innerResult)
        => new()
        {
            IsSuccess = false,
            Error = error,
            ErrorType = innerResult.ErrorType ?? Helpers.ErrorType.ServerError,
            InnerError = BuildInnerError(innerResult)
        };

    private static string? BuildInnerError(IResult innerResult)
        => string.IsNullOrWhiteSpace(innerResult.InnerError)
            ? innerResult.Error
            : $"{innerResult.Error}. {innerResult.InnerError}";
}