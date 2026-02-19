using MultiplayerServer.Application.Matches;

namespace MultiplayerServer.Tests;

public sealed class ClassicChessRulesEngineTests
{
    [Fact]
    public void TryApplyMove_InvalidPromotion_IsRejectedWithInvalidPromotionReason()
    {
        var engine = new ClassicChessRulesEngine();
        var match = CreateInitialMatchState();

        var outcome = engine.TryApplyMove(match, MatchSeats.Creator, "e2", "e4", "X");

        var rejected = Assert.IsType<MoveRejectedOutcome>(outcome);
        Assert.Equal(MoveRejectionReason.InvalidPromotion, rejected.Reason);
    }

    [Fact]
    public void TryApplyMove_ValidPawnAdvance_AppliesBoardTransition()
    {
        var engine = new ClassicChessRulesEngine();
        var match = CreateInitialMatchState();

        var outcome = engine.TryApplyMove(match, MatchSeats.Creator, "e2", "e4", null);

        Assert.IsType<MoveAppliedOutcome>(outcome);
        Assert.Equal('.', PieceAt(match.Board, "e2"));
        Assert.Equal('P', PieceAt(match.Board, "e4"));
    }

    [Fact]
    public void TryApplyMove_IllegalMove_RejectsWithoutBoardMutation()
    {
        var engine = new ClassicChessRulesEngine();
        var match = CreateInitialMatchState();

        var outcome = engine.TryApplyMove(match, MatchSeats.Creator, "e2", "e5", null);

        var rejected = Assert.IsType<MoveRejectedOutcome>(outcome);
        Assert.Equal(MoveRejectionReason.IllegalMove, rejected.Reason);
        Assert.Equal('P', PieceAt(match.Board, "e2"));
        Assert.Equal('.', PieceAt(match.Board, "e5"));
    }

    private static MatchState CreateInitialMatchState()
    {
        return new MatchState
        {
            MatchId = "match-1",
            JoinCode = "ABC123",
            CreatorToken = "creator",
            JoinerToken = "joiner",
            Board = BoardFromRows(
                "rnbqkbnr",
                "pppppppp",
                "........",
                "........",
                "........",
                "........",
                "PPPPPPPP",
                "RNBQKBNR"),
            SideToMove = MatchSeats.Creator,
            MoveNumber = 1,
            WhiteCanCastleKingSide = true,
            WhiteCanCastleQueenSide = true,
            BlackCanCastleKingSide = true,
            BlackCanCastleQueenSide = true,
            EnPassantTarget = null
        };
    }

    private static char[] BoardFromRows(params string[] rows)
    {
        var board = new char[64];
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                board[(row * 8) + col] = rows[row][col];
            }
        }

        return board;
    }

    private static char PieceAt(char[] board, string square)
    {
        var file = char.ToLowerInvariant(square[0]) - 'a';
        var rank = square[1] - '0';
        var row = 8 - rank;
        return board[(row * 8) + file];
    }
}
