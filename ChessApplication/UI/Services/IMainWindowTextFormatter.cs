using System.Collections.Generic;
using Chess.Domain;
using Chess.UI.ViewModels;

namespace Chess.UI.Services;

internal interface IMainWindowTextFormatter
{
    string ToCoordinate(Square square);
    string BuildFocusedSquareText(Square square);
    string BuildGameStatusText(GameState gameState);
    string BuildHumanMoveLastAction(GameState previousState, Square fromSquare, Square toSquare, PieceType? movedPieceType);
    string BuildAiMoveLastAction(PieceColor aiColor, Move aiMove);
    string BuildNoPieceSelectionFeedback(Square square, PieceColor sideToMove);
    string BuildOpponentPieceSelectionFeedback(Piece piece, Square square, PieceColor sideToMove);
    string BuildNoLegalMovesFeedback(Square square);
    string BuildMoveRejectedFeedback(Square fromSquare, Square toSquare);
    string BuildInvalidMoveTargetFeedback(
        Square targetSquare,
        Square selectedSquare,
        IReadOnlyCollection<Square> legalDestinationSquares);
    IReadOnlyList<MoveHistoryEntryViewModel> BuildMoveHistoryEntries(IReadOnlyList<Move> moveHistory);
}
