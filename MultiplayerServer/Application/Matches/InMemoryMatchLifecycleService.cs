using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Application.Matches;

public sealed class InMemoryMatchLifecycleService : IMatchLifecycleService
{
    private const int JoinCodeLength = 6;
    private static readonly char[] JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    private readonly object _sync = new();
    private readonly Dictionary<string, MatchRecord> _matchesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _matchIdByJoinCode = new(StringComparer.Ordinal);
    private readonly ILogger<InMemoryMatchLifecycleService> _logger;

    public InMemoryMatchLifecycleService(ILogger<InMemoryMatchLifecycleService>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemoryMatchLifecycleService>.Instance;
    }

    public CreateMatchResponse CreateMatch()
    {
        lock (_sync)
        {
            var matchId = Guid.NewGuid().ToString("N");
            var joinCode = GenerateUniqueJoinCode();
            var creatorToken = GenerateToken();

            _matchesById[matchId] = new MatchRecord
            {
                MatchId = matchId,
                JoinCode = joinCode,
                CreatorToken = creatorToken,
                SideToMove = MatchProtocolConstants.CreatorSeat,
                MoveNumber = 1,
                Board = CreateInitialBoard(),
                WhiteCanCastleKingSide = true,
                WhiteCanCastleQueenSide = true,
                BlackCanCastleKingSide = true,
                BlackCanCastleQueenSide = true,
                EnPassantTarget = null
            };

            _matchIdByJoinCode[joinCode] = matchId;
            _logger.LogInformation("Match created {MatchId}", matchId);

            return new CreateMatchResponse(matchId, joinCode, creatorToken);
        }
    }

    public JoinMatchOutcome JoinMatch(string? joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            _logger.LogWarning(
                "Join rejected with {ErrorCode}",
                MatchProtocolConstants.ErrorJoinCodeRequired);
            return new JoinMatchFailed(
                new JoinMatchFailure(
                    MatchProtocolConstants.ErrorJoinCodeRequired,
                    "joinCode is required."));
        }

        var normalizedJoinCode = joinCode.Trim().ToUpperInvariant();

