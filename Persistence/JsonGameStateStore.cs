using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Persistence;

public sealed class JsonGameStateStore : IGameStateStore
{
    private const int SupportedSchemaVersion = 2;

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

        if (gameState.SchemaVersion != SupportedSchemaVersion)
        {
            throw CreateUnsupportedSchemaException(gameState.SchemaVersion);
        }

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

        await using var stream = File.OpenRead(filePath);
        var gameState = await JsonSerializer.DeserializeAsync<GameState>(stream, SerializerOptions, cancellationToken);

        if (gameState is null)
        {
            throw new InvalidDataException("Saved game data is invalid or empty.");
        }

        if (gameState.SchemaVersion != SupportedSchemaVersion)
        {
            throw CreateUnsupportedSchemaException(gameState.SchemaVersion);
        }

        return gameState;
    }

    private static InvalidDataException CreateUnsupportedSchemaException(int schemaVersion)
    {
        var message = schemaVersion < SupportedSchemaVersion
            ? $"Unsupported save schema version '{schemaVersion}'. Expected '{SupportedSchemaVersion}'. Legacy saves must be re-created with the current app version."
            : $"Unsupported save schema version '{schemaVersion}'. Expected '{SupportedSchemaVersion}'.";

        return new InvalidDataException(message);
    }
}
