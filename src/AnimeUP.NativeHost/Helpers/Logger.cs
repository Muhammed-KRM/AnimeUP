using System.IO;

namespace AnimeUP.NativeHost.Helpers
{
    /// <summary>
    /// %TEMP%\AnimeUP\host.log dosyasına basit metin log kaydı yazan yardımcı.
    ///
    /// Kullanım Amacı:
    ///  - SQLite başlatılmadan önce veya kritik hatalarda fallback kayıt.
    ///  - Geliştirme sırasında Debug çıktısı.
    ///  - Üretimde de aktif; dosya max ~5MB'da rotate edilir.
    ///
    /// Tasarım Notu:
    ///  - static lock ile thread-safe yazma.
    ///  - Her satırda UTC timestamp ve kısa kaynak bilgisi içerir.
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDirectory =
            Path.Combine(Path.GetTempPath(), "AnimeUP");

        private static readonly string LogFilePath =
            Path.Combine(LogDirectory, "host.log");

        private static readonly object _fileLock = new();

        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        static Logger()
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
            }
            catch
            {
                // Log dizini oluşturulamazsa sessizce devam et.
            }
        }

        /// <summary>INFO seviyesinde log yazar.</summary>
        public static void Log(string message) =>
            Write("INFO", message);

        /// <summary>WARNING seviyesinde log yazar.</summary>
        public static void Warn(string message) =>
            Write("WARN", message);

        /// <summary>ERROR seviyesinde log yazar.</summary>
        public static void Error(string message, Exception? ex = null)
        {
            Write("ERROR", message);
            if (ex is not null)
                Write("ERROR", $"Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }

        // ── Özel: Dosyaya yazar ───────────────────────────────────────────
        private static void Write(string level, string message)
        {
            try
            {
                string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] {message}";

                lock (_fileLock)
                {
                    RotateIfNeeded();
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Log yazımı asla ana akışı çökertemez.
            }
        }

        /// <summary>Dosya boyutu limiti aşılırsa .old sürümüne yeniden adlandır.</summary>
        private static void RotateIfNeeded()
        {
            try
            {
                var info = new FileInfo(LogFilePath);
                if (info.Exists && info.Length > MaxFileSizeBytes)
                {
                    string backup = LogFilePath + ".old";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(LogFilePath, backup);
                }
            }
            catch { }
        }
    }
}
