using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SendEmailService.Common;
using SendEmailService.Core.Interfaces;
using SendEmailService.Core.Models;
using System.Data;

namespace SendEmailService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based implementation of <see cref="IMailQueueRepository"/> that communicates
/// with SQL Server via stored procedures.
/// </summary>
public sealed class MailQueueRepository : IMailQueueRepository
{
    private readonly string _connectionString;

    /// <summary>Initialises a new instance of <see cref="MailQueueRepository"/>.</summary>
    /// <param name="configuration">Application configuration (reads <c>ConnectionStrings:EmailDB</c>).</param>
    public MailQueueRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("EmailDB")
            ?? throw new InvalidOperationException("Connection string 'EmailDB' is not configured.");
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    /// <inheritdoc/>
    public async Task<IEnumerable<MailQueueItem>> GetPendingEmailsAsync(
        int batchSize,
        int maxRetry,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@BatchSize", batchSize, DbType.Int32);
        parameters.Add("@MaxRetry", maxRetry, DbType.Int32);
        parameters.Add("@InstanceId", instanceId, DbType.String, size: 100);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            Constants.StoredProcedures.GetPendingEmails,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        return await connection.QueryAsync<MailQueueItem>(command);
    }

    /// <inheritdoc/>
    public async Task UpdateEmailStatusAsync(
        int sno,
        string status,
        string? transmissionId,
        string? exceptionMsg,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@Sno", sno, DbType.Int32);
        parameters.Add("@Status", status, DbType.StringFixedLength, size: 1);
        parameters.Add("@TransmissionID", transmissionId, DbType.String, size: 100);
        parameters.Add("@ExceptionMsg", exceptionMsg, DbType.String);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            Constants.StoredProcedures.UpdateEmailStatus,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    /// <inheritdoc/>
    public async Task ArchiveEmailAsync(
        int sno,
        string finalStatus,
        string? exceptionMsg,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@Sno", sno, DbType.Int32);
        parameters.Add("@FinalStatus", finalStatus, DbType.StringFixedLength, size: 1);
        parameters.Add("@ExceptionMsg", exceptionMsg, DbType.String);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            Constants.StoredProcedures.ArchiveEmail,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    /// <inheritdoc/>
    public async Task<int> ResetStaleProcessingAsync(
        int staleMinutes,
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@StaleMinutes", staleMinutes, DbType.Int32);
        parameters.Add("@InstanceId", instanceId, DbType.String, size: 100);

        await using var connection = CreateConnection();
        var command = new CommandDefinition(
            Constants.StoredProcedures.ResetStaleProcessing,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<int>(command);
    }
}
