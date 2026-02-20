using System;
using System.Collections.Generic;
using Chess.Domain;

namespace Chess.Online;

/// <summary>
/// OnlineSnapshotGameStateMapper is a concrete type within the Online module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include App (AppShell).
/// Key collaborators are IOnlineSnapshotGameStateMapper.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> App (AppShell)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> IOnlineSnapshotGameStateMapper.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed class OnlineSnapshotGameStateMapper : IOnlineSnapshotGameStateMapper
{
    public GameState Map(OnlineMatchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var pieces = ParseBoard(snapshot.Board);
        return new GameState(
            Pieces: pieces,
            SideToMove: ParseSeat(snapshot.SideToMove),
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: Math.Max(1, snapshot.MoveNumber),
            Status: ParseStatus(snapshot),
            MoveHistory: Array.Empty<Move>(),
            PositionHistory: null,
            SchemaVersion: 2);
    }

    private static IReadOnlyList<PiecePlacement> ParseBoard(IReadOnlyList<string> boardRows)
    {
        if (boardRows is null || boardRows.Count != 8)
        {
            throw new InvalidOperationException("Snapshot board must contain 8 rows.");
        }

        var pieces = new List<PiecePlacement>(32);

        for (var rowIndex = 0; rowIndex < boardRows.Count; rowIndex++)
        {
            var row = boardRows[rowIndex];
            if (string.IsNullOrWhiteSpace(row) || row.Length != 8)
            {
                throw new InvalidOperationException("Each snapshot board row must contain exactly 8 characters.");
            }

            for (var file = 0; file < row.Length; file++)
            {
                var symbol = row[file];
                if (symbol == '.')
                {
                    continue;
                }

                var color = char.IsUpper(symbol) ? PieceColor.White : PieceColor.Black;
                var type = ParsePieceType(char.ToLowerInvariant(symbol));
                var rank = 7 - rowIndex;
                pieces.Add(new PiecePlacement(new Square(file, rank), new Piece(type, color, HasMoved: false)));
            }
        }

        return pieces;
    }

    private static PieceColor ParseSeat(string seat)
    {
        return seat switch
        {
            OnlineMatchProtocolConstants.CreatorSeat => PieceColor.White,
            OnlineMatchProtocolConstants.JoinerSeat => PieceColor.Black,
            _ => throw new InvalidOperationException($"Unsupported seat value '{seat}'.")
        };
    }

    private static PieceType ParsePieceType(char symbol)
    {
        return symbol switch
        {
            'p' => PieceType.Pawn,
            'n' => PieceType.Knight,
            'b' => PieceType.Bishop,
            'r' => PieceType.Rook,
            'q' => PieceType.Queen,
            'k' => PieceType.King,
            _ => throw new InvalidOperationException($"Unsupported board symbol '{symbol}'.")
        };
    }

    private static GameStatus ParseStatus(OnlineMatchSnapshot snapshot)
    {
        if (!string.Equals(snapshot.Status, OnlineMatchProtocolConstants.MatchStatusEnded, StringComparison.Ordinal))
        {
            return GameStatus.InProgress;
        }

        if (string.Equals(snapshot.Resolution, OnlineMatchProtocolConstants.MatchResolutionDraw, StringComparison.Ordinal))
        {
            return GameStatus.Draw;
        }

        if (string.Equals(snapshot.Resolution, OnlineMatchProtocolConstants.MatchResolutionForfeit, StringComparison.Ordinal))
        {
            return snapshot.WinnerSeat switch
            {
                OnlineMatchProtocolConstants.CreatorSeat => GameStatus.WhiteWin,
                OnlineMatchProtocolConstants.JoinerSeat => GameStatus.BlackWin,
                _ => GameStatus.Draw
            };
        }

        return GameStatus.Draw;
    }
}
