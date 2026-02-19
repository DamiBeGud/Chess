namespace MultiplayerServer.Application.Matches;

public sealed class ClassicChessRulesEngine : IChessRulesEngine
{
    public MoveApplicationOutcome TryApplyMove(
        MatchState matchState,
        string seat,
        string from,
        string to,
        string? promotion)
    {
        var normalizedPromotion = string.IsNullOrWhiteSpace(promotion) ? null : promotion.Trim();
        if (!TryParsePromotionPiece(normalizedPromotion, out var promotionPiece))
        {
            return new MoveRejectedOutcome(MoveRejectionReason.InvalidPromotion);
        }

        if (!TryParseSquare(from, out var fromSquare) ||
            !TryParseSquare(to, out var toSquare))
        {
            return new MoveRejectedOutcome(MoveRejectionReason.InvalidCoordinates);
        }

        return TryApplyLegalMove(matchState, fromSquare, toSquare, seat, promotionPiece)
            ? new MoveAppliedOutcome()
            : new MoveRejectedOutcome(MoveRejectionReason.IllegalMove);
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
        MatchState matchState,
        BoardSquare fromSquare,
        BoardSquare toSquare,
        string seat,
        char? requestedPromotionPiece)
    {
        if (fromSquare.Equals(toSquare))
        {
            return false;
        }

        var isWhiteTurn = seat == MatchSeats.Creator;
        var board = (char[])matchState.Board.Clone();
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

        var whiteCanCastleKingSide = matchState.WhiteCanCastleKingSide;
        var whiteCanCastleQueenSide = matchState.WhiteCanCastleQueenSide;
        var blackCanCastleKingSide = matchState.BlackCanCastleKingSide;
        var blackCanCastleQueenSide = matchState.BlackCanCastleQueenSide;
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
                        if (!matchState.EnPassantTarget.HasValue || !matchState.EnPassantTarget.Value.Equals(toSquare))
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

        matchState.Board = board;
        matchState.WhiteCanCastleKingSide = whiteCanCastleKingSide;
        matchState.WhiteCanCastleQueenSide = whiteCanCastleQueenSide;
        matchState.BlackCanCastleKingSide = blackCanCastleKingSide;
        matchState.BlackCanCastleQueenSide = blackCanCastleQueenSide;
        matchState.EnPassantTarget = nextEnPassantTarget;
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
}
