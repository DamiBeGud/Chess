using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
