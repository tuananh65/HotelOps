using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public enum SchemaCompatibility
{
    Compatible,
    MissingRequiredSchema,
    DatabaseInaccessible,
    UnexpectedValidationError
}

/// <summary>Read-only deployment prerequisite, not a migration or a complete schema audit.</summary>
public sealed class StartupSchemaValidator(HotelOpsDbContext db)
{
    public async Task<SchemaCompatibility> ValidateAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        var connected = false;
        try
        {
            // Use raw ADO calls so EF does not log provider exceptions/configuration.
            // The scoped DbContext owns/disposes this connection, including on startup failure.
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(budget.Token);
            connected = true;
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 15;
            command.CommandText = """
                SELECT COUNT(*) FROM sys.columns AS c
                INNER JOIN sys.tables AS t ON t.object_id = c.object_id
                INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                WHERE s.name = N'dbo' AND t.name = N'users' AND c.name = N'row_version'
                  AND c.system_type_id = 189 AND c.max_length = 8
                  AND c.is_nullable = 0 AND c.is_computed = 0
                """;
            var count = (int)(await command.ExecuteScalarAsync(budget.Token))!;
            return count == 1 ? SchemaCompatibility.Compatible : SchemaCompatibility.MissingRequiredSchema;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return SchemaCompatibility.DatabaseInaccessible;
        }
        catch (SqlException exception) when (!connected || exception.Number is -2 or 233 or 10053 or 10054 or 10060)
        {
            return SchemaCompatibility.DatabaseInaccessible;
        }
        catch (Exception)
        {
            // Deliberately do not retain/log provider messages, credentials or connection strings.
            return SchemaCompatibility.UnexpectedValidationError;
        }
    }
}
