using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace AnimeUP.Installer
{
    /// <summary>
    /// AnimeUP Kurulum Motoru
    ///
    /// Görevler:
    ///  1. %APPDATA%\AnimeUP dizin yapısını oluşturur.
    ///  2. mpv-config klasörünü (shaders, scripts, script-opts) kopyalar.
    ///  3. Kullanıcıdan Chrome/Edge eklenti ID'sini alır.
    ///  4. com.animeup.nativehost.json manifest dosyasını oluşturur.
    ///  5. Chrome ve Edge için Windows Registry'ye NativeMessagingHosts kaydını yazar.
    ///  6. Kurulum özetini ekrana yazdırır.
    ///
    /// Hata Yönetimi:
    ///  - Her adım ayrı try-catch içindedir; bir adımın başarısızlığı diğerlerini durdurmaz.
    ///  - Kritik adımlar (Registry) başarısız olursa kullanıcı uyarılır ve exit kodu 1 döner.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class Program
    {
        // Tarayıcı kayıt defteri yolları
        private static readonly string[] BrowserRegistryPaths =
        {
            @"Software\Google\Chrome\NativeMessagingHosts\com.animeup.nativehost",
            @"Software\Microsoft\Edge\NativeMessagingHosts\com.animeup.nativehost",
            @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.animeup.nativehost"
        };

        private static readonly string AppDataDir =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AnimeUP");

        // ────────────────────────────────────────────────────────────────
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            PrintBanner();

            bool hasError = false;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // ── Adım 1: Dizinleri oluştur ────────────────────────────────
            Step("Dizinler oluşturuluyor...", () =>
            {
                Directory.CreateDirectory(AppDataDir);
                Directory.CreateDirectory(Path.Combine(AppDataDir, "mpv"));
                Directory.CreateDirectory(Path.Combine(AppDataDir, "mpv-config"));
            });

            // ── Adım 2: mpv-config klasörünü kopyala ─────────────────────
            Step("MPV yapılandırması kopyalanıyor...", () =>
            {
                string sourceMpvConfig = Path.Combine(baseDir, "mpv-config");
                string destMpvConfig   = Path.Combine(AppDataDir, "mpv-config");

                if (Directory.Exists(sourceMpvConfig))
                {
                    CopyDirectory(sourceMpvConfig, destMpvConfig);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  ✓ mpv-config kopyalandı.");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  ⚠ mpv-config klasörü bulunamadı — atlandı.");
                    Console.ResetColor();
                }
            });

            // ── Adım 2.1: mpv oynatıcı ve yt-dlp kopyalanıyor ─────────────
            Step("MPV oynatıcı ve yt-dlp kopyalanıyor...", () =>
            {
                string destMpv = Path.Combine(AppDataDir, "mpv");
                Directory.CreateDirectory(destMpv);

                bool copiedMpv = false;

                // 1. mpv.exe baseDir içinde doğrudan varsa kopyala (Shinchiro'nun arşivinden çıkarılmışsa)
                string mpvExeInBase = Path.Combine(baseDir, "mpv.exe");
                if (File.Exists(mpvExeInBase))
                {
                    File.Copy(mpvExeInBase, Path.Combine(destMpv, "mpv.exe"), true);
                    copiedMpv = true;

                    // Varsa d3dcompiler_43.dll dosyasını da kopyala
                    string d3dDll = Path.Combine(baseDir, "d3dcompiler_43.dll");
                    if (File.Exists(d3dDll))
                    {
                        File.Copy(d3dDll, Path.Combine(destMpv, "d3dcompiler_43.dll"), true);
                    }
                }

                // 2. mpv klasörü varsa içeriğini kopyala
                string sourceMpv = Path.Combine(baseDir, "mpv");
                if (Directory.Exists(sourceMpv))
                {
                    CopyDirectory(sourceMpv, destMpv);
                    copiedMpv = true;
                }

                if (copiedMpv)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  ✓ mpv oynatıcı kopyalandı.");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  ⚠ mpv oynatıcı (mpv.exe veya mpv/ klasörü) bulunamadı — atlandı.");
                    Console.ResetColor();
                }

                string sourceYtDlp = Path.Combine(baseDir, "yt-dlp.exe");
                string destYtDlp   = Path.Combine(AppDataDir, "mpv", "yt-dlp.exe");

                if (File.Exists(sourceYtDlp))
                {
                    File.Copy(sourceYtDlp, destYtDlp, true);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  ✓ yt-dlp.exe kopyalandı.");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  ⚠ yt-dlp.exe bulunamadı — atlandı.");
                    Console.ResetColor();
                }
            });

            // ── Adım 3: Eklenti ID'sini al ───────────────────────────────
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.WriteLine("  Chrome uzantısı yüklendiyse:");
            Console.WriteLine("  chrome://extensions → Geliştirici Modu → Uzantı ID'si");
            Console.WriteLine("─────────────────────────────────────────────────────────");
            Console.ResetColor();
            Console.Write("  Eklenti ID'sini girin (boş bırakırsanız daha sonra güncelleyebilirsiniz): ");

            string? extensionId = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(extensionId))
            {
                extensionId = "PLACEHOLDER_ID";
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ⚠ Eklenti ID'si boş — yer tutucu kullanıldı. Kurulumdan sonra manifest'i güncelleyin.");
                Console.ResetColor();
            }

            // ── Adım 4: Native Host manifest dosyasını oluştur ───────────
            string hostExePath   = Path.Combine(baseDir, "AnimeUP.NativeHost.exe");
            string manifestPath  = Path.Combine(AppDataDir, "com.animeup.nativehost.json");

            Step("Native Host manifest oluşturuluyor...", () =>
            {
                string manifest = BuildManifestJson(hostExePath, extensionId!);
                File.WriteAllText(manifestPath, manifest, Encoding.UTF8);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ Manifest: {manifestPath}");
                Console.ResetColor();
            });

            // ── Adım 5: Windows Registry'ye kaydet ───────────────────────
            Step("Registry anahtarları yazılıyor...", () =>
            {
                foreach (string regPath in BrowserRegistryPaths)
                {
                    try
                    {
                        using var key = Registry.CurrentUser.CreateSubKey(regPath);
                        key.SetValue(string.Empty, manifestPath);

                        string browserName = regPath.Contains("Edge") ? "Edge"
                            : regPath.Contains("Brave") ? "Brave" : "Chrome";

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  ✓ {browserName} kaydı tamamlandı.");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  ✗ Registry hatası ({regPath}): {ex.Message}");
                        Console.ResetColor();
                        hasError = true;
                    }
                }
            });

            // ── Kurulum Özeti ─────────────────────────────────────────────
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("═════════════════════════════════════════════════════════");

            if (!hasError)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ KURULUM BAŞARIYLA TAMAMLANDI!");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("  Sonraki Adımlar:");
                Console.WriteLine("  1. Chrome/Edge'i yeniden başlatın.");
                Console.WriteLine("  2. AnimeUP eklentisini tarayıcıya yükleyin.");
                Console.WriteLine($"  3. Eklenti ID: {extensionId}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ✗ KURULUM HATALARLA TAMAMLANDI.");
                Console.WriteLine("  Registry yazma hatası oluştu. Yönetici olarak çalıştırmayı deneyin.");
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("═════════════════════════════════════════════════════════");
            Console.ResetColor();
            Console.WriteLine("\nÇıkmak için Enter'a basın...");
            Console.ReadLine();

            return hasError ? 1 : 0;
        }

        // ── Yardımcı: Adım çalıştırıcı ───────────────────────────────────
        private static void Step(string description, Action action)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"▶ {description}");
            Console.ResetColor();

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ Hata: {ex.Message}");
                Console.ResetColor();
            }
        }

        // ── Yardımcı: Manifest JSON şablonu ──────────────────────────────
        private static string BuildManifestJson(string hostExePath, string extensionId)
        {
            // Path separatorlerini JSON'a uygun hale getir
            string escapedPath = hostExePath.Replace(@"\", @"\\");

            return "{\n" +
                   "  \"name\": \"com.animeup.nativehost\",\n" +
                   "  \"description\": \"AnimeUP Chrome Extension Companion Host\",\n" +
                  $"  \"path\": \"{escapedPath}\",\n" +
                   "  \"type\": \"stdio\",\n" +
                   "  \"allowed_origins\": [\n" +
                  $"    \"chrome-extension://{extensionId}/\"\n" +
                   "  ]\n" +
                   "}";
        }

        // ── Yardımcı: Klasör kopyalama (özyinelemeli) ────────────────────
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                file.CopyTo(Path.Combine(destDir, file.Name), overwrite: true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                CopyDirectory(subDir.FullName, Path.Combine(destDir, subDir.Name));
            }
        }

        // ── Yardımcı: Banner ──────────────────────────────────────────────
        private static void PrintBanner()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("""

              █████╗ ███╗   ██╗██╗███╗   ███╗███████╗██╗   ██╗██████╗
             ██╔══██╗████╗  ██║██║████╗ ████║██╔════╝██║   ██║██╔══██╗
             ███████║██╔██╗ ██║██║██╔████╔██║█████╗  ██║   ██║██████╔╝
             ██╔══██║██║╚██╗██║██║██║╚██╔╝██║██╔══╝  ██║   ██║██╔═══╝
             ██║  ██║██║ ╚████║██║██║ ╚═╝ ██║███████╗╚██████╔╝██║
             ╚═╝  ╚═╝╚═╝  ╚═══╝╚═╝╚═╝     ╚═╝╚══════╝ ╚═════╝ ╚═╝

            """);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Real-Time Anime Upscaler — Kurulum Motoru v1.0");
            Console.ResetColor();
        }
    }
}
