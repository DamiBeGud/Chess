using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
using Xunit;

namespace Chess.Tests;

public sealed class EngineCollaboratorRegressionTests
{
    private readonly ChessGameEngine _engine = new();

    [Fact]
    public void GenerateLegalMoves_PinnedRook_RemainsRestrictedToKingSafetyLine()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.None,
            null,
            0,
            1,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 4, 1),
            Placement(PieceType.King, PieceColor.Black, 0, 7),
            Placement(PieceType.Rook, PieceColor.Black, 4, 7));

        var pseudoMoves = _engine.GeneratePseudoLegalMoves(state, new Square(4, 1));
        var legalMoves = _engine.GenerateLegalMoves(state, new Square(4, 1));

        Assert.Contains(pseudoMoves, move => move.To == new Square(3, 1));
        Assert.DoesNotContain(legalMoves, move => move.To == new Square(3, 1));

        var legalDestinations = legalMoves
            .Select(move => $"{move.To.File},{move.To.Rank}")
            .OrderBy(destination => destination)
            .ToArray();
        Assert.Equal(["4,2", "4,3", "4,4", "4,5", "4,6", "4,7"], legalDestinations);
    }

    [Fact]
    public void TryApplyMove_CastlingTransition_PreservesBoardRightsAndHistoryContracts()
    {
        var state = CreateState(
            PieceColor.White,
            CastlingRights.WhiteKingSide,
            null,
            0,
            1,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 7, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7));

        var moveApplied = _engine.TryApplyMove(state, new Square(4, 0), new Square(6, 0), out var updatedState);

        Assert.True(moveApplied);
        Assert.Equal(PieceColor.Black, updatedState.SideToMove);
        Assert.Equal(CastlingRights.None, updatedState.CastlingRights);
        Assert.Null(updatedState.EnPassantTarget);
        Assert.Equal(1, updatedState.HalfmoveClock);
        Assert.Equal(1, updatedState.FullmoveNumber);
        Assert.Equal(GameStatus.InProgress, updatedState.Status);

        Assert.Contains(updatedState.Pieces, placement =>
            placement.Square == new Square(6, 0)
            && placement.Piece.Type == PieceType.King
            && placement.Piece.Color == PieceColor.White
            && placement.Piece.HasMoved);
        Assert.Contains(updatedState.Pieces, placement =>
            placement.Square == new Square(5, 0)
            && placement.Piece.Type == PieceType.Rook
            && placement.Piece.Color == PieceColor.White
            && placement.Piece.HasMoved);

        var recordedMove = Assert.Single(updatedState.MoveHistory);
        Assert.Equal(new Square(4, 0), recordedMove.From);
        Assert.Equal(new Square(6, 0), recordedMove.To);
        Assert.True(recordedMove.IsCastling);
        Assert.True(recordedMove.MovedPiece.HasMoved);
    }

    [Fact]
    public async Task SaveLoad_MidgameRoundTrip_PreservesContinuationLegalMoves()
    {
        var store = new JsonGameStateStore();
        var state = _engine.CreateInitialGameState();

        Assert.True(_engine.TryApplyMove(state, new Square(4, 1), new Square(4, 3), out state));
        Assert.True(_engine.TryApplyMove(state, new Square(4, 6), new Square(4, 4), out state));
        Assert.True(_engine.TryApplyMove(state, new Square(6, 0), new Square(5, 2), out state));
        Assert.True(_engine.TryApplyMove(state, new Square(1, 7), new Square(2, 5), out var beforeSave));

        var filePath = Path.Combine(Path.GetTempPath(), $"chess-save-{Path.GetRandomFileName()}.json");
        try
        {
            await store.SaveAsync(filePath, beforeSave);
            var loaded = await store.LoadAsync(filePath);

            var expectedLegalMoves = NormalizeMoves(_engine.GenerateLegalMoves(beforeSave))
                .OrderBy(move => move)
                .ToArray();
            var loadedLegalMoves = NormalizeMoves(_engine.GenerateLegalMoves(loaded))
                .OrderBy(move => move)
                .ToArray();

            Assert.Equal(beforeSave.CastlingRights, loaded.CastlingRights);
            Assert.Equal(beforeSave.EnPassantTarget, loaded.EnPassantTarget);
            Assert.Equal(beforeSave.PositionHistory ?? [], loaded.PositionHistory ?? []);
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

    private static IReadOnlyList<string> NormalizeMoves(IReadOnlyList<Move> moves)
    {
        return moves
            .Select(m =>
                $"{m.From.File}:{m.From.Rank}->{m.To.File}:{m.To.Rank}:{m.MovedPiece.Color}:{m.MovedPiece.Type}:{m.MovedPiece.HasMoved}:{m.CapturedPiece?.Color}:{m.CapturedPiece?.Type}:{m.CapturedPiece?.HasMoved}:{m.IsCastling}:{m.IsEnPassant}:{m.PromotionPieceType}")
            .ToArray();
    }
}
