using AnimeUP.NativeHost.Data;
using AnimeUP.NativeHost.Helpers;
using AnimeUP.NativeHost.Interfaces;
using AnimeUP.NativeHost.Models;
using AnimeUP.NativeHost.Serialization;
using AnimeUP.NativeHost.Services;

namespace AnimeUP.NativeHost
{
    /// <summary>
    /// AnimeUP Native Messaging Host — Ana Giriş Noktası
    ///
    /// Akış:
    ///  1. SQLite veritabanını ve tabloları başlat (DbInitializer).
    ///  2. Servis bağımlılıklarını oluştur (LogManager, MpvLauncher).
    ///  3. Sonsuz döngüde Chrome Native Messaging stdin'ini dinle.
    ///  4. Gelen her mesajı aksiyon tipine göre yönlendir:
    ///       "play"    → MpvLauncher ile MPV'yi başlat
    ///       "getLogs" → SQLite'tan logları çek ve döndür
    ///  5. Her isteği endpoint_logs tablosuna kaydet.
    ///  6. Pipe kapanırsa (Chrome sekme kapatıldı) temizce çık.
    ///
    /// Tasarım Kararları:
    ///  - yt-dlp güncelleme kontrolü arka planda (fire-and-forget) çalışır.
    ///  - Her istek için UUID traceId üretilir; loglarda iz sürmeyi kolaylaştırır.
    ///  - Beklenmedik exception'lar yakalanır, loglanır ve host çökmez.
    /// </summary>
    internal static class Program
    {
        private static ILogService _logService = null!;
        private static MpvLauncher _mpvLauncher = null!;

        private static readonly string DbPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AnimeUP",
                "animeup.db");

        private static readonly string ConnectionString =
            $"Data Source={DbPath};Cache=Shared;";

        // ────────────────────────────────────────────────────────────────
        static async Task Main(string[] args)
        {
            Logger.Log("=== AnimeUP NativeHost başlatıldı ===");

            // 1. Veritabanı dizinini ve tabloları oluştur
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
            DbInitializer.Initialize(ConnectionString);
            Logger.Log($"SQLite DB hazır: {DbPath}");

            // 2. Servisleri oluştur
            _logService  = new LogManager(ConnectionString);
            _mpvLauncher = new MpvLauncher(_logService);

            // 3. yt-dlp güncelleme kontrolü (arka planda, non-blocking)
            _ = Task.Run(CheckYtDlpUpdateAsync);

            // 4. Chrome Native Messaging ana döngüsü
            try
            {
                await RunMessageLoopAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Ana döngüde beklenmedik hata", ex);
            }

            Logger.Log("=== AnimeUP NativeHost sonlandırıldı ===");
        }

        // ── Ana Mesaj Döngüsü ────────────────────────────────────────────
        private static async Task RunMessageLoopAsync()
        {
            while (true)
            {
                // Stdin'den mesaj oku (bloklar — Chrome mesaj gönderene kadar bekler)
                PlayRequest? request = NativeMessaging.ReadMessage();

                if (request is null)
                {
                    Logger.Log("Stdin kapandı — döngü sonlandırılıyor.");
                    break;
                }

                string traceId = Guid.NewGuid().ToString("N")[..16];
                Logger.Log($"[{traceId}] Gelen aksiyon: {request.Action}");

                StatusResponse response = await HandleRequestAsync(request, traceId);
                NativeMessaging.WriteMessage(response);
            }
        }

        // ── İstek Yönlendirici ────────────────────────────────────────────
        private static async Task<StatusResponse> HandleRequestAsync(
            PlayRequest request, string traceId)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // AOT-safe: AppJsonContext ile serileştir
            string requestJson = System.Text.Json.JsonSerializer.Serialize(request, AppJsonContext.Default.PlayRequest);
            StatusResponse response;

            try
            {
                response = request.Action?.ToLowerInvariant() switch
                {
                    "play"    => await HandlePlayAsync(request, traceId),
                    "getlogs" => await HandleGetLogsAsync(request, traceId),
                    _         => StatusResponse.Fail($"Bilinmeyen aksiyon: {request.Action}")
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"[{traceId}] İstek işleme hatası", ex);
                response = StatusResponse.Fail($"Sunucu hatası: {ex.Message}");
            }

            stopwatch.Stop();

            // Her isteği endpoint_logs tablosuna kaydet (fire-and-forget)
            string responseJson = System.Text.Json.JsonSerializer.Serialize(response, AppJsonContext.Default.StatusResponse);
            string status = response.Success ? "Success" : "Error";
            _ = _logService.LogEndpointAsync(
                traceId, request.Action ?? "unknown",
                requestJson, responseJson,
                (int)stopwatch.ElapsedMilliseconds, status);

            return response;
        }

        // ── "play" Aksiyonu ───────────────────────────────────────────────
        private static async Task<StatusResponse> HandlePlayAsync(
            PlayRequest request, string traceId)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                Logger.Warn($"[{traceId}] Boş video URL'si.");
                return StatusResponse.Fail("Video URL'si boş.");
            }

            int pid = await _mpvLauncher.LaunchAsync(request, traceId);

            return pid > 0
                ? StatusResponse.Ok($"MPV başlatıldı → PID {pid}", pid)
                : StatusResponse.Fail("MPV başlatılamadı. host.log dosyasını inceleyin.");
        }

        // ── "getLogs" Aksiyonu ────────────────────────────────────────────
        private static async Task<StatusResponse> HandleGetLogsAsync(
            PlayRequest request, string traceId)
        {
            string  logType  = request.LogType  ?? "function";
            int     limit    = Math.Clamp(request.Limit, 1, 500);
            string? severity = request.Severity;

            Logger.Log($"[{traceId}] Log sorgusu → tür={logType}, limit={limit}, filtre={severity ?? "tümü"}");

            var logs = await _logService.QueryLogsAsync(logType, limit, severity);
            return StatusResponse.WithLogs(logs);
        }

        // ── yt-dlp Otomatik Güncelleme ────────────────────────────────────
        private static async Task CheckYtDlpUpdateAsync()
        {
            try
            {
                string ytDlpPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AnimeUP", "mpv", "yt-dlp.exe");

                if (!File.Exists(ytDlpPath))
                {
                    Logger.Warn("[yt-dlp] Güncelleme atlandı: yt-dlp.exe bulunamadı.");
                    return;
                }

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = ytDlpPath,
                    Arguments       = "-U",
                    UseShellExecute = false,
                    CreateNoWindow  = true
                };

                var proc = System.Diagnostics.Process.Start(startInfo);
                if (proc is not null)
                {
                    await proc.WaitForExitAsync();
                    Logger.Log($"[yt-dlp] Güncelleme kontrolü tamamlandı. Exit={proc.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[yt-dlp] Güncelleme hatası: {ex.Message}");
            }
        }
    }
}
