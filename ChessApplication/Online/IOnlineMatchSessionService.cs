namespace Chess.Online;

public interface IOnlineMatchSessionService :
    IOnlineMatchSessionReadModel,
    IOnlineMatchSessionCommands,
    IOnlineMatchSessionLifecycle
{
}
