namespace MultiplayerServer.Application.Matches;

public sealed class MatchSnapshotFactory : IMatchSnapshotFactory
{
    public MatchSnapshot Create(MatchState matchState)
    {
        var boardRows = new string[8];
        for (var row = 0; row < 8; row++)
        {
            var rowChars = new char[8];
            for (var col = 0; col < 8; col++)
            {
                rowChars[col] = matchState.Board[(row * 8) + col];
            }

            boardRows[row] = new string(rowChars);
        }

        return new MatchSnapshot(
            matchState.MatchId,
            matchState.SideToMove,
            matchState.MoveNumber,
            boardRows);
    }
}
