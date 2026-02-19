using System;
using Chess.Domain;
using Chess.Engine;

namespace Chess.AI;

public sealed class MaterialMobilityPositionEvaluator : IAiPositionEvaluator
{
    private readonly IGameEngine _gameEngine;

    public MaterialMobilityPositionEvaluator(IGameEngine gameEngine)
    {
        _gameEngine = gameEngine ?? throw new ArgumentNullException(nameof(gameEngine));
    }

    public int Evaluate(GameState gameState, PieceColor perspectiveColor)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        var terminalScore = EvaluateTerminal(gameState, perspectiveColor);
        if (terminalScore is not null)
        {
            return terminalScore.Value;
        }

        var materialScore = 0;
        foreach (var placement in gameState.Pieces)
        {
            var pieceValue = GetPieceValue(placement.Piece.Type);
            materialScore += placement.Piece.Color == perspectiveColor ? pieceValue : -pieceValue;
        }

        var opponent = GetOpponentColor(perspectiveColor);
        var perspectiveMobility = _gameEngine.GenerateLegalMoves(gameState with { SideToMove = perspectiveColor }).Count;
        var opponentMobility = _gameEngine.GenerateLegalMoves(gameState with { SideToMove = opponent }).Count;
        var mobilityScore = (perspectiveMobility - opponentMobility) * 8;

        return materialScore + mobilityScore;
    }

    private static int? EvaluateTerminal(GameState gameState, PieceColor perspectiveColor)
    {
        return gameState.Status switch
        {
            GameStatus.WhiteWin => perspectiveColor == PieceColor.White ? 1_000_000 : -1_000_000,
            GameStatus.BlackWin => perspectiveColor == PieceColor.Black ? 1_000_000 : -1_000_000,
            GameStatus.Draw => 0,
            GameStatus.InProgress => null,
            _ => 0
        };
    }

    private static int GetPieceValue(PieceType pieceType)
    {
        return pieceType switch
        {
            PieceType.Pawn => 100,
            PieceType.Knight => 320,
            PieceType.Bishop => 330,
            PieceType.Rook => 500,
            PieceType.Queen => 900,
            PieceType.King => 20_000,
            _ => 0
        };
    }

    private static PieceColor GetOpponentColor(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}
