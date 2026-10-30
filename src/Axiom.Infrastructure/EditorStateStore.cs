using Axiom.Core;
using Microsoft.Data.Sqlite;

namespace Axiom.Infrastructure;





/// <summary>Only editor recovery/session and existing document identity bookkeeping. No classification or semantic tables.</summary>
public sealed class EditorStateStore(AppDataPaths paths) : IRecoveryStore, IDocumentMetadataStore
{
    private string ConnectionString
    {
        get
        {
            return new SqliteConnectionStringBuilder
                   {
                       DataSource = paths.DatabasePath
                     , Pooling    = false
                   }.ToString();
        }
    }

    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();
        await ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS Sessions (Id TEXT PRIMARY KEY, Started TEXT, Clean INTEGER DEFAULT 0);
            CREATE TABLE IF NOT EXISTS Drafts (DraftKey TEXT PRIMARY KEY, FilePath TEXT, TextContent TEXT NOT NULL, Updated TEXT, SavedWrite TEXT, IsNew INTEGER);
            CREATE TABLE IF NOT EXISTS Identities (DocumentId TEXT, FilePath TEXT, PRIMARY KEY(DocumentId, FilePath));
            """
                         , cancellationToken);
    }

    public async Task<string> BeginSessionAsync(CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("N");
        await ExecuteAsync("INSERT INTO Sessions(Id, Started) VALUES($id, $started)"
                         , cancellationToken
                         , ("$id", id)
                         , ("$started", DateTimeOffset.UtcNow.ToString("O")));
        return id;
    }

    public Task EndSessionAsync(string            sessionId
                              , bool              cleanExit
                              , CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("UPDATE Sessions SET Clean=$clean WHERE Id=$id"
                          , cancellationToken
                          , ("$clean", cleanExit ? 1 : 0)
                          , ("$id", sessionId));
    }

    public Task SaveDraftAsync(RecoveryDraft     draft
                             , CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("INSERT OR REPLACE INTO Drafts VALUES($key,$path,$text,$updated,$saved,$new)"
                          , cancellationToken
                          , ("$key", draft.DraftKey)
                          , ("$path", draft.FilePath)
                          , ("$text", draft.Text)
                          , ("$updated", draft.UpdatedUtc.ToString("O"))
                          , ("$saved", draft.SavedFileWriteUtc?.ToString("O"))
                          , ("$new", draft.IsNewDocument ? 1 : 0));
    }

    public Task DeleteDraftAsync(string            draftKey
                               , CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("DELETE FROM Drafts WHERE DraftKey=$key", cancellationToken, ("$key", draftKey));
    }

    public async Task<IReadOnlyList<RecoveryDraft>> GetRecoverableDraftsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Drafts ORDER BY Updated";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<RecoveryDraft>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new(reader.GetString(0)
                              , reader.IsDBNull(1) ? null : reader.GetString(1)
                              , reader.GetString(2)
                              , DateTimeOffset.Parse(reader.GetString(3))
                              , reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4))
                              , reader.GetInt32(5) != 0));
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task RecordIdentityAsync(Guid              documentId
                                  , string            path
                                  , CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("INSERT OR REPLACE INTO Identities VALUES($id,$path)"
                          , cancellationToken
                          , ("$id", documentId.ToString())
                          , ("$path", path));
    }

    public Task RemovePathAsync(string            path
                              , CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("DELETE FROM Identities WHERE FilePath=$path", cancellationToken, ("$path", path));
    }

    public async Task<IReadOnlyList<DocumentIdentityOccurrence>> GetIdentitiesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT DocumentId,FilePath FROM Identities";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<DocumentIdentityOccurrence>();
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1)));
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ExecuteAsync(       string                         sql
                                  ,        CancellationToken              cancellation
                                  , params (string Name, object? Value)[] parameters)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync(cancellation);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
            }

            await command.ExecuteNonQueryAsync(cancellation);
        }
        finally
        {
            _gate.Release();
        }
    }
}
