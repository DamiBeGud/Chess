using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Persistence;

public sealed class JsonGameStateStore : IGameStateStore
{
    private const int SupportedSchemaVersion = 2;
    private const CastlingRights SupportedCastlingRightsMask =
        CastlingRights.WhiteKingSide
        | CastlingRights.WhiteQueenSide
        | CastlingRights.BlackKingSide
        | CastlingRights.BlackQueenSide;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task SaveAsync(string filePath, GameState gameState, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidDataException("File path is required.");
        }

        ValidateGameState(gameState);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, gameState, SerializerOptions, cancellationToken);
    }

    public async Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidDataException("File path is required.");
        }

        await using Stream stream = OpenReadStream(filePath);
        GameState? gameState;
        try
        {
            gameState = await JsonSerializer.DeserializeAsync<GameState>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Saved game JSON is invalid or corrupted.", exception);
        }

        if (gameState is null)
        {
            throw new InvalidDataException("Saved game data is invalid or empty.");
        }

        ValidateGameState(gameState);

        return gameState;
    }

    private static FileStream OpenReadStream(string filePath)
    {
        try
        {
            return File.OpenRead(filePath);
        }
        catch (FileNotFoundException exception)
        {
            throw new InvalidDataException($"Save file '{filePath}' was not found.", exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new InvalidDataException($"Save file directory for '{filePath}' was not found.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidDataException($"Save file '{filePath}' cannot be accessed.", exception);
        }
    }

    private static void ValidateGameState(GameState gameState)
    {
        if (gameState.SchemaVersion != SupportedSchemaVersion)
        {
            throw CreateUnsupportedSchemaException(gameState.SchemaVersion);
        }

        if (gameState.HalfmoveClock < 0)
        {
            throw new InvalidDataException("Halfmove clock cannot be negative.");
        }

        if (gameState.FullmoveNumber < 1)
        {
            throw new InvalidDataException("Fullmove number must be at least 1.");
        }

        if (!Enum.IsDefined(typeof(PieceColor), gameState.SideToMove))
        {
            throw new InvalidDataException($"Invalid side to move value '{gameState.SideToMove}'.");
        }

        if (!Enum.IsDefined(typeof(GameStatus), gameState.Status))
        {
            throw new InvalidDataException($"Invalid game status value '{gameState.Status}'.");
        }

        if ((gameState.CastlingRights & ~SupportedCastlingRightsMask) != 0)
        {
            throw new InvalidDataException($"Invalid castling rights value '{gameState.CastlingRights}'.");
        }

        if (gameState.EnPassantTarget is { } enPassantTarget
            && enPassantTarget.Rank is not 2 and not 5)
        {
            throw new InvalidDataException(
                $"Invalid en passant target square '{enPassantTarget.File},{enPassantTarget.Rank}'.");
        }

        if (gameState.Pieces is null)
        {
            throw new InvalidDataException("Saved game is missing board pieces.");
        }

        if (gameState.MoveHistory is null)
        {
            throw new InvalidDataException("Saved game is missing move history.");
        }

        ValidatePieces(gameState.Pieces);
        ValidateMoveHistory(gameState.MoveHistory);
        ValidatePositionHistory(gameState.PositionHistory);
    }

    private static void ValidatePieces(IReadOnlyList<PiecePlacement> pieces)
    {
        var occupiedSquares = new HashSet<Square>();

        foreach (var piecePlacement in pieces)
        {
            if (piecePlacement is null)
            {
                throw new InvalidDataException("Saved game contains a null piece placement.");
            }

            if (piecePlacement.Piece is null)
            {
                throw new InvalidDataException(
                    $"Saved game contains a null piece at square '{piecePlacement.Square.File},{piecePlacement.Square.Rank}'.");
            }

            ValidatePiece(piecePlacement.Piece, "piece placement");

            if (!occupiedSquares.Add(piecePlacement.Square))
            {
                throw new InvalidDataException(
                    $"Multiple pieces were found on square '{piecePlacement.Square.File},{piecePlacement.Square.Rank}'.");
            }
        }
    }

    private static void ValidateMoveHistory(IReadOnlyList<Move> moveHistory)
    {
        foreach (var move in moveHistory)
        {
            if (move is null)
            {
                throw new InvalidDataException("Saved game contains a null move history entry.");
            }

            if (move.MovedPiece is null)
            {
                throw new InvalidDataException("Saved game contains a move with null moved piece metadata.");
            }

            ValidatePiece(move.MovedPiece, "move history");

            if (move.CapturedPiece is not null)
            {
                ValidatePiece(move.CapturedPiece, "captured piece");
            }

            if (move.PromotionPieceType is not null
                && !Enum.IsDefined(typeof(PieceType), move.PromotionPieceType.Value))
            {
                throw new InvalidDataException($"Invalid promotion piece type '{move.PromotionPieceType}'.");
            }
        }
    }

    private static void ValidatePositionHistory(IReadOnlyList<string>? positionHistory)
    {
        if (positionHistory is null)
        {
            return;
        }

        for (var index = 0; index < positionHistory.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(positionHistory[index]))
            {
                throw new InvalidDataException($"Position history entry '{index}' is empty.");
            }
        }
    }

    private static void ValidatePiece(Piece piece, string context)
    {
        if (piece is null)
        {
            throw new InvalidDataException($"Null piece value found in {context}.");
        }

        if (!Enum.IsDefined(typeof(PieceType), piece.Type))
        {
            throw new InvalidDataException($"Invalid piece type '{piece.Type}' in {context}.");
        }

        if (!Enum.IsDefined(typeof(PieceColor), piece.Color))
        {
            throw new InvalidDataException($"Invalid piece color '{piece.Color}' in {context}.");
        }
    }

    private static InvalidDataException CreateUnsupportedSchemaException(int schemaVersion)
    {
        var message = schemaVersion < SupportedSchemaVersion
            ? $"Unsupported save schema version '{schemaVersion}'. Expected '{SupportedSchemaVersion}'. Legacy saves must be re-created with the current app version."
            : $"Unsupported save schema version '{schemaVersion}'. Expected '{SupportedSchemaVersion}'.";

        return new InvalidDataException(message);
    }
}
