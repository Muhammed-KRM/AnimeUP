using System.IO;
using System.Text;
using System.Text.Json;
using AnimeUP.NativeHost.Models;
using AnimeUP.NativeHost.Serialization;

namespace AnimeUP.NativeHost.Helpers
{
    /// <summary>
    /// Chrome Native Messaging protokolüne göre stdin/stdout üzerinden
    /// 4-byte big-endian uzunluk öneki + JSON mesaj okuyup yazar.
    ///
    /// Protokol:
    ///   [ 4-byte LE int32: mesaj uzunluğu ] [ UTF-8 JSON gövdesi ]
    ///
    /// Tasarım Kararları:
    ///  - ReadFully() ile akış parçalanmalarına (stream split) karşı güvenlik.
    ///  - Encoding.UTF8 (BOM'suz) zorunludur — Chrome BOM'lu UTF8'i reddeder.
    ///  - stdout.Flush() her yazma sonrası çağrılır; tampon sıkışmasını önler.
    ///  - Hem okuma hem yazma için ayrı akışlar kullanılır.
    /// </summary>
    public static class NativeMessaging
    {
        private static readonly Stream _stdin  = Console.OpenStandardInput();
        private static readonly Stream _stdout = Console.OpenStandardOutput();

        // AOT-safe context — IL2026 uyarısı vermez
        private static readonly AppJsonContext _jsonContext = AppJsonContext.Default;

        // ── Okuma ────────────────────────────────────────────────────────

        /// <summary>
        /// Stdin'den bir Chrome Native Messaging mesajı okur.
        /// </summary>
        /// <returns>Deserialize edilmiş PlayRequest veya null (pipe kapandıysa)</returns>
        public static PlayRequest? ReadMessage()
        {
            // 1. 4-byte uzunluk önekini oku
            byte[] lengthBuffer = ReadFully(_stdin, 4);
            if (lengthBuffer.Length < 4)
            {
                Logger.Log("Stdin pipe kapandı veya uzunluk öneki eksik.");
                return null;
            }

            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

            if (messageLength <= 0 || messageLength > 1_048_576) // Max 1MB güvenlik sınırı
            {
                Logger.Warn($"Geçersiz mesaj uzunluğu: {messageLength}");
                return null;
            }

            // 2. JSON gövdesini oku
            byte[] messageBuffer = ReadFully(_stdin, messageLength);
            if (messageBuffer.Length != messageLength)
            {
                Logger.Warn($"Eksik veri okundu: beklenen={messageLength}, alınan={messageBuffer.Length}");
                return null;
            }

            string jsonString = Encoding.UTF8.GetString(messageBuffer);

            try
            {
                return JsonSerializer.Deserialize(jsonString, _jsonContext.PlayRequest);
            }
            catch (JsonException ex)
            {
                Logger.Error($"JSON ayrıştırma hatası: {ex.Message}", ex);
                return null;
            }
        }

        // ── Yazma ────────────────────────────────────────────────────────

        /// <summary>
        /// StatusResponse'u Chrome Native Messaging formatında stdout'a yazar.
        /// </summary>
        public static void WriteMessage(StatusResponse response)
        {
            try
            {
                byte[] messageBuffer = JsonSerializer.SerializeToUtf8Bytes(response, _jsonContext.StatusResponse);
                byte[] lengthBuffer  = BitConverter.GetBytes(messageBuffer.Length);

                // Atomiklik için önce uzunluk, sonra gövde; iki Write arası boşluk yok.
                _stdout.Write(lengthBuffer, 0, 4);
                _stdout.Write(messageBuffer, 0, messageBuffer.Length);
                _stdout.Flush();
            }
            catch (Exception ex)
            {
                Logger.Error("Stdout yazma hatası", ex);
            }
        }

        // ── Özel: Güvenli Tam Okuma ───────────────────────────────────────

        /// <summary>
        /// Akıştan tam olarak <paramref name="length"/> byte okur.
        /// TCP/pipe parçalanmalarına karşı döngü kullanır.
        /// </summary>
        private static byte[] ReadFully(Stream stream, int length)
        {
            byte[] buffer   = new byte[length];
            int totalRead   = 0;

            while (totalRead < length)
            {
                int read = stream.Read(buffer, totalRead, length - totalRead);
                if (read <= 0) break; // EOF veya pipe kapandı
                totalRead += read;
            }

            if (totalRead < length)
            {
                // Kısmi veri: yalnızca okunan kısmı döndür
                Array.Resize(ref buffer, totalRead);
            }

            return buffer;
        }
    }
}
