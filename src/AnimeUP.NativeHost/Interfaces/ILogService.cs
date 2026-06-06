using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AnimeUP.NativeHost.Interfaces
{
    /// <summary>
    /// AnimeUP loglama sistemi arayüzü.
    ///
    /// İki tür log desteklenir:
    ///   1. EndpointLog  — Eklentiden gelen her Native Messaging isteği/yanıtı.
    ///   2. FunctionLog  — İç fonksiyonların girdi, çıktı ve hata kayıtları.
    ///
    /// Tüm yazma metotları async'tir; ana iş akışını bloklamaz.
    /// [CallerMemberName] / [CallerFilePath] öznitelikleri sayesinde
    /// fonksiyon adı ve konum bilgileri otomatik enjekte edilir.
    /// </summary>
    public interface ILogService
    {
        // ── Yazma ────────────────────────────────────────────────────────

        /// <summary>
        /// Bir endpoint isteğini (Native Messaging round-trip) loglar.
        /// </summary>
        /// <param name="traceId">İstek izleme kimliği</param>
        /// <param name="action">İstek aksiyonu (play, getLogs vb.)</param>
        /// <param name="requestPayload">JSON formatındaki istek gövdesi</param>
        /// <param name="responsePayload">JSON formatındaki yanıt gövdesi</param>
        /// <param name="durationMs">İşlem süresi (ms)</param>
        /// <param name="status">Durum: "Success" veya "Error"</param>
        Task LogEndpointAsync(
            string traceId,
            string action,
            string? requestPayload,
            string? responsePayload,
            int    durationMs,
            string status = "Success");

        /// <summary>
        /// Bir iç fonksiyon çağrısını loglar.
        /// Metot adı, dosya yolu ve satır numarası derleyici tarafından otomatik doldurulur.
        /// </summary>
        Task LogFunctionAsync(
            string  traceId,
            string? inputArgs,
            string? outputData,
            string? exceptionMessage,
            string? stackTrace,
            int     durationMs,
            [CallerMemberName] string methodName = "",
            [CallerFilePath]   string filePath   = "",
            [CallerLineNumber] int    lineNumber  = 0);

        // ── Okuma ────────────────────────────────────────────────────────

        /// <summary>
        /// Filtrelenmiş log kayıtlarını döndürür.
        /// </summary>
        /// <param name="logType">"function" veya "endpoint"</param>
        /// <param name="limit">Maksimum kayıt sayısı</param>
        /// <param name="severityFilter">"Success", "Error" veya null (tümü)</param>
        Task<IReadOnlyList<Dictionary<string, object?>>> QueryLogsAsync(
            string  logType,
            int     limit,
            string? severityFilter);
    }
}
