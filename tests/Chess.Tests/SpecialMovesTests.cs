using System.Linq;
using Chess.Domain;
using Chess.Engine;
using Xunit;

namespace Chess.Tests;

public sealed class SpecialMovesTests
{
    private readonly ChessGameEngine _engine = new();

    [Fact]
    public void Castling_IsAllowedWhenConstraintsPass_AndUpdatesBoardAndRights()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.WhiteKingSide,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 7, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7));

        var kingMoves = _engine.GenerateLegalMoves(state, new Square(4, 0));
        Assert.Contains(kingMoves, move => move.To == new Square(6, 0) && move.IsCastling);

        var moveApplied = _engine.TryApplyMove(state, new Square(4, 0), new Square(6, 0), out var updatedState);
        Assert.True(moveApplied);

        Assert.Contains(updatedState.Pieces, p => p.Square == new Square(6, 0) && p.Piece.Type == PieceType.King && p.Piece.Color == PieceColor.White);
        Assert.Contains(updatedState.Pieces, p => p.Square == new Square(5, 0) && p.Piece.Type == PieceType.Rook && p.Piece.Color == PieceColor.White);
        Assert.DoesNotContain(updatedState.Pieces, p => p.Square == new Square(4, 0));
        Assert.DoesNotContain(updatedState.Pieces, p => p.Square == new Square(7, 0));

        Assert.Equal(CastlingRights.None, updatedState.CastlingRights);
        var recordedMove = Assert.Single(updatedState.MoveHistory);
        Assert.True(recordedMove.IsCastling);
    }

    [Fact]
    public void QueenSideCastling_IsAllowedWhenConstraintsPass()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.WhiteQueenSide,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 0, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7));

        var kingMoves = _engine.GenerateLegalMoves(state, new Square(4, 0));
        Assert.Contains(kingMoves, move => move.To == new Square(2, 0) && move.IsCastling);

        var moveApplied = _engine.TryApplyMove(state, new Square(4, 0), new Square(2, 0), out var updatedState);
        Assert.True(moveApplied);

        Assert.Contains(updatedState.Pieces, p => p.Square == new Square(2, 0) && p.Piece.Type == PieceType.King && p.Piece.Color == PieceColor.White);
        Assert.Contains(updatedState.Pieces, p => p.Square == new Square(3, 0) && p.Piece.Type == PieceType.Rook && p.Piece.Color == PieceColor.White);
        Assert.DoesNotContain(updatedState.Pieces, p => p.Square == new Square(4, 0));
        Assert.DoesNotContain(updatedState.Pieces, p => p.Square == new Square(0, 0));
    }

    [Fact]
    public void Castling_IsRejectedWhenKingInCheckOrCrossesAttackedSquare()
    {
        var kingInCheckState = CreateState(
            PieceColor.White,
            CastlingRights.WhiteKingSide,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 7, 0),
            Placement(PieceType.Rook, PieceColor.Black, 4, 7),
            Placement(PieceType.King, PieceColor.Black, 7, 7));

        Assert.False(_engine.IsMoveLegal(kingInCheckState, new Square(4, 0), new Square(6, 0)));

        var attackedTransitState = CreateState(
            PieceColor.White,
            CastlingRights.WhiteKingSide,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 7, 0),
            Placement(PieceType.Rook, PieceColor.Black, 5, 7),
            Placement(PieceType.King, PieceColor.Black, 7, 7));

        Assert.False(_engine.IsMoveLegal(attackedTransitState, new Square(4, 0), new Square(6, 0)));
    }

    [Fact]
    public void EnPassant_IsAvailableOnlyImmediatelyAfterPawnDoubleStep()
    {
        var state = CreateState(
            PieceColor.Black,
            CastlingRights.None,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Pawn, PieceColor.White, 4, 4),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Pawn, PieceColor.Black, 3, 6));

        var blackDoubleStepApplied = _engine.TryApplyMove(state, new Square(3, 6), new Square(3, 4), out var afterBlackDoubleStep);
        Assert.True(blackDoubleStepApplied);
        Assert.Equal(new Square(3, 5), afterBlackDoubleStep.EnPassantTarget);

        var whiteMoves = _engine.GenerateLegalMoves(afterBlackDoubleStep, new Square(4, 4));
        Assert.Contains(whiteMoves, move => move.To == new Square(3, 5) && move.IsEnPassant);

        var whiteQuietMoveApplied = _engine.TryApplyMove(afterBlackDoubleStep, new Square(4, 0), new Square(5, 0), out var afterWhiteQuietMove);
        Assert.True(whiteQuietMoveApplied);
        Assert.Null(afterWhiteQuietMove.EnPassantTarget);

        var blackReplyApplied = _engine.TryApplyMove(afterWhiteQuietMove, new Square(4, 7), new Square(5, 7), out var afterBlackReply);
        Assert.True(blackReplyApplied);

        Assert.False(_engine.IsMoveLegal(afterBlackReply, new Square(4, 4), new Square(3, 5)));
    }

    [Fact]
    public void EnPassant_ExecutionRemovesCapturedPawnAndRecordsFlags()
    {
        var state = CreateState(
            PieceColor.Black,
            CastlingRights.None,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Pawn, PieceColor.White, 4, 4),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Pawn, PieceColor.Black, 3, 6));

        var blackDoubleStepApplied = _engine.TryApplyMove(state, new Square(3, 6), new Square(3, 4), out var afterBlackDoubleStep);
        Assert.True(blackDoubleStepApplied);

        var whiteEnPassantApplied = _engine.TryApplyMove(afterBlackDoubleStep, new Square(4, 4), new Square(3, 5), out var afterEnPassant);
        Assert.True(whiteEnPassantApplied);

        Assert.Contains(afterEnPassant.Pieces, p => p.Square == new Square(3, 5) && p.Piece.Type == PieceType.Pawn && p.Piece.Color == PieceColor.White);
        Assert.DoesNotContain(afterEnPassant.Pieces, p => p.Square == new Square(3, 4));
        Assert.Null(afterEnPassant.EnPassantTarget);

        var enPassantMove = afterEnPassant.MoveHistory.Last();
        Assert.True(enPassantMove.IsEnPassant);
        Assert.NotNull(enPassantMove.CapturedPiece);
        Assert.Equal(PieceType.Pawn, enPassantMove.CapturedPiece!.Type);
        Assert.Equal(PieceColor.Black, enPassantMove.CapturedPiece.Color);
    }

    [Fact]
    public void Promotion_UsesDefaultQueenAndSupportsSelectedPiece()
    {
        var defaultPromotionState = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Pawn, PieceColor.White, 0, 6));

        var defaultPromotionApplied = _engine.TryApplyMove(defaultPromotionState, new Square(0, 6), new Square(0, 7), out var afterDefaultPromotion);
        Assert.True(defaultPromotionApplied);
        Assert.True(_engine.IsMoveLegal(defaultPromotionState, new Square(0, 6), new Square(0, 7)));

        var promotedQueen = afterDefaultPromotion.Pieces.Single(p => p.Square == new Square(0, 7)).Piece;
        Assert.Equal(PieceType.Queen, promotedQueen.Type);
        Assert.Equal(PieceColor.White, promotedQueen.Color);
        Assert.Equal(PieceType.Queen, Assert.Single(afterDefaultPromotion.MoveHistory).PromotionPieceType);

        var selectedPromotionState = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Pawn, PieceColor.White, 1, 6));

        var selectedPromotionApplied = _engine.TryApplyMove(
            selectedPromotionState,
            new Square(1, 6),
            new Square(1, 7),
            out var afterSelectedPromotion,
            promotionPieceType: PieceType.Knight);

        Assert.True(selectedPromotionApplied);
        var promotedKnight = afterSelectedPromotion.Pieces.Single(p => p.Square == new Square(1, 7)).Piece;
        Assert.Equal(PieceType.Knight, promotedKnight.Type);
        Assert.Equal(PieceColor.White, promotedKnight.Color);
        Assert.Equal(PieceType.Knight, Assert.Single(afterSelectedPromotion.MoveHistory).PromotionPieceType);
    }

    [Fact]
    public void CastlingRights_AreUpdatedWhenRooksMoveOrAreCapturedOnHomeSquares()
    {
        var rookMoveState = CreateState(
            PieceColor.White,
            CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 7, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7));

        var rookMoveApplied = _engine.TryApplyMove(rookMoveState, new Square(7, 0), new Square(7, 1), out var afterRookMove);
        Assert.True(rookMoveApplied);
        Assert.Equal(CastlingRights.WhiteQueenSide, afterRookMove.CastlingRights);

        var rookCaptureState = CreateState(
            PieceColor.White,
            CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 0, 1),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Rook, PieceColor.Black, 0, 7));

        var rookCaptureApplied = _engine.TryApplyMove(rookCaptureState, new Square(0, 1), new Square(0, 7), out var afterRookCapture);
        Assert.True(rookCaptureApplied);
        Assert.Equal(CastlingRights.BlackKingSide, afterRookCapture.CastlingRights);
    }

    private static PiecePlacement Placement(PieceType pieceType, PieceColor color, int file, int rank)
    {
        return new PiecePlacement(new Square(file, rank), new Piece(pieceType, color));
    }

    private static GameState CreateState(
        PieceColor sideToMove,
        CastlingRights castlingRights,
        Square? enPassantTarget,
        params PiecePlacement[] pieces)
    {
        return new GameState(
            Pieces: pieces,
            SideToMove: sideToMove,
            CastlingRights: castlingRights,
            EnPassantTarget: enPassantTarget,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: []);
    }
}
