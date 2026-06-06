using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnimeUP.NativeHost.Interfaces;
using Microsoft.Data.Sqlite;

namespace AnimeUP.NativeHost.Services
{
    /// <summary>
    /// ILogService'in SQLite tabanlı gerçeklemesi.
    ///
    /// Tasarım Kararları:
    ///  - SemaphoreSlim(1,1) ile yazma işlemleri sıralanır;
    ///    SQLite'ın tek yazıcı kısıtını ihlal etmeden eşzamanlı çağrı güvenliği sağlanır.
    ///  - Bağlantı her çağrıda açılıp kapatılır (connection pooling SQLite'da gereksiz).
    ///  - Parametreli sorgular SQL injection'a karşı koruma sağlar.
    ///  - Tüm public metotlar async Task döner; çağıran iş parçacığını bloklamaz.
    /// </summary>
    public sealed class LogManager : ILogService
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public LogManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ── ILogService: Endpoint Loglama ────────────────────────────────

        public async Task LogEndpointAsync(
            string traceId,
            string action,
            string? requestPayload,
            string? responsePayload,
            int    durationMs,
            string status = "Success")
        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync().ConfigureAwait(false);

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO endpoint_logs
                        (trace_id, action, req_payload, res_payload, duration_ms, status)
                    VALUES
                        ($traceId, $action, $req, $res, $duration, $status);
                    """;

                cmd.Parameters.AddWithValue("$traceId",  traceId);
                cmd.Parameters.AddWithValue("$action",   action);
                cmd.Parameters.AddWithValue("$req",      (object?)requestPayload  ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$res",      (object?)responsePayload ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$duration", durationMs);
                cmd.Parameters.AddWithValue("$status",   status);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // ── ILogService: Fonksiyon Loglama ───────────────────────────────

        public async Task LogFunctionAsync(
            string  traceId,
            string? inputArgs,
            string? outputData,
            string? exceptionMessage,
            string? stackTrace,
            int     durationMs,
            [CallerMemberName] string methodName = "",
            [CallerFilePath]   string filePath   = "",
            [CallerLineNumber] int    lineNumber  = 0)
        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync().ConfigureAwait(false);

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO function_logs
                        (trace_id, method_name, file_path, line_number,
                         input_args, output_data, error_msg, stack_trace, duration_ms)
                    VALUES
                        ($traceId, $method, $file, $line,
                         $input, $output, $error, $stack, $duration);
                    """;

                cmd.Parameters.AddWithValue("$traceId",  traceId);
                cmd.Parameters.AddWithValue("$method",   methodName);
                cmd.Parameters.AddWithValue("$file",     System.IO.Path.GetFileName(filePath)); // Tam yol değil, sadece dosya adı
                cmd.Parameters.AddWithValue("$line",     lineNumber);
                cmd.Parameters.AddWithValue("$input",    (object?)inputArgs       ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$output",   (object?)outputData      ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$error",    (object?)exceptionMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$stack",    (object?)stackTrace       ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$duration", durationMs);

                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // ── ILogService: Log Sorgulama ────────────────────────────────────

        public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryLogsAsync(
            string  logType,
            int     limit,
            string? severityFilter)
        {
            var results = new List<Dictionary<string, object?>>();

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();

            if (logType == "endpoint")
            {
                cmd.CommandText = BuildEndpointQuery(severityFilter, limit);
                if (!string.IsNullOrEmpty(severityFilter))
                    cmd.Parameters.AddWithValue("$status", severityFilter);
            }
            else // "function" (varsayılan)
            {
                cmd.CommandText = BuildFunctionQuery(severityFilter, limit);
                if (!string.IsNullOrEmpty(severityFilter))
                    cmd.Parameters.AddWithValue("$hasError", severityFilter == "Error" ? 1 : 0);
            }

            cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                results.Add(row);
            }

            return results;
        }

        // ── Özel: Sorgu oluşturucular ─────────────────────────────────────

        private static string BuildEndpointQuery(string? severity, int limit)
        {
            var where = string.IsNullOrEmpty(severity)
                ? ""
                : "WHERE status = $status ";

            return $"""
                SELECT id, trace_id, action, req_payload, res_payload,
                       duration_ms, status, created_at
                FROM   endpoint_logs
                {where}
                ORDER  BY id DESC
                LIMIT  $limit;
                """;
        }

        private static string BuildFunctionQuery(string? severity, int limit)
        {
            string where = severity switch
            {
                "Error"   => "WHERE error_msg IS NOT NULL ",
                "Success" => "WHERE error_msg IS NULL ",
                _         => ""
            };

            return $"""
                SELECT id, trace_id, method_name, file_path, line_number,
                       input_args, output_data, error_msg, stack_trace,
                       duration_ms, created_at
                FROM   function_logs
                {where}
                ORDER  BY id DESC
                LIMIT  $limit;
                """;
        }
    }
}
