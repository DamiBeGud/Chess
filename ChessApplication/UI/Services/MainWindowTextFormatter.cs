using System;
using System.Collections.Generic;
using System.Linq;
using Chess.Domain;
using Chess.UI.Assets;
using Chess.UI.ViewModels;

namespace Chess.UI.Services;

internal sealed class MainWindowTextFormatter : IMainWindowTextFormatter
{
    private readonly IPieceAssetResolver _pieceAssetResolver;

    internal MainWindowTextFormatter(IPieceAssetResolver pieceAssetResolver)
    {
        _pieceAssetResolver = pieceAssetResolver ?? throw new ArgumentNullException(nameof(pieceAssetResolver));
    }

    public string ToCoordinate(Square square)
    {
        return $"{(char)('a' + square.File)}{square.Rank + 1}";
    }

    public string BuildFocusedSquareText(Square square)
    {
        return $"Keyboard focus: {ToCoordinate(square)}.";
    }

    public string BuildGameStatusText(GameState gameState)
    {
        return gameState.Status switch
        {
            GameStatus.InProgress => $"Status: In progress. Side to move: {gameState.SideToMove}.",
            GameStatus.WhiteWin => "Status: White wins.",
            GameStatus.BlackWin => "Status: Black wins.",
            GameStatus.Draw => "Status: Draw.",
            _ => $"Status: {gameState.Status}."
        };
    }

    public string BuildHumanMoveLastAction(GameState previousState, Square fromSquare, Square toSquare, PieceType? movedPieceType)
    {
        var movedPieceName = movedPieceType?.ToString() ?? "Piece";
        return $"Last action: {previousState.SideToMove} moved {movedPieceName} from {ToCoordinate(fromSquare)} to {ToCoordinate(toSquare)}.";
    }

    public string BuildAiMoveLastAction(PieceColor aiColor, Move aiMove)
    {
        return $"Last action: AI ({aiColor}) moved {aiMove.MovedPiece.Type} from {ToCoordinate(aiMove.From)} to {ToCoordinate(aiMove.To)}.";
    }

    public string BuildNoPieceSelectionFeedback(Square square, PieceColor sideToMove)
    {
        return $"No piece at {ToCoordinate(square)}. Select one of your {sideToMove} pieces.";
    }

    public string BuildOpponentPieceSelectionFeedback(Piece piece, Square square, PieceColor sideToMove)
    {
        return $"Cannot select {piece.Color} piece at {ToCoordinate(square)}. It is {sideToMove} to move.";
    }

    public string BuildNoLegalMovesFeedback(Square square)
    {
        return $"Selected square {ToCoordinate(square)} has no legal moves.";
    }

    public string BuildMoveRejectedFeedback(Square fromSquare, Square toSquare)
    {
        return $"Move rejected: {ToCoordinate(fromSquare)} -> {ToCoordinate(toSquare)}.";
    }

    public string BuildInvalidMoveTargetFeedback(
        Square targetSquare,
        Square selectedSquare,
        IReadOnlyCollection<Square> legalDestinationSquares)
    {
        var legalDestinationsText = legalDestinationSquares.Count == 0
            ? "none"
            : string.Join(", ", legalDestinationSquares
                .Select(ToCoordinate)
                .OrderBy(coordinate => coordinate, StringComparer.Ordinal));

        return $"Invalid move target: {ToCoordinate(targetSquare)}. Legal destinations from {ToCoordinate(selectedSquare)}: {legalDestinationsText}.";
    }

    public IReadOnlyList<MoveHistoryEntryViewModel> BuildMoveHistoryEntries(IReadOnlyList<Move> moveHistory)
    {
        if (moveHistory.Count == 0)
        {
            return Array.Empty<MoveHistoryEntryViewModel>();
        }

        var entries = new List<MoveHistoryEntryViewModel>(moveHistory.Count);
        for (var index = 0; index < moveHistory.Count; index++)
        {
            var move = moveHistory[index];
            var moveNumber = (index / 2) + 1;
            var movePrefix = $"{moveNumber}.";
            var resolvedPieceAsset = _pieceAssetResolver.Resolve(move.MovedPiece);
            entries.Add(
                new MoveHistoryEntryViewModel(
                    movePrefix,
                    move.MovedPiece.Type,
                    move.MovedPiece.Color,
                    BuildMoveNotation(move),
                    resolvedPieceAsset));
        }

        return entries;
    }

    private string BuildMoveNotation(Move move)
    {
        if (move.IsCastling)
        {
            return move.To.File > move.From.File ? "O-O" : "O-O-O";
        }

        var separator = move.CapturedPiece is not null || move.IsEnPassant ? "x" : "-";
        var notation = $"{ToCoordinate(move.From)}{separator}{ToCoordinate(move.To)}";

        if (move.PromotionPieceType is not null)
        {
            notation = $"{notation}={ToPromotionSymbol(move.PromotionPieceType.Value)}";
        }

        if (move.IsEnPassant)
        {
            notation = $"{notation} e.p.";
        }

        return notation;
    }

    private static string ToPromotionSymbol(PieceType pieceType)
    {
        return pieceType switch
        {
            PieceType.Queen => "Q",
            PieceType.Rook => "R",
            PieceType.Bishop => "B",
            PieceType.Knight => "N",
            PieceType.King => "K",
            PieceType.Pawn => "P",
            _ => pieceType.ToString()
        };
    }
}
