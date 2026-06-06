using System.Text.Json.Serialization;

namespace AnimeUP.NativeHost.Models
{
    /// <summary>
    /// Native Host'tan Chrome Extension'a gönderilen yanıt modeli.
    /// Her aksiyon tipi için genel kullanım sağlar.
    /// </summary>
    public sealed class StatusResponse
    {
        /// <summary>İşlemin başarılı olup olmadığı</summary>
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        /// <summary>Kullanıcı dostu durum mesajı</summary>
        [JsonPropertyName("message")]
        public string Message { get; init; } = string.Empty;

        /// <summary>MPV süreç ID'si (yalnızca "play" eylemlerinde doldurulur)</summary>
        [JsonPropertyName("pid")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Pid { get; init; }

        /// <summary>Hata mesajı (yalnızca başarısız işlemlerde doldurulur)</summary>
        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Error { get; init; }

        /// <summary>Log listesi (yalnızca "getLogs" yanıtlarında doldurulur)</summary>
        [JsonPropertyName("logs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<Dictionary<string, object?>>? Logs { get; init; }

        // ── Fabrika metotları — nesne oluşturma mantığını merkezileştirir ──

        public static StatusResponse Ok(string message, int? pid = null) =>
            new() { Success = true, Message = message, Pid = pid };

        public static StatusResponse Fail(string error) =>
            new() { Success = false, Message = "Failed", Error = error };

        public static StatusResponse WithLogs(IReadOnlyList<Dictionary<string, object?>> logs) =>
            new() { Success = true, Message = "OK", Logs = logs };
    }
}
