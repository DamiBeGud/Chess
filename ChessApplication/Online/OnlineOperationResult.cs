using System;

namespace Chess.Online;

/// <summary>
/// OnlineOperationResult is a concrete type within the Online module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), MultiplayerServerHttpClient (Online).
/// Key collaborators are T, OnlineUserError.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSessionService (Online), NoOpOnlineMatchSessionService (Online), MultiplayerServerHttpClient (Online), IOnlineMatchHttpClient (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> T, OnlineUserError.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
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
