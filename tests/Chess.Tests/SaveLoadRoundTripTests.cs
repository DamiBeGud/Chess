using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
using Xunit;

namespace Chess.Tests;

public sealed class SaveLoadRoundTripTests
{
    [Fact]
    public async Task SaveAndLoad_RestoresEquivalentStateMetadataAndBoard()
    {
        var engine = new ChessGameEngine();
        var store = new JsonGameStateStore();

        var initial = engine.CreateInitialGameState();
        var expected = initial with
        {
            SideToMove = PieceColor.Black,
            CastlingRights = CastlingRights.WhiteKingSide | CastlingRights.BlackQueenSide,
            EnPassantTarget = new Square(4, 2),
            HalfmoveClock = 12,
            FullmoveNumber = 22,
            MoveHistory = new List<Move>
            {
                new(
                    new Square(4, 1),
                    new Square(4, 3),
                    new Piece(PieceType.Pawn, PieceColor.White, HasMoved: true)),
                new(
                    new Square(1, 7),
                    new Square(2, 5),
                    new Piece(PieceType.Knight, PieceColor.Black, HasMoved: true),
                    new Piece(PieceType.Pawn, PieceColor.White, HasMoved: true)),
                new(
                    new Square(0, 6),
                    new Square(0, 7),
                    new Piece(PieceType.Pawn, PieceColor.White, HasMoved: true),
                    PromotionPieceType: PieceType.Queen)
            }
        };

        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");

        try
        {
            await store.SaveAsync(filePath, expected);
            var loaded = await store.LoadAsync(filePath);

            Assert.Equal(expected.SchemaVersion, loaded.SchemaVersion);
            Assert.Equal(expected.SideToMove, loaded.SideToMove);
            Assert.Equal(expected.CastlingRights, loaded.CastlingRights);
            Assert.Equal(expected.EnPassantTarget, loaded.EnPassantTarget);
            Assert.Equal(expected.HalfmoveClock, loaded.HalfmoveClock);
            Assert.Equal(expected.FullmoveNumber, loaded.FullmoveNumber);
            Assert.Equal(expected.Status, loaded.Status);
            Assert.Equal(expected.MoveHistory.Count, loaded.MoveHistory.Count);
            Assert.Equal(expected.PositionHistory ?? [], loaded.PositionHistory ?? []);
            Assert.Equal(NormalizeMoves(expected.MoveHistory), NormalizeMoves(loaded.MoveHistory));

            Assert.Equal(NormalizePieces(expected.Pieces), NormalizePieces(loaded.Pieces));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task Save_WithUnsupportedSchemaVersion_ThrowsInvalidDataException()
    {
        var engine = new ChessGameEngine();
        var store = new JsonGameStateStore();
        var unsupported = engine.CreateInitialGameState() with { SchemaVersion = 99 };
        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");

        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(filePath, unsupported));
    }

    [Fact]
    public async Task Load_WithUnsupportedSchemaVersion_ThrowsInvalidDataException()
    {
        var engine = new ChessGameEngine();
        var store = new JsonGameStateStore();
        var unsupported = engine.CreateInitialGameState() with { SchemaVersion = 99 };
        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");

        try
        {
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(unsupported));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(filePath));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task Load_WithLegacySchemaVersion_ThrowsInvalidDataException()
    {
        var engine = new ChessGameEngine();
        var store = new JsonGameStateStore();
        var legacy = engine.CreateInitialGameState() with { SchemaVersion = 1 };
        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");

        try
        {
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(legacy));
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(filePath));
            Assert.Contains("Legacy saves must be re-created", exception.Message);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task Load_WithCorruptedJson_ThrowsInvalidDataException()
    {
        var store = new JsonGameStateStore();
        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");

        try
        {
            await File.WriteAllTextAsync(filePath, "{ this is not valid json");
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(filePath));
            Assert.Contains("invalid or corrupted", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task Load_WithDuplicateSquareEntries_ThrowsInvalidDataException()
    {
        var engine = new ChessGameEngine();
        var store = new JsonGameStateStore();
        var invalidState = engine.CreateInitialGameState() with
        {
            Pieces = new List<PiecePlacement>
            {
                new(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
                new(new Square(4, 0), new Piece(PieceType.Rook, PieceColor.White)),
                new(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black))
            }
        };
        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");

        try
        {
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(invalidState));
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(filePath));
            Assert.Contains("Multiple pieces", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_AfterSpecialMoveSequence_PreservesEquivalentContinuation()
    {
        var engine = new ChessGameEngine();
        var store = new JsonGameStateStore();

        var state = new GameState(
            Pieces:
            [
                new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
                new PiecePlacement(new Square(7, 0), new Piece(PieceType.Rook, PieceColor.White)),
                new PiecePlacement(new Square(4, 4), new Piece(PieceType.Pawn, PieceColor.White)),
                new PiecePlacement(new Square(0, 6), new Piece(PieceType.Pawn, PieceColor.White)),
                new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)),
                new PiecePlacement(new Square(3, 6), new Piece(PieceType.Pawn, PieceColor.Black))
            ],
            SideToMove: PieceColor.White,
            CastlingRights: CastlingRights.WhiteKingSide,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: []);

        Assert.True(engine.TryApplyMove(state, new Square(4, 0), new Square(6, 0), out state));
        Assert.True(engine.TryApplyMove(state, new Square(3, 6), new Square(3, 4), out state));
        Assert.True(engine.TryApplyMove(state, new Square(4, 4), new Square(3, 5), out state));
        Assert.True(engine.TryApplyMove(state, new Square(4, 7), new Square(3, 7), out state));
        Assert.True(engine.TryApplyMove(state, new Square(0, 6), new Square(0, 7), out var expected));

        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");

        try
        {
            await store.SaveAsync(filePath, expected);
            var loaded = await store.LoadAsync(filePath);

            Assert.Equal(expected.CastlingRights, loaded.CastlingRights);
            Assert.Equal(expected.EnPassantTarget, loaded.EnPassantTarget);
            Assert.Equal(expected.SideToMove, loaded.SideToMove);
            Assert.Equal(expected.HalfmoveClock, loaded.HalfmoveClock);
            Assert.Equal(expected.FullmoveNumber, loaded.FullmoveNumber);
            Assert.Equal(expected.Status, loaded.Status);
            Assert.Equal(expected.SchemaVersion, loaded.SchemaVersion);
            Assert.Equal(expected.PositionHistory ?? [], loaded.PositionHistory ?? []);
            Assert.Equal(NormalizePieces(expected.Pieces), NormalizePieces(loaded.Pieces));
            Assert.Equal(NormalizeMoves(expected.MoveHistory), NormalizeMoves(loaded.MoveHistory));

            var expectedLegalMoves = NormalizeMoves(engine.GenerateLegalMoves(expected))
                .OrderBy(move => move)
                .ToArray();
            var loadedLegalMoves = NormalizeMoves(engine.GenerateLegalMoves(loaded))
                .OrderBy(move => move)
                .ToArray();
            Assert.Equal(expectedLegalMoves, loadedLegalMoves);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_PreservesThreefoldRepetitionContinuation()
    {
        var engine = new ChessGameEngine();
        var store = new JsonGameStateStore();

        var state = new GameState(
            Pieces:
            [
                new PiecePlacement(new Square(0, 0), new Piece(PieceType.King, PieceColor.White)),
                new PiecePlacement(new Square(1, 0), new Piece(PieceType.Knight, PieceColor.White)),
                new PiecePlacement(new Square(0, 1), new Piece(PieceType.Pawn, PieceColor.White)),
                new PiecePlacement(new Square(7, 7), new Piece(PieceType.King, PieceColor.Black))
            ],
            SideToMove: PieceColor.White,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: [],
            PositionHistory: []);

        Assert.True(engine.TryApplyMove(state, new Square(1, 0), new Square(2, 2), out state));
        Assert.True(engine.TryApplyMove(state, new Square(7, 7), new Square(6, 7), out state));
        Assert.True(engine.TryApplyMove(state, new Square(2, 2), new Square(1, 0), out state));
        Assert.True(engine.TryApplyMove(state, new Square(6, 7), new Square(7, 7), out var beforeSave));
        Assert.Equal(GameStatus.InProgress, beforeSave.Status);

        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");

        try
        {
            await store.SaveAsync(filePath, beforeSave);
            var loaded = await store.LoadAsync(filePath);

            Assert.True(engine.TryApplyMove(loaded, new Square(1, 0), new Square(2, 2), out loaded));
            Assert.True(engine.TryApplyMove(loaded, new Square(7, 7), new Square(6, 7), out loaded));
            Assert.True(engine.TryApplyMove(loaded, new Square(2, 2), new Square(1, 0), out loaded));
            Assert.True(engine.TryApplyMove(loaded, new Square(6, 7), new Square(7, 7), out loaded));

            Assert.Equal(GameStatus.Draw, loaded.Status);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_ApplicationFlow_AllowsContinueAndFinishAfterLoad()
    {
        var engine = new ChessGameEngine();
        var store = new JsonGameStateStore();
        var session = new GameSessionService(engine, store);
        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");

        try
        {
            session.StartNewGame();
            Assert.True(session.TryMakeMove(new Square(5, 1), new Square(5, 2)));
            Assert.True(session.TryMakeMove(new Square(4, 6), new Square(4, 4)));

            await session.SaveAsync(filePath);

            session.StartNewGame();
            Assert.Equal(PieceColor.White, session.CurrentGameState.SideToMove);

            var loaded = await session.LoadAsync(filePath);
            Assert.Equal(PieceColor.White, loaded.SideToMove);
            Assert.Equal(GameStatus.InProgress, loaded.Status);

            Assert.True(session.TryMakeMove(new Square(6, 1), new Square(6, 3)));
            Assert.True(session.TryMakeMove(new Square(3, 7), new Square(7, 3)));

            Assert.Equal(GameStatus.BlackWin, session.CurrentGameState.Status);
            Assert.Equal(PieceColor.White, session.CurrentGameState.SideToMove);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static IReadOnlyList<string> NormalizePieces(IReadOnlyList<PiecePlacement> pieces)
    {
        return pieces
            .Select(p => $"{p.Piece.Color}:{p.Piece.Type}:{p.Piece.HasMoved}:{p.Square.File}:{p.Square.Rank}")
            .OrderBy(p => p)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeMoves(IReadOnlyList<Move> moves)
    {
        return moves
            .Select(m =>
                $"{m.From.File}:{m.From.Rank}->{m.To.File}:{m.To.Rank}:{m.MovedPiece.Color}:{m.MovedPiece.Type}:{m.MovedPiece.HasMoved}:{m.CapturedPiece?.Color}:{m.CapturedPiece?.Type}:{m.CapturedPiece?.HasMoved}:{m.IsCastling}:{m.IsEnPassant}:{m.PromotionPieceType}")
            .ToArray();
    }
}
