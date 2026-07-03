using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SendEmailService.Common;
using SendEmailService.Core.Interfaces;
using SendEmailService.Core.Models;
using System.Data;

namespace SendEmailService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based implementation of <see cref="IServiceLogRepository"/> that writes
/// operational log entries to <c>tbl_EmailServiceLog</c> via a stored procedure.
/// </summary>
public sealed class ServiceLogRepository : IServiceLogRepository
{
    private readonly string _connectionString;

    /// <summary>Initialises a new instance of <see cref="ServiceLogRepository"/>.</summary>
    /// <param name="configuration">Application configuration (reads <c>ConnectionStrings:EmailDB</c>).</param>
    public ServiceLogRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("EmailDB")
            ?? throw new InvalidOperationException("Connection string 'EmailDB' is not configured.");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    /// <inheritdoc/>
    public async Task LogAsync(ServiceLogEntry entry, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@LogLevel", entry.LogLevel, DbType.String, size: 20);
        parameters.Add("@Message", entry.Message, DbType.String);
        parameters.Add("@ExceptionDetail", entry.ExceptionDetail, DbType.String);
        parameters.Add("@Sno", entry.Sno, DbType.Int32);
        parameters.Add("@MachineName", entry.MachineName, DbType.String, size: 100);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            Constants.StoredProcedures.InsertServiceLog,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
