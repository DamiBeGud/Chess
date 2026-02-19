namespace MultiplayerServer.Application.Matches;

public interface IMatchIdGenerator
{
    string Generate();
}

public interface IJoinCodeGenerator
{
    string Generate();
}

public interface IPlayerTokenGenerator
{
    string Generate();
}
