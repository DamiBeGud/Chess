using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Chess.Domain;
using Chess.Engine;

namespace Chess.AI;

/// <summary>
/// NegamaxAiMoveSelector is a concrete type within the AI module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include App (AppShell).
/// Key collaborators are IGameEngine, IAiPositionEvaluator, IAiMoveSelector.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> App (AppShell)</para>
/// <para><b>Usage pattern:</b> Startup code constructs and wires this type during application initialization and desktop-lifetime setup.</para>
/// <para><b>Dependencies/Collaborators:</b> IGameEngine, IAiPositionEvaluator, IAiMoveSelector.</para>
/// <para><b>Boundary:</b> This type sits in the application shell boundary and participates in startup or desktop lifetime wiring.</para>
/// </remarks>
public sealed class NegamaxAiMoveSelector : IAiMoveSelector
{
    private readonly IGameEngine _gameEngine;
    private readonly IAiPositionEvaluator _positionEvaluator;

    public NegamaxAiMoveSelector(IGameEngine gameEngine, IAiPositionEvaluator positionEvaluator)
    {
        _gameEngine = gameEngine ?? throw new ArgumentNullException(nameof(gameEngine));
        _positionEvaluator = positionEvaluator ?? throw new ArgumentNullException(nameof(positionEvaluator));
    }

    public Move? SelectBestMove(
        GameState gameState,
        PieceColor aiColor,
        int searchDepth,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        if (gameState.Status != GameStatus.InProgress || gameState.SideToMove != aiColor)
        {
            return null;
        }

        var boundedDepth = Math.Clamp(searchDepth, 1, 2);
        var legalMoves = OrderMoves(_gameEngine.GenerateLegalMoves(gameState));
        if (legalMoves.Count == 0)
        {
            return null;
        }

        Move? bestMove = null;
        var bestScore = int.MinValue;
        var bestMoveKey = string.Empty;

        foreach (var move in legalMoves)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_gameEngine.TryApplyMove(gameState, move.From, move.To, out var nextState, move.PromotionPieceType))
            {
                continue;
            }

            var score = EvaluateNode(nextState, boundedDepth - 1, aiColor, cancellationToken);
            var moveKey = GetMoveSortKey(move);

            if (score > bestScore
                || (score == bestScore && (bestMove is null || string.CompareOrdinal(moveKey, bestMoveKey) < 0)))
            {
                bestScore = score;
                bestMove = move;
                bestMoveKey = moveKey;
            }
        }

        return bestMove;
    }

    private int EvaluateNode(
        GameState gameState,
        int depthRemaining,
        PieceColor aiColor,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (gameState.Status != GameStatus.InProgress || depthRemaining <= 0)
        {
            return _positionEvaluator.Evaluate(gameState, aiColor);
        }

        var legalMoves = OrderMoves(_gameEngine.GenerateLegalMoves(gameState));
        if (legalMoves.Count == 0)
        {
            return _positionEvaluator.Evaluate(gameState, aiColor);
        }

        var maximize = gameState.SideToMove == aiColor;
        var bestScore = maximize ? int.MinValue : int.MaxValue;

        foreach (var move in legalMoves)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_gameEngine.TryApplyMove(gameState, move.From, move.To, out var nextState, move.PromotionPieceType))
            {
                continue;
            }

            var childScore = EvaluateNode(nextState, depthRemaining - 1, aiColor, cancellationToken);
            bestScore = maximize
                ? Math.Max(bestScore, childScore)
                : Math.Min(bestScore, childScore);
        }

        return bestScore;
    }

    private static IReadOnlyList<Move> OrderMoves(IReadOnlyList<Move> moves)
    {
        return moves
            .OrderBy(GetMoveSortKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetMoveSortKey(Move move)
    {
        return $"{BoardGeometry.ToCoordinate(move.From)}:{BoardGeometry.ToCoordinate(move.To)}:{(int?)move.PromotionPieceType ?? -1}:{move.IsCastling}:{move.IsEnPassant}";
    }
}
