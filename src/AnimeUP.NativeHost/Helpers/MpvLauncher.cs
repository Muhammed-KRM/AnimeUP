using System.Diagnostics;
using AnimeUP.NativeHost.Interfaces;
using AnimeUP.NativeHost.Models;

namespace AnimeUP.NativeHost.Helpers
{
    /// <summary>
    /// MPV media player sürecini güvenli argümanlarla başlatan sınıf.
    ///
    /// Tasarım Kararları:
    ///  - ArgumentList kullanılır (string birleştirme değil) — shell injection'a karşı koruma.
    ///  - MPV yolu önce %APPDATA%\AnimeUP\mpv altında aranır, bulunamazsa PATH'e fallback yapılır.
    ///  - Tek-instance kontrolü: Yeni video açılmadan önce mevcut MPV süreçleri sonlandırılır.
    ///  - GPU fallback: vulkan → d3d11 → opengl sırası mpv.conf'da tanımlıdır; burası sadece
    ///    --gpu-api başlangıç değerini iletir.
    ///  - ILogService enjeksiyonu ile her kritik adım loglanır.
    /// </summary>
    public sealed class MpvLauncher
    {
        private readonly ILogService _logService;

        private static readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AnimeUP");

        private static readonly string DefaultMpvExe =
            Path.Combine(AppDataDir, "mpv", "mpv.exe");

        private static readonly string ConfigDir =
            Path.Combine(AppDataDir, "mpv-config");

        public MpvLauncher(ILogService logService)
        {
            _logService = logService;
        }

        /// <summary>
        /// MPV oynatıcısını verilen istek parametreleriyle başlatır.
        /// </summary>
        /// <param name="request">Eklentiden gelen oynatma isteği</param>
        /// <param name="traceId">Bu işleme ait izleme kimliği</param>
        /// <returns>MPV süreç ID'si; başarısız olursa -1</returns>
        public async Task<int> LaunchAsync(PlayRequest request, string traceId)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            string? inputJson = null;

            try
            {
                // Loglama için trim-safe string format
                inputJson = $"{{\"url\":\"{request.Url}\",\"title\":\"{request.Title}\",\"referer\":\"{request.Referer}\",\"pageUrl\":\"{request.PageUrl}\"}}";

                string mpvExe = ResolveMpvPath();
                Logger.Log($"[MpvLauncher] MPV yolu: {mpvExe}");

                TerminateExistingInstances();

                var startInfo = BuildStartInfo(mpvExe, request);
                var process   = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Process.Start null döndürdü.");

                int pid = process.Id;
                stopwatch.Stop();

                string outputJson = $"{{\"pid\":{pid}}}";
                _ = _logService.LogFunctionAsync(
                    traceId, inputJson, outputJson, null, null,
                    (int)stopwatch.ElapsedMilliseconds);

                Logger.Log($"[MpvLauncher] MPV başlatıldı → PID {pid}");
                return pid;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error("[MpvLauncher] Başlatma hatası", ex);

                _ = _logService.LogFunctionAsync(
                    traceId, inputJson, null, ex.Message, ex.StackTrace,
                    (int)stopwatch.ElapsedMilliseconds);

                return -1;
            }
        }

        // ── Özel: MPV yolunu çöz ──────────────────────────────────────────

        private static string ResolveMpvPath()
        {
            if (File.Exists(DefaultMpvExe))
                return DefaultMpvExe;

            // PATH'te ara
            Logger.Warn("[MpvLauncher] Yerel MPV bulunamadı, PATH'e fallback yapılıyor.");
            return "mpv.exe";
        }

        // ── Özel: Mevcut MPV süreçlerini sonlandır ────────────────────────

        private static void TerminateExistingInstances()
        {
            try
            {
                var processes = Process.GetProcessesByName("mpv");
                foreach (var proc in processes)
                {
                    try
                    {
                        proc.Kill(entireProcessTree: true);
                        Logger.Log($"[MpvLauncher] Eski MPV süreci sonlandırıldı → PID {proc.Id}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[MpvLauncher] PID {proc.Id} sonlandırılamadı: {ex.Message}");
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[MpvLauncher] Süreç listesi alınamadı: {ex.Message}");
            }
        }

        // ── Özel: ProcessStartInfo oluştur ───────────────────────────────

        private static ProcessStartInfo BuildStartInfo(string mpvExe, PlayRequest request)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName         = mpvExe,
                UseShellExecute  = false,
                CreateNoWindow   = false,
                RedirectStandardError = false
            };

            // 1. Video URL'si (zorunlu)
            startInfo.ArgumentList.Add(request.Url);

            // 2. Pencere başlığı
            string title = string.IsNullOrWhiteSpace(request.Title)
                ? "AnimeUP"
                : $"AnimeUP — {request.Title}";
            startInfo.ArgumentList.Add($"--title={title}");

            // 3. Config dizini
            if (Directory.Exists(ConfigDir))
                startInfo.ArgumentList.Add($"--config-dir={ConfigDir}");

            // 4. yt-dlp entegrasyonu (sibnet, turkanime, YouTube vb. için)
            startInfo.ArgumentList.Add("--ytdl=yes");
            string ytDlpExe = Path.Combine(AppDataDir, "mpv", "yt-dlp.exe");
            if (File.Exists(ytDlpExe))
            {
                startInfo.ArgumentList.Add($"--ytdl-path={ytDlpExe}");
                Logger.Log($"[MpvLauncher] yt-dlp bulundu: {ytDlpExe}");
            }
            else
            {
                Logger.Warn("[MpvLauncher] yt-dlp bulunamadı, PATH fallback.");
            }

            // 5. HTTP header'lar (Referer + User-Agent + Cookie)
            var headers = new System.Text.StringBuilder();
            headers.Append("User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");

            if (!string.IsNullOrWhiteSpace(request.Referer))
                headers.Append($",Referer: {request.Referer}");

            if (!string.IsNullOrWhiteSpace(request.Cookies))
                headers.Append($",Cookie: {request.Cookies}");

            startInfo.ArgumentList.Add($"--http-header-fields={headers}");

            // 6. Render altyapısı
            startInfo.ArgumentList.Add("--vo=gpu-next");
            startInfo.ArgumentList.Add("--hwdec=auto-safe");

            // 7. OSD başlangıç seviyesi
            startInfo.ArgumentList.Add("--osd-level=1");

            return startInfo;
        }

    }
}
