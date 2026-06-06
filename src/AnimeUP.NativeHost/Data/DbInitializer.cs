using Microsoft.Data.Sqlite;

namespace AnimeUP.NativeHost.Data
{
    /// <summary>
    /// Uygulama ilk başlatıldığında SQLite veritabanını ve tablolarını oluşturur.
    ///
    /// Tasarım Kararları:
    ///  - CREATE TABLE IF NOT EXISTS → Yeniden başlatmalar idempotent'tir.
    ///  - PRAGMA journal_mode=WAL    → Okuma/yazma çakışmalarını önler (eşzamanlı sorgular).
    ///  - PRAGMA synchronous=NORMAL  → Performans/güvenlik dengesi.
    ///  - created_at alanı UTC timestamp olarak TEXT tipinde saklanır.
    /// </summary>
    public static class DbInitializer
    {
        /// <summary>
        /// Verilen bağlantı dizesini kullanarak DB'yi başlatır.
        /// Tablolar zaten varsa hiçbir şey yapmaz.
        /// </summary>
        /// <param name="connectionString">SQLite bağlantı dizesi</param>
        public static void Initialize(string connectionString)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            // WAL modu: çok okuyucu, tek yazıcı senaryosunda kilitlemeyi önler
            ExecuteNonQuery(connection, "PRAGMA journal_mode=WAL;");
            ExecuteNonQuery(connection, "PRAGMA synchronous=NORMAL;");
            ExecuteNonQuery(connection, "PRAGMA foreign_keys=ON;");

            // Endpoint logları: Extension → NativeHost round-trip kayıtları
            ExecuteNonQuery(connection, """
                CREATE TABLE IF NOT EXISTS endpoint_logs (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    trace_id     TEXT    NOT NULL,
                    action       TEXT    NOT NULL,
                    req_payload  TEXT,
                    res_payload  TEXT,
                    duration_ms  INTEGER NOT NULL DEFAULT 0,
                    status       TEXT    NOT NULL DEFAULT 'Success',
                    created_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now'))
                );
                """);

            // Fonksiyon logları: Her iç metot çağrısının girdi/çıktı/hata kaydı
            ExecuteNonQuery(connection, """
                CREATE TABLE IF NOT EXISTS function_logs (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    trace_id     TEXT    NOT NULL,
                    method_name  TEXT,
                    file_path    TEXT,
                    line_number  INTEGER,
                    input_args   TEXT,
                    output_data  TEXT,
                    error_msg    TEXT,
                    stack_trace  TEXT,
                    duration_ms  INTEGER NOT NULL DEFAULT 0,
                    created_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now'))
                );
                """);

            // Sorgulama performansı için indeksler
            ExecuteNonQuery(connection,
                "CREATE INDEX IF NOT EXISTS idx_endpoint_trace ON endpoint_logs(trace_id);");
            ExecuteNonQuery(connection,
                "CREATE INDEX IF NOT EXISTS idx_endpoint_status ON endpoint_logs(status);");
            ExecuteNonQuery(connection,
                "CREATE INDEX IF NOT EXISTS idx_function_trace ON function_logs(trace_id);");
            ExecuteNonQuery(connection,
                "CREATE INDEX IF NOT EXISTS idx_function_method ON function_logs(method_name);");
            ExecuteNonQuery(connection,
                "CREATE INDEX IF NOT EXISTS idx_function_error ON function_logs(error_msg) WHERE error_msg IS NOT NULL;");
        }

        private static void ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
