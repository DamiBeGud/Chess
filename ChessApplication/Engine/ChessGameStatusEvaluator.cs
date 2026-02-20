using System;
using System.Collections.Generic;
using System.Linq;
using Chess.Domain;

namespace Chess.Engine;

/// <summary>
/// ChessGameStatusEvaluator is a concrete type within the Engine module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include ChessStateTransitionService (Engine), ChessGameEngine (Engine).
/// Key collaborators are Move, string.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessStateTransitionService (Engine), ChessGameEngine (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> Move, string.</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
internal sealed class ChessGameStatusEvaluator
{
    private readonly Func<GameState, IReadOnlyDictionary<Square, Piece>, IReadOnlyList<Move>> _generateLegalMoves;
    private readonly Action<PieceColor, int, GameStatus, string> _logGameStatusDecision;

    internal ChessGameStatusEvaluator(
        Func<GameState, IReadOnlyDictionary<Square, Piece>, IReadOnlyList<Move>> generateLegalMoves,
        Action<PieceColor, int, GameStatus, string> logGameStatusDecision)
    {
        _generateLegalMoves = generateLegalMoves ?? throw new ArgumentNullException(nameof(generateLegalMoves));
        _logGameStatusDecision = logGameStatusDecision ?? throw new ArgumentNullException(nameof(logGameStatusDecision));
    }

    internal GameStatus DetermineGameStatus(
        GameState state,
        IReadOnlyDictionary<Square, Piece> board,
        string currentPositionSignature,
        IReadOnlyList<string> positionHistory)
    {
        var legalMoves = _generateLegalMoves(state, board);
        if (legalMoves.Count == 0)
        {
            if (ChessAttackDetector.IsKingInCheck(board, state.SideToMove))
            {
                var winningStatus = state.SideToMove == PieceColor.White ? GameStatus.BlackWin : GameStatus.WhiteWin;
                _logGameStatusDecision(
                    state.SideToMove,
                    legalMoves.Count,
                    winningStatus,
                    "checkmate");
                return winningStatus;
            }

            _logGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.Draw, "stalemate");
            return GameStatus.Draw;
        }

        if (state.HalfmoveClock >= 100)
        {
            _logGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.Draw, "fifty-move rule");
            return GameStatus.Draw;
        }

        if (IsThreefoldRepetition(currentPositionSignature, positionHistory))
        {
            _logGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.Draw, "threefold repetition");
            return GameStatus.Draw;
        }

        if (IsInsufficientMaterial(state))
        {
            _logGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.Draw, "insufficient material");
            return GameStatus.Draw;
        }

        _logGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.InProgress, "legal moves remain");
        return GameStatus.InProgress;
    }

    internal static string BuildPositionSignature(GameState gameState)
    {
        var pieceLayout = string.Join(
            ",",
            gameState.Pieces
                .OrderBy(placement => placement.Square.Rank)
                .ThenBy(placement => placement.Square.File)
                .Select(placement =>
                    $"{ToPieceSymbol(placement.Piece)}{placement.Square.File}{placement.Square.Rank}"));

        var enPassant = gameState.EnPassantTarget is null
            ? "-"
            : $"{gameState.EnPassantTarget.Value.File}{gameState.EnPassantTarget.Value.Rank}";

        return $"{gameState.SideToMove}|{(int)gameState.CastlingRights}|{enPassant}|{pieceLayout}";
    }

    private static bool IsThreefoldRepetition(string currentPositionSignature, IReadOnlyList<string> positionHistory)
    {
        var occurrences = 0;

        foreach (var positionSignature in positionHistory)
        {
            if (positionSignature != currentPositionSignature)
            {
                continue;
            }

            occurrences++;
            if (occurrences >= 3)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsufficientMaterial(GameState gameState)
    {
        var nonKingPlacements = gameState.Pieces
            .Where(placement => placement.Piece.Type != PieceType.King)
            .ToArray();

        if (nonKingPlacements.Length == 0)
        {
            return true;
        }

        if (nonKingPlacements.Any(placement =>
                placement.Piece.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen))
        {
            return false;
        }

        if (nonKingPlacements.Length == 1)
        {
            return true;
        }

        var whiteMinorPlacements = nonKingPlacements
            .Where(placement => placement.Piece.Color == PieceColor.White)
            .ToArray();
        var blackMinorPlacements = nonKingPlacements
            .Where(placement => placement.Piece.Color == PieceColor.Black)
            .ToArray();

        if (whiteMinorPlacements.Length == 2
            && blackMinorPlacements.Length == 0
            && whiteMinorPlacements.All(placement => placement.Piece.Type == PieceType.Knight))
        {
            return true;
        }

        if (blackMinorPlacements.Length == 2
            && whiteMinorPlacements.Length == 0
            && blackMinorPlacements.All(placement => placement.Piece.Type == PieceType.Knight))
        {
            return true;
        }

        if (!nonKingPlacements.All(placement => placement.Piece.Type == PieceType.Bishop))
        {
            return false;
        }

        var hasLightSquareBishop = nonKingPlacements.Any(placement => IsLightSquare(placement.Square));
        var hasDarkSquareBishop = nonKingPlacements.Any(placement => !IsLightSquare(placement.Square));
        return !(hasLightSquareBishop && hasDarkSquareBishop);
    }

    private static char ToPieceSymbol(Piece piece)
    {
        var symbol = piece.Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => throw new ArgumentOutOfRangeException(nameof(piece))
        };

        return piece.Color == PieceColor.White ? char.ToUpperInvariant(symbol) : symbol;
    }

    private static bool IsLightSquare(Square square)
    {
        return (square.File + square.Rank) % 2 != 0;
    }
}
