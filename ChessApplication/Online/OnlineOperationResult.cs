using System;

namespace Chess.Online;

public sealed class OnlineOperationResult<T>
{
    private OnlineOperationResult(T? value, OnlineUserError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public OnlineUserError? Error { get; }

    public static OnlineOperationResult<T> Success(T value)
    {
        return new OnlineOperationResult<T>(value, null);
    }

    public static OnlineOperationResult<T> Failure(OnlineUserError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new OnlineOperationResult<T>(default, error);
    }
}
