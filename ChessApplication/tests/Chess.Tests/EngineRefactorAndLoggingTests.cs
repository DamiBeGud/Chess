using System.Collections.Generic;
using System.Linq;
using Chess.Domain;
using Chess.Engine;
using Xunit;

namespace Chess.Tests;

public sealed class EngineRefactorAndLoggingTests
{
    [Fact]
    public void IsMoveLegal_LogsStructuredRejection_ForEmptySourceSquare()
    {
        var logger = new CapturingEngineLogger();
        var engine = CreateEngine(logger, EngineLogCategory.MoveValidation);
        var state = engine.CreateInitialGameState();

        var isLegal = engine.IsMoveLegal(state, new Square(4, 4), new Square(4, 5));

        Assert.False(isLegal);
        var entry = Assert.Single(logger.Entries, e => e.EventName == "MoveRejected");
        Assert.Equal(EngineLogCategory.MoveValidation, entry.Category);
        Assert.Equal("No piece exists on source square.", entry.Message);
        Assert.Equal(new Square(4, 4), entry.From);
        Assert.Equal(new Square(4, 5), entry.To);
    }

    [Fact]
    public void TryApplyMove_LogsPromotionSelectionRejection_WhenPromotionChoiceIsInvalid()
    {
        var logger = new CapturingEngineLogger();
        var engine = CreateEngine(logger, EngineLogCategory.MoveValidation);
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Pawn, PieceColor.White, 0, 6));

        var moveApplied = engine.TryApplyMove(
            state,
            new Square(0, 6),
            new Square(0, 7),
            out _,
            promotionPieceType: PieceType.Pawn);

        Assert.False(moveApplied);
        var entry = Assert.Single(logger.Entries, e => e.EventName == "MoveRejected");
        Assert.Equal("Promotion selection is incompatible with legal promotion moves.", entry.Message);
    }

    [Fact]
    public void GenerateLegalMoves_LogsCastlingEligibilityDecision()
    {
        var logger = new CapturingEngineLogger();
        var engine = CreateEngine(logger, EngineLogCategory.SpecialMoves);
        var state = CreateState(
            PieceColor.White,
            CastlingRights.WhiteKingSide,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 7, 0),
            Placement(PieceType.Rook, PieceColor.Black, 4, 7),
            Placement(PieceType.King, PieceColor.Black, 7, 7));

        var kingMoves = engine.GenerateLegalMoves(state, new Square(4, 0));

        Assert.DoesNotContain(kingMoves, move => move.IsCastling);
        Assert.Contains(logger.Entries, entry =>
            entry.EventName == "CastlingEvaluated"
            && entry.Category == EngineLogCategory.SpecialMoves
            && entry.Message.Contains("king is currently in check"));
    }

    [Fact]
    public void TryApplyMove_LogsGameStatusDecision_OnCheckmate()
    {
        var logger = new CapturingEngineLogger();
        var engine = CreateEngine(logger, EngineLogCategory.GameStatus);
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            Placement(PieceType.King, PieceColor.White, 5, 5),
            Placement(PieceType.Queen, PieceColor.White, 6, 5),
            Placement(PieceType.King, PieceColor.Black, 7, 7));

        var moveApplied = engine.TryApplyMove(state, new Square(6, 5), new Square(6, 6), out var updatedState);

        Assert.True(moveApplied);
        Assert.Equal(GameStatus.WhiteWin, updatedState.Status);
        Assert.Contains(logger.Entries, entry =>
            entry.EventName == "GameStatusEvaluated"
            && entry.GameStatus == GameStatus.WhiteWin
            && entry.Message.Contains("checkmate"));
    }

    [Fact]
    public void GenerateLegalMoves_InitialPosition_RemainsDeterministicWithExpectedMoveCount()
    {
        var engine = new ChessGameEngine();
        var state = engine.CreateInitialGameState();

        var firstPass = engine.GenerateLegalMoves(state).Select(ToMoveKey).ToArray();
        var secondPass = engine.GenerateLegalMoves(state).Select(ToMoveKey).ToArray();

        Assert.Equal(20, firstPass.Length);
        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void IsKingInCheck_SlidingAttacksRespectBlockingPieces()
    {
        var engine = new ChessGameEngine();
        var blockedState = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Knight, PieceColor.White, 2, 2),
            Placement(PieceType.Bishop, PieceColor.Black, 0, 4),
            Placement(PieceType.King, PieceColor.Black, 7, 7));

        var unblockedState = blockedState with
        {
            Pieces = blockedState.Pieces.Where(p => p.Square != new Square(2, 2)).ToArray()
        };

        Assert.False(engine.IsKingInCheck(blockedState, PieceColor.White));
        Assert.True(engine.IsKingInCheck(unblockedState, PieceColor.White));
    }

    private static ChessGameEngine CreateEngine(CapturingEngineLogger logger, EngineLogCategory categories)
    {
        return new ChessGameEngine(new ChessGameEngineOptions(logger, categories));
    }

    private static string ToMoveKey(Move move)
    {
        return $"{move.From.File},{move.From.Rank}:{move.To.File},{move.To.Rank}:{(int?)move.PromotionPieceType ?? -1}";
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

    private sealed class CapturingEngineLogger : IChessEngineLogger
    {
        public bool IsEnabled => true;

        public List<EngineLogEntry> Entries { get; } = [];

        public void Log(in EngineLogEntry entry)
        {
            Entries.Add(entry);
        }
    }
}
