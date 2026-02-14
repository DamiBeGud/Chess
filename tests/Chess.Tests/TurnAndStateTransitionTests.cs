using System.Collections.Generic;
using System.Linq;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
using Xunit;

namespace Chess.Tests;

public sealed class TurnAndStateTransitionTests
{
    private readonly ChessGameEngine _engine = new();

    [Fact]
    public void TryApplyMove_TogglesTurnOnlyAfterSuccessfulMove()
    {
        var initialState = _engine.CreateInitialGameState();

        var whiteMoveApplied = _engine.TryApplyMove(
            initialState,
            new Square(4, 1),
            new Square(4, 3),
            out var afterWhiteMove);

        Assert.True(whiteMoveApplied);
        Assert.Equal(PieceColor.Black, afterWhiteMove.SideToMove);

        var invalidMoveApplied = _engine.TryApplyMove(
            afterWhiteMove,
            new Square(4, 1),
            new Square(4, 2),
            out var afterInvalidMoveAttempt);

        Assert.False(invalidMoveApplied);
        Assert.Equal(afterWhiteMove, afterInvalidMoveAttempt);
        Assert.Equal(PieceColor.Black, afterInvalidMoveAttempt.SideToMove);
    }

    [Fact]
    public void TryApplyMove_RecordsMoveHistoryWithFromToAndFlags()
    {
        var initialState = _engine.CreateInitialGameState();

        var moveApplied = _engine.TryApplyMove(
            initialState,
            new Square(4, 1),
            new Square(4, 3),
            out var updatedState);

        Assert.True(moveApplied);
        var recordedMove = Assert.Single(updatedState.MoveHistory);

        Assert.Equal(new Square(4, 1), recordedMove.From);
        Assert.Equal(new Square(4, 3), recordedMove.To);
        Assert.Equal(PieceType.Pawn, recordedMove.MovedPiece.Type);
        Assert.Equal(PieceColor.White, recordedMove.MovedPiece.Color);
        Assert.True(recordedMove.MovedPiece.HasMoved);
        Assert.Null(recordedMove.CapturedPiece);
        Assert.False(recordedMove.IsCastling);
        Assert.False(recordedMove.IsEnPassant);
        Assert.Null(recordedMove.PromotionPieceType);
    }

    [Fact]
    public void TryApplyMove_TracksCapturesInBoardAndMoveHistory()
    {
        var state = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 0, 0),
            Placement(PieceType.King, PieceColor.Black, 7, 7),
            Placement(PieceType.Knight, PieceColor.Black, 0, 3));

        var moveApplied = _engine.TryApplyMove(
            state,
            new Square(0, 0),
            new Square(0, 3),
            out var updatedState);

        Assert.True(moveApplied);
        Assert.Equal(PieceColor.Black, updatedState.SideToMove);
        Assert.Equal(0, updatedState.HalfmoveClock);

        var pieceOnCaptureSquare = updatedState.Pieces.Single(p => p.Square == new Square(0, 3)).Piece;
        Assert.Equal(PieceType.Rook, pieceOnCaptureSquare.Type);
        Assert.Equal(PieceColor.White, pieceOnCaptureSquare.Color);
        Assert.True(pieceOnCaptureSquare.HasMoved);

        Assert.DoesNotContain(updatedState.Pieces, p => p.Piece.Color == PieceColor.Black && p.Piece.Type == PieceType.Knight);

        var recordedMove = Assert.Single(updatedState.MoveHistory);
        Assert.NotNull(recordedMove.CapturedPiece);
        Assert.Equal(PieceType.Knight, recordedMove.CapturedPiece!.Type);
        Assert.Equal(PieceColor.Black, recordedMove.CapturedPiece.Color);
    }

    [Fact]
    public void GameSessionService_TryMakeMoveAppliesSingleStateTransition()
    {
        var sessionService = new GameSessionService(_engine, new JsonGameStateStore());

        var moveApplied = sessionService.TryMakeMove(new Square(4, 1), new Square(4, 3));
        Assert.True(moveApplied);

        var afterValidMove = sessionService.CurrentGameState;
        Assert.Equal(PieceColor.Black, afterValidMove.SideToMove);
        Assert.Single(afterValidMove.MoveHistory);

        var invalidMoveApplied = sessionService.TryMakeMove(new Square(4, 1), new Square(4, 2));
        Assert.False(invalidMoveApplied);

        Assert.Equal(afterValidMove, sessionService.CurrentGameState);
    }

    [Fact]
    public void TryApplyMove_IncrementsFullmoveNumberAfterSuccessfulBlackMove()
    {
        var initialState = _engine.CreateInitialGameState();

        var whiteMoveApplied = _engine.TryApplyMove(
            initialState,
            new Square(4, 1),
            new Square(4, 3),
            out var afterWhiteMove);
        Assert.True(whiteMoveApplied);
        Assert.Equal(1, afterWhiteMove.FullmoveNumber);

        var blackMoveApplied = _engine.TryApplyMove(
            afterWhiteMove,
            new Square(4, 6),
            new Square(4, 4),
            out var afterBlackMove);

        Assert.True(blackMoveApplied);
        Assert.Equal(2, afterBlackMove.FullmoveNumber);
        Assert.Equal(PieceColor.White, afterBlackMove.SideToMove);
        Assert.Equal(2, afterBlackMove.MoveHistory.Count);
    }

    [Fact]
    public void TryApplyMove_IncrementsHalfmoveClockForQuietNonPawnMove()
    {
        var state = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Knight, PieceColor.White, 1, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7))
            with
            {
                HalfmoveClock = 7,
                FullmoveNumber = 4
            };

        var moveApplied = _engine.TryApplyMove(
            state,
            new Square(1, 0),
            new Square(2, 2),
            out var updatedState);

        Assert.True(moveApplied);
        Assert.Equal(8, updatedState.HalfmoveClock);
        Assert.Equal(4, updatedState.FullmoveNumber);
        Assert.Equal(PieceColor.Black, updatedState.SideToMove);
    }

    private static PiecePlacement Placement(PieceType pieceType, PieceColor color, int file, int rank)
    {
        return new PiecePlacement(new Square(file, rank), new Piece(pieceType, color));
    }

    private static GameState CreateState(PieceColor sideToMove, params PiecePlacement[] pieces)
    {
        return new GameState(
            Pieces: pieces,
            SideToMove: sideToMove,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: []);
    }
}
