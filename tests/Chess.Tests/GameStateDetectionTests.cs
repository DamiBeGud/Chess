using System.Linq;
using Chess.Domain;
using Chess.Engine;
using Xunit;

namespace Chess.Tests;

public sealed class GameStateDetectionTests
{
    private readonly ChessGameEngine _engine = new();

    [Fact]
    public void IsKingInCheck_IdentifiesCheckInRepresentativePosition()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            0,
            1,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.Black, 4, 7),
            Placement(PieceType.King, PieceColor.Black, 0, 7));

        Assert.True(_engine.IsKingInCheck(state, PieceColor.White));
        Assert.False(_engine.IsKingInCheck(state, PieceColor.Black));
    }

    [Fact]
    public void TryApplyMove_Checkmate_SetsWinningStatus()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            0,
            1,
            Placement(PieceType.King, PieceColor.White, 5, 5),
            Placement(PieceType.Queen, PieceColor.White, 6, 5),
            Placement(PieceType.King, PieceColor.Black, 7, 7));

        var moveApplied = _engine.TryApplyMove(state, new Square(6, 5), new Square(6, 6), out var updatedState);
        Assert.True(moveApplied);
        Assert.Equal(GameStatus.WhiteWin, updatedState.Status);
        Assert.True(_engine.IsKingInCheck(updatedState, PieceColor.Black));
        Assert.Empty(_engine.GenerateLegalMoves(updatedState));
    }

    [Fact]
    public void TryApplyMove_Stalemate_SetsDrawStatus()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            0,
            1,
            Placement(PieceType.King, PieceColor.White, 2, 5),
            Placement(PieceType.Queen, PieceColor.White, 1, 5),
            Placement(PieceType.King, PieceColor.Black, 0, 7));

        var moveApplied = _engine.TryApplyMove(state, new Square(1, 5), new Square(2, 6), out var updatedState);
        Assert.True(moveApplied);
        Assert.Equal(GameStatus.Draw, updatedState.Status);
        Assert.False(_engine.IsKingInCheck(updatedState, PieceColor.Black));
        Assert.Empty(_engine.GenerateLegalMoves(updatedState));
    }

    [Fact]
    public void TryApplyMove_FiftyMoveRule_SetsDrawStatus()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            99,
            20,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Knight, PieceColor.White, 1, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7));

        var moveApplied = _engine.TryApplyMove(state, new Square(1, 0), new Square(2, 2), out var updatedState);
        Assert.True(moveApplied);
        Assert.Equal(100, updatedState.HalfmoveClock);
        Assert.Equal(GameStatus.Draw, updatedState.Status);
    }

    [Fact]
    public void TryApplyMove_ThreefoldRepetition_SetsDrawStatus()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            0,
            1,
            Placement(PieceType.King, PieceColor.White, 0, 0),
            Placement(PieceType.Knight, PieceColor.White, 1, 0),
            Placement(PieceType.Pawn, PieceColor.White, 0, 1),
            Placement(PieceType.King, PieceColor.Black, 7, 7));

        Assert.True(_engine.TryApplyMove(state, new Square(1, 0), new Square(2, 2), out state));
        Assert.True(_engine.TryApplyMove(state, new Square(7, 7), new Square(6, 7), out state));
        Assert.True(_engine.TryApplyMove(state, new Square(2, 2), new Square(1, 0), out state));
        Assert.True(_engine.TryApplyMove(state, new Square(6, 7), new Square(7, 7), out state));
        Assert.Equal(GameStatus.InProgress, state.Status);

        Assert.True(_engine.TryApplyMove(state, new Square(1, 0), new Square(2, 2), out state));
        Assert.True(_engine.TryApplyMove(state, new Square(7, 7), new Square(6, 7), out state));
        Assert.True(_engine.TryApplyMove(state, new Square(2, 2), new Square(1, 0), out state));
        Assert.True(_engine.TryApplyMove(state, new Square(6, 7), new Square(7, 7), out state));

        Assert.Equal(GameStatus.Draw, state.Status);
    }

    [Fact]
    public void TryApplyMove_InsufficientMaterial_SetsDrawStatus()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            0,
            1,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7));

        var moveApplied = _engine.TryApplyMove(state, new Square(4, 0), new Square(4, 1), out var updatedState);
        Assert.True(moveApplied);
        Assert.Equal(GameStatus.Draw, updatedState.Status);
    }

    [Fact]
    public void TryApplyMove_InsufficientMaterial_BishopVsKnight_RemainsInProgress()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            0,
            1,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Bishop, PieceColor.White, 2, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Knight, PieceColor.Black, 6, 7));

        var moveApplied = _engine.TryApplyMove(state, new Square(2, 0), new Square(1, 1), out var updatedState);
        Assert.True(moveApplied);
        Assert.Equal(GameStatus.InProgress, updatedState.Status);
    }

    [Fact]
    public void TryApplyMove_InsufficientMaterial_OppositeColorBishops_RemainsInProgress()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            0,
            1,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Bishop, PieceColor.White, 2, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Bishop, PieceColor.Black, 2, 7));

        var moveApplied = _engine.TryApplyMove(state, new Square(2, 0), new Square(1, 1), out var updatedState);
        Assert.True(moveApplied);
        Assert.Equal(GameStatus.InProgress, updatedState.Status);
    }

    private static PiecePlacement Placement(PieceType pieceType, PieceColor color, int file, int rank)
    {
        return new PiecePlacement(new Square(file, rank), new Piece(pieceType, color));
    }

    private static GameState CreateState(
        PieceColor sideToMove,
        CastlingRights castlingRights,
        Square? enPassantTarget,
        int halfmoveClock,
        int fullmoveNumber,
        params PiecePlacement[] pieces)
    {
        return new GameState(
            Pieces: pieces.OrderBy(piece => piece.Square.Rank).ThenBy(piece => piece.Square.File).ToArray(),
            SideToMove: sideToMove,
            CastlingRights: castlingRights,
            EnPassantTarget: enPassantTarget,
            HalfmoveClock: halfmoveClock,
            FullmoveNumber: fullmoveNumber,
            Status: GameStatus.InProgress,
            MoveHistory: [],
            PositionHistory: []);
    }
}