        lock (_sync)
        {
            if (!_matchIdByJoinCode.TryGetValue(normalizedJoinCode, out var matchId) ||
                !_matchesById.TryGetValue(matchId, out var match))
            {
                _logger.LogWarning(
                    "Join rejected with {ErrorCode}",
                    MatchProtocolConstants.ErrorMatchNotFound);
                return new JoinMatchFailed(
                    new JoinMatchFailure(
                        MatchProtocolConstants.ErrorMatchNotFound,
                        "Match was not found."));
            }

            if (match.JoinerToken is not null)
            {
                _logger.LogWarning(
                    "Join rejected for {MatchId} with {ErrorCode}",
                    match.MatchId,
                    MatchProtocolConstants.ErrorMatchFull);
                return new JoinMatchFailed(
                    new JoinMatchFailure(
                        MatchProtocolConstants.ErrorMatchFull,
                        "Match already has two players."));
            }

            var joinerToken = GenerateToken();
            match.JoinerToken = joinerToken;
            _logger.LogInformation(
                "Join accepted for {MatchId}; assigned {Seat}",
                match.MatchId,
                MatchProtocolConstants.JoinerSeat);

            // Deterministic seat assignment (MS-002): creator is White, first joiner is Black.
            return new JoinMatchSucceeded(
                new JoinMatchResponse(match.MatchId, MatchProtocolConstants.JoinerSeat, joinerToken));
        }
    }

    public SubmitMoveOutcome SubmitMove(string? matchId, string? playerToken, string? from, string? to, string? promotion)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            return new SubmitMoveFailed(
                new SubmitMoveFailure(
                    MatchProtocolConstants.ErrorMatchIdRequired,
                    "matchId is required."));
        }

        if (string.IsNullOrWhiteSpace(playerToken))
        {
            return new SubmitMoveFailed(
                new SubmitMoveFailure(
                    MatchProtocolConstants.ErrorPlayerTokenRequired,
                    "playerToken is required."));
        }

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return new SubmitMoveFailed(
                new SubmitMoveFailure(
                    MatchProtocolConstants.ErrorMoveCoordinatesRequired,
                    "from and to are required."));
        }

        var normalizedMatchId = matchId.Trim();
        var normalizedPlayerToken = playerToken.Trim();
        var normalizedFrom = from.Trim().ToLowerInvariant();
        var normalizedTo = to.Trim().ToLowerInvariant();
        var normalizedPromotion = string.IsNullOrWhiteSpace(promotion) ? null : promotion.Trim();
        if (!TryParsePromotionPiece(normalizedPromotion, out var promotionPiece))
        {
            _logger.LogWarning(
                "Move rejected with {ErrorCode}; invalid promotion value {Promotion}",
                MatchProtocolConstants.ErrorInvalidPromotion,
                normalizedPromotion);
            return new SubmitMoveFailed(
                new SubmitMoveFailure(
                    MatchProtocolConstants.ErrorInvalidPromotion,
                    "promotion must be one of Q, R, B, or N."));
        }

        lock (_sync)
        {
            if (!_matchesById.TryGetValue(normalizedMatchId, out var match))
            {
                _logger.LogWarning(
                    "Move rejected with {ErrorCode}; match {MatchId} not found",
                    MatchProtocolConstants.ErrorMatchNotFound,
                    normalizedMatchId);
                return new SubmitMoveFailed(
                    new SubmitMoveFailure(
                        MatchProtocolConstants.ErrorMatchNotFound,
                        "Match was not found."));
            }

            if (match.JoinerToken is null)
            {
                _logger.LogWarning(
                    "Move rejected for {MatchId} with {ErrorCode}",
                    match.MatchId,
                    MatchProtocolConstants.ErrorMatchNotReady);
                return new SubmitMoveFailed(
                    new SubmitMoveFailure(
                        MatchProtocolConstants.ErrorMatchNotReady,
                        "Match is waiting for the second player."));
            }

            var seat = ResolveSeat(match, normalizedPlayerToken);
            if (seat is null)
            {
                _logger.LogWarning(
                    "Move rejected for {MatchId} with {ErrorCode}",
                    match.MatchId,
                    MatchProtocolConstants.ErrorInvalidPlayerToken);
                return new SubmitMoveFailed(
                    new SubmitMoveFailure(
                        MatchProtocolConstants.ErrorInvalidPlayerToken,
                        "playerToken is not valid for this match."));
            }

            if (!string.Equals(match.SideToMove, seat, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Move rejected for {MatchId} with {ErrorCode}; expected {ExpectedSeat}, got {Seat}",
                    match.MatchId,
                    MatchProtocolConstants.ErrorOutOfTurn,
                    match.SideToMove,
                    seat);
                return new SubmitMoveFailed(
                    new SubmitMoveFailure(
                        MatchProtocolConstants.ErrorOutOfTurn,
                        "It is not this player's turn."));
            }

            if (!TryParseSquare(normalizedFrom, out var fromSquare) ||
                !TryParseSquare(normalizedTo, out var toSquare))
            {
                return new SubmitMoveFailed(
                    new SubmitMoveFailure(
                        MatchProtocolConstants.ErrorIllegalMove,
                        "Move format is invalid. Use coordinates like e2 and e4."));
            }

            if (!TryApplyLegalMove(match, fromSquare, toSquare, seat, promotionPiece))
            {
                _logger.LogWarning(
                    "Move rejected for {MatchId} with {ErrorCode}; {From}->{To}",
                    match.MatchId,
                    MatchProtocolConstants.ErrorIllegalMove,
                    normalizedFrom,
                    normalizedTo);
                return new SubmitMoveFailed(
                    new SubmitMoveFailure(
                        MatchProtocolConstants.ErrorIllegalMove,
                        "Move is not legal."));
            }

            match.SideToMove = seat == MatchProtocolConstants.CreatorSeat
                ? MatchProtocolConstants.JoinerSeat
                : MatchProtocolConstants.CreatorSeat;
            match.MoveNumber += 1;

            _logger.LogInformation(
                "Move accepted for {MatchId}; {From}->{To}; next turn {SideToMove}",
                match.MatchId,
                normalizedFrom,
                normalizedTo,
                match.SideToMove);

            var snapshot = BuildSnapshot(match);
            return new SubmitMoveSucceeded(new SubmitMoveResponse(true, snapshot));
        }
    }

    private string GenerateUniqueJoinCode()
    {
        while (true)
        {
            var candidate = GenerateJoinCode();
            if (_matchIdByJoinCode.ContainsKey(candidate))
            {
                continue;
            }

            return candidate;
        }
    }

    private static string GenerateJoinCode()
    {
        Span<char> buffer = stackalloc char[JoinCodeLength];
        Span<byte> randomBytes = stackalloc byte[JoinCodeLength];
        RandomNumberGenerator.Fill(randomBytes);

        for (var i = 0; i < JoinCodeLength; i++)
        {
            buffer[i] = JoinCodeAlphabet[randomBytes[i] % JoinCodeAlphabet.Length];
        }

        return new string(buffer);
    }

    private static string GenerateToken()
    {
        Span<byte> tokenBytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToHexString(tokenBytes).ToLowerInvariant();
    }

    private static MatchSnapshotResponse BuildSnapshot(MatchRecord match)
    {
        var boardRows = new string[8];
        for (var row = 0; row < 8; row++)
        {
            var rowChars = new char[8];
            for (var col = 0; col < 8; col++)
            {
                rowChars[col] = match.Board[(row * 8) + col];
            }

            boardRows[row] = new string(rowChars);
        }

        return new MatchSnapshotResponse(match.MatchId, match.SideToMove, match.MoveNumber, boardRows);
    }

    private static string? ResolveSeat(MatchRecord match, string playerToken)
    {
        if (string.Equals(match.CreatorToken, playerToken, StringComparison.OrdinalIgnoreCase))
        {
            return MatchProtocolConstants.CreatorSeat;
        }

        if (match.JoinerToken is not null &&
            string.Equals(match.JoinerToken, playerToken, StringComparison.OrdinalIgnoreCase))
        {
            return MatchProtocolConstants.JoinerSeat;
        }

        return null;
    }

    private static bool TryParseSquare(string value, out BoardSquare square)
    {
        square = default;
        if (value.Length != 2)
        {
            return false;
        }

        var fileChar = char.ToLowerInvariant(value[0]);
        var rankChar = value[1];

        if (fileChar is < 'a' or > 'h' || rankChar is < '1' or > '8')
        {
            return false;
        }

        var col = fileChar - 'a';
        var row = '8' - rankChar;
        square = new BoardSquare(row, col);
        return true;
    }

    private static bool TryApplyLegalMove(
        MatchRecord match,
        BoardSquare fromSquare,
        BoardSquare toSquare,
        string seat,
        char? requestedPromotionPiece)
    {
        if (fromSquare.Equals(toSquare))
        {
            return false;
        }

        var isWhiteTurn = seat == MatchProtocolConstants.CreatorSeat;
        var board = (char[])match.Board.Clone();
        var movingPiece = GetPiece(board, fromSquare);
        if (movingPiece == '.' || IsWhitePiece(movingPiece) != isWhiteTurn)
        {
            return false;
        }

        var targetPiece = GetPiece(board, toSquare);
        if (targetPiece != '.' && IsWhitePiece(targetPiece) == isWhiteTurn)
        {
            return false;
        }

        var whiteCanCastleKingSide = match.WhiteCanCastleKingSide;
        var whiteCanCastleQueenSide = match.WhiteCanCastleQueenSide;
        var blackCanCastleKingSide = match.BlackCanCastleKingSide;
        var blackCanCastleQueenSide = match.BlackCanCastleQueenSide;
        BoardSquare? nextEnPassantTarget = null;

        var pieceType = char.ToUpperInvariant(movingPiece);
        var deltaRow = toSquare.Row - fromSquare.Row;
        var deltaCol = toSquare.Col - fromSquare.Col;
        var absRow = Math.Abs(deltaRow);
        var absCol = Math.Abs(deltaCol);

        var isCastlingMove = false;
        var rookFromSquare = default(BoardSquare);
        var rookToSquare = default(BoardSquare);

        var isEnPassantCapture = false;
        var enPassantCapturedSquare = default(BoardSquare);
        var movingPieceAfterMove = movingPiece;

        switch (pieceType)
        {
            case 'P':
            {
                var direction = isWhiteTurn ? -1 : 1;
                var startRow = isWhiteTurn ? 6 : 1;

                if (deltaCol == 0)
                {
                    if (deltaRow == direction && targetPiece == '.')
                    {
                        // Single pawn advance.
                    }
                    else if (deltaRow == (2 * direction) && fromSquare.Row == startRow && targetPiece == '.')
                    {
                        var intermediateSquare = new BoardSquare(fromSquare.Row + direction, fromSquare.Col);
                        if (GetPiece(board, intermediateSquare) != '.')
                        {
                            return false;
                        }

                        nextEnPassantTarget = intermediateSquare;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (absCol == 1 && deltaRow == direction)
                {
                    if (targetPiece != '.')
                    {
                        if (IsWhitePiece(targetPiece) == isWhiteTurn)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!match.EnPassantTarget.HasValue || !match.EnPassantTarget.Value.Equals(toSquare))
                        {
                            return false;
                        }

                        enPassantCapturedSquare = new BoardSquare(toSquare.Row + (isWhiteTurn ? 1 : -1), toSquare.Col);
                        var capturedPawn = GetPiece(board, enPassantCapturedSquare);
                        if (capturedPawn != (isWhiteTurn ? 'p' : 'P'))
                        {
                            return false;
                        }

                        isEnPassantCapture = true;
                    }
                }
                else
                {
                    return false;
                }

                if (toSquare.Row is 0 or 7)
                {
                    var promotionPiece = requestedPromotionPiece ?? 'Q';
                    movingPieceAfterMove = isWhiteTurn
                        ? promotionPiece
                        : char.ToLowerInvariant(promotionPiece);
                }

                break;
            }
            case 'N':
                if (!((absRow == 2 && absCol == 1) || (absRow == 1 && absCol == 2)))
                {
                    return false;
                }

                break;
            case 'B':
                if (absRow != absCol || !IsPathClear(board, fromSquare, toSquare))
                {
                    return false;
                }

                break;
            case 'R':
                if ((deltaRow != 0 && deltaCol != 0) || !IsPathClear(board, fromSquare, toSquare))
                {
                    return false;
                }

                break;
            case 'Q':
                if (!(absRow == absCol || deltaRow == 0 || deltaCol == 0) ||
                    !IsPathClear(board, fromSquare, toSquare))
                {
                    return false;
                }

                break;
            case 'K':
                if (absRow <= 1 && absCol <= 1)
                {
                    // Regular king move.
                }
                else
                {
                    if (!TryValidateCastling(
                            board,
                            fromSquare,
                            toSquare,
                            isWhiteTurn,
                            whiteCanCastleKingSide,
                            whiteCanCastleQueenSide,
                            blackCanCastleKingSide,
                            blackCanCastleQueenSide,
                            out rookFromSquare,
                            out rookToSquare))
                    {
                        return false;
                    }

                    isCastlingMove = true;
                }

                break;
            default:
                return false;
        }

        board[(fromSquare.Row * 8) + fromSquare.Col] = '.';
        if (isEnPassantCapture)
        {
            board[(enPassantCapturedSquare.Row * 8) + enPassantCapturedSquare.Col] = '.';
        }

        if (isCastlingMove)
        {
            var rookPiece = board[(rookFromSquare.Row * 8) + rookFromSquare.Col];
            board[(rookFromSquare.Row * 8) + rookFromSquare.Col] = '.';
            board[(rookToSquare.Row * 8) + rookToSquare.Col] = rookPiece;
        }

        board[(toSquare.Row * 8) + toSquare.Col] = movingPieceAfterMove;

        if (pieceType == 'K')
        {
            if (isWhiteTurn)
            {
                whiteCanCastleKingSide = false;
                whiteCanCastleQueenSide = false;
            }
            else
            {
                blackCanCastleKingSide = false;
                blackCanCastleQueenSide = false;
            }
        }

        if (pieceType == 'R')
        {
            if (fromSquare.Equals(new BoardSquare(7, 0)))
            {
                whiteCanCastleQueenSide = false;
            }
            else if (fromSquare.Equals(new BoardSquare(7, 7)))
            {
                whiteCanCastleKingSide = false;
            }
            else if (fromSquare.Equals(new BoardSquare(0, 0)))
            {
                blackCanCastleQueenSide = false;
            }
            else if (fromSquare.Equals(new BoardSquare(0, 7)))
            {
                blackCanCastleKingSide = false;
            }
        }

        if (targetPiece == 'R')
        {
            if (toSquare.Equals(new BoardSquare(7, 0)))
            {
                whiteCanCastleQueenSide = false;
            }
            else if (toSquare.Equals(new BoardSquare(7, 7)))
            {
                whiteCanCastleKingSide = false;
            }
        }
        else if (targetPiece == 'r')
        {
            if (toSquare.Equals(new BoardSquare(0, 0)))
            {
                blackCanCastleQueenSide = false;
            }
            else if (toSquare.Equals(new BoardSquare(0, 7)))
            {
                blackCanCastleKingSide = false;
            }
        }

        if (!ContainsKing(board, true) || !ContainsKing(board, false))
        {
            return false;
        }

        if (IsKingInCheck(board, isWhiteTurn))
        {
            return false;
        }

        match.Board = board;
        match.WhiteCanCastleKingSide = whiteCanCastleKingSide;
        match.WhiteCanCastleQueenSide = whiteCanCastleQueenSide;
        match.BlackCanCastleKingSide = blackCanCastleKingSide;
        match.BlackCanCastleQueenSide = blackCanCastleQueenSide;
        match.EnPassantTarget = nextEnPassantTarget;
        return true;
    }

    private static bool TryValidateCastling(
        char[] board,
        BoardSquare fromSquare,
        BoardSquare toSquare,
        bool isWhiteTurn,
        bool whiteCanCastleKingSide,
        bool whiteCanCastleQueenSide,
        bool blackCanCastleKingSide,
        bool blackCanCastleQueenSide,
        out BoardSquare rookFromSquare,
        out BoardSquare rookToSquare)
    {
        rookFromSquare = default;
        rookToSquare = default;

        if (isWhiteTurn)
        {
            if (!fromSquare.Equals(new BoardSquare(7, 4)))
            {
                return false;
            }

            if (toSquare.Equals(new BoardSquare(7, 6)))
            {
                if (!whiteCanCastleKingSide ||
                    GetPiece(board, new BoardSquare(7, 5)) != '.' ||
                    GetPiece(board, new BoardSquare(7, 6)) != '.' ||
                    GetPiece(board, new BoardSquare(7, 7)) != 'R')
                {
                    return false;
                }

                if (IsKingInCheck(board, true) ||
                    IsSquareAttacked(board, new BoardSquare(7, 5), byWhite: false) ||
                    IsSquareAttacked(board, new BoardSquare(7, 6), byWhite: false))
                {
                    return false;
                }

                rookFromSquare = new BoardSquare(7, 7);
                rookToSquare = new BoardSquare(7, 5);
                return true;
            }

            if (toSquare.Equals(new BoardSquare(7, 2)))
            {
                if (!whiteCanCastleQueenSide ||
                    GetPiece(board, new BoardSquare(7, 1)) != '.' ||
                    GetPiece(board, new BoardSquare(7, 2)) != '.' ||
                    GetPiece(board, new BoardSquare(7, 3)) != '.' ||
                    GetPiece(board, new BoardSquare(7, 0)) != 'R')
                {
                    return false;
                }

                if (IsKingInCheck(board, true) ||
                    IsSquareAttacked(board, new BoardSquare(7, 3), byWhite: false) ||
                    IsSquareAttacked(board, new BoardSquare(7, 2), byWhite: false))
                {
                    return false;
                }

                rookFromSquare = new BoardSquare(7, 0);
                rookToSquare = new BoardSquare(7, 3);
                return true;
            }

            return false;
        }

        if (!fromSquare.Equals(new BoardSquare(0, 4)))
        {
            return false;
        }

        if (toSquare.Equals(new BoardSquare(0, 6)))
        {
            if (!blackCanCastleKingSide ||
                GetPiece(board, new BoardSquare(0, 5)) != '.' ||
                GetPiece(board, new BoardSquare(0, 6)) != '.' ||
                GetPiece(board, new BoardSquare(0, 7)) != 'r')
            {
                return false;
            }

            if (IsKingInCheck(board, false) ||
                IsSquareAttacked(board, new BoardSquare(0, 5), byWhite: true) ||
                IsSquareAttacked(board, new BoardSquare(0, 6), byWhite: true))
            {
                return false;
            }

            rookFromSquare = new BoardSquare(0, 7);
            rookToSquare = new BoardSquare(0, 5);
            return true;
        }

        if (toSquare.Equals(new BoardSquare(0, 2)))
        {
            if (!blackCanCastleQueenSide ||
                GetPiece(board, new BoardSquare(0, 1)) != '.' ||
                GetPiece(board, new BoardSquare(0, 2)) != '.' ||
                GetPiece(board, new BoardSquare(0, 3)) != '.' ||
                GetPiece(board, new BoardSquare(0, 0)) != 'r')
            {
                return false;
            }

            if (IsKingInCheck(board, false) ||
                IsSquareAttacked(board, new BoardSquare(0, 3), byWhite: true) ||
                IsSquareAttacked(board, new BoardSquare(0, 2), byWhite: true))
            {
                return false;
            }

            rookFromSquare = new BoardSquare(0, 0);
            rookToSquare = new BoardSquare(0, 3);
            return true;
        }

        return false;
    }

    private static bool IsKingInCheck(char[] board, bool isWhiteKing)
    {
        var kingPiece = isWhiteKing ? 'K' : 'k';
        for (var i = 0; i < board.Length; i++)
        {
            if (board[i] != kingPiece)
            {
                continue;
            }

            var kingSquare = new BoardSquare(i / 8, i % 8);
            return IsSquareAttacked(board, kingSquare, byWhite: !isWhiteKing);
        }

        return true;
    }

    private static bool ContainsKing(char[] board, bool isWhiteKing)
    {
        var kingPiece = isWhiteKing ? 'K' : 'k';
        for (var i = 0; i < board.Length; i++)
        {
            if (board[i] == kingPiece)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParsePromotionPiece(string? value, out char? promotionPiece)
    {
        promotionPiece = null;
        if (value is null)
        {
            return true;
        }

        if (value.Length != 1)
        {
            return false;
        }

        var pieceType = char.ToUpperInvariant(value[0]);
        if (pieceType is not ('Q' or 'R' or 'B' or 'N'))
        {
            return false;
        }

        promotionPiece = pieceType;
        return true;
    }

    private static bool IsSquareAttacked(char[] board, BoardSquare square, bool byWhite)
    {
        var pawnRow = square.Row + (byWhite ? 1 : -1);
        var pawnPiece = byWhite ? 'P' : 'p';
        if (IsInBounds(pawnRow, square.Col - 1) && board[(pawnRow * 8) + (square.Col - 1)] == pawnPiece)
        {
            return true;
        }

        if (IsInBounds(pawnRow, square.Col + 1) && board[(pawnRow * 8) + (square.Col + 1)] == pawnPiece)
        {
            return true;
        }

        var knightPiece = byWhite ? 'N' : 'n';
        var knightOffsets = new (int Row, int Col)[]
        {
            (-2, -1), (-2, 1), (-1, -2), (-1, 2),
            (1, -2), (1, 2), (2, -1), (2, 1)
        };
        foreach (var (rowOffset, colOffset) in knightOffsets)
        {
            var row = square.Row + rowOffset;
            var col = square.Col + colOffset;
            if (IsInBounds(row, col) && board[(row * 8) + col] == knightPiece)
            {
                return true;
            }
        }

        if (IsAttackedOnRay(board, square, byWhite, -1, -1, 'B', 'Q') ||
            IsAttackedOnRay(board, square, byWhite, -1, 1, 'B', 'Q') ||
            IsAttackedOnRay(board, square, byWhite, 1, -1, 'B', 'Q') ||
            IsAttackedOnRay(board, square, byWhite, 1, 1, 'B', 'Q') ||
            IsAttackedOnRay(board, square, byWhite, -1, 0, 'R', 'Q') ||
            IsAttackedOnRay(board, square, byWhite, 1, 0, 'R', 'Q') ||
            IsAttackedOnRay(board, square, byWhite, 0, -1, 'R', 'Q') ||
            IsAttackedOnRay(board, square, byWhite, 0, 1, 'R', 'Q'))
        {
            return true;
        }

        var kingPiece = byWhite ? 'K' : 'k';
        for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
        {
            for (var colOffset = -1; colOffset <= 1; colOffset++)
            {
                if (rowOffset == 0 && colOffset == 0)
                {
                    continue;
                }

                var row = square.Row + rowOffset;
                var col = square.Col + colOffset;
                if (IsInBounds(row, col) && board[(row * 8) + col] == kingPiece)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAttackedOnRay(
        char[] board,
        BoardSquare fromSquare,
        bool byWhite,
        int rowStep,
        int colStep,
        char primaryPieceType,
        char secondaryPieceType)
    {
        var row = fromSquare.Row + rowStep;
        var col = fromSquare.Col + colStep;
        while (IsInBounds(row, col))
        {
            var piece = board[(row * 8) + col];
            if (piece == '.')
            {
                row += rowStep;
                col += colStep;
                continue;
            }

            if (IsWhitePiece(piece) != byWhite)
            {
                return false;
            }

            var pieceType = char.ToUpperInvariant(piece);
            return pieceType == primaryPieceType || pieceType == secondaryPieceType;
        }

        return false;
    }

    private static bool IsPathClear(char[] board, BoardSquare fromSquare, BoardSquare toSquare)
    {
        var rowStep = Math.Sign(toSquare.Row - fromSquare.Row);
        var colStep = Math.Sign(toSquare.Col - fromSquare.Col);

        var currentRow = fromSquare.Row + rowStep;
        var currentCol = fromSquare.Col + colStep;

        while (currentRow != toSquare.Row || currentCol != toSquare.Col)
        {
            if (board[(currentRow * 8) + currentCol] != '.')
            {
                return false;
            }

            currentRow += rowStep;
            currentCol += colStep;
        }

        return true;
    }

    private static bool IsWhitePiece(char piece)
    {
        return char.IsUpper(piece);
    }

    private static bool IsInBounds(int row, int col)
    {
        return row >= 0 && row < 8 && col >= 0 && col < 8;
    }

    private static char GetPiece(char[] board, BoardSquare square)
    {
        return board[(square.Row * 8) + square.Col];
    }

    private static char[] CreateInitialBoard()
    {
        var boardRows = new[]
        {
            "rnbqkbnr",
            "pppppppp",
            "........",
            "........",
            "........",
            "........",
            "PPPPPPPP",
            "RNBQKBNR"
        };

        var board = new char[64];
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                board[(row * 8) + col] = boardRows[row][col];
            }
        }

        return board;
    }

    private sealed class MatchRecord
    {
        public required string MatchId { get; init; }
        public required string JoinCode { get; init; }
        public required string CreatorToken { get; init; }
        public string? JoinerToken { get; set; }
        public required char[] Board { get; set; }
        public required string SideToMove { get; set; }
        public required int MoveNumber { get; set; }
        public required bool WhiteCanCastleKingSide { get; set; }
        public required bool WhiteCanCastleQueenSide { get; set; }
        public required bool BlackCanCastleKingSide { get; set; }
        public required bool BlackCanCastleQueenSide { get; set; }
        public required BoardSquare? EnPassantTarget { get; set; }
    }

    private readonly record struct BoardSquare(int Row, int Col);
}
