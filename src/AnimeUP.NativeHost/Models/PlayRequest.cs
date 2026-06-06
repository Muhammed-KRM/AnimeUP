using System;
using System.Text.Json.Serialization;

namespace AnimeUP.NativeHost.Models
{
    /// <summary>
    /// Chrome Extension'dan Native Host'a gelen oynatma isteğinin veri modeli.
    /// JSON alanları camelCase olarak alınır, C# özellikleri PascalCase'dir.
    /// </summary>
    public sealed class PlayRequest
    {
        /// <summary>İstek türü: "play" | "getLogs"</summary>
        [JsonPropertyName("action")]
        public string Action { get; init; } = string.Empty;

        /// <summary>Video akış URL'si (M3U8, MP4, MPD veya iframe URL'si)</summary>
        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;

        /// <summary>Videonun bulunduğu sayfanın URL'si (CORS Referer için)</summary>
        [JsonPropertyName("pageUrl")]
        public string PageUrl { get; init; } = string.Empty;

        /// <summary>Video başlığı (MPV pencere başlığına yansır)</summary>
        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        /// <summary>HTTP Referer başlık değeri (CORS engelini aşmak için)</summary>
        [JsonPropertyName("referer")]
        public string Referer { get; init; } = string.Empty;

        // ── Log Sorgu Alanları ────────────────────────────────────────────

        /// <summary>Log türü filtresi: "function" | "endpoint"</summary>
        [JsonPropertyName("logType")]
        public string? LogType { get; init; }

        /// <summary>Kaç log kaydı getirileceği (varsayılan: 50)</summary>
        [JsonPropertyName("limit")]
        public int Limit { get; init; } = 50;

        /// <summary>Önem filtresi: "Success" | "Error" | null</summary>
        [JsonPropertyName("severity")]
        public string? Severity { get; init; }
    }
}
