# AnimeUP — Real-Time Anime Upscaler

> İzlediğiniz animeleri tek tıkla yerel MPV oynatıcısında **Anime4K AI shader'ları** ile gerçek zamanlı 4K kalitede izleyin.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue)](https://github.com/Muhammed-KRM/AnimeUP)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)

---

## Nasıl Çalışır?

```
[Anime Sitesi] → [Chrome Extension] → [Native Host] → [MPV + Anime4K]
                   (Video URL yakala)   (Güvenli köprü)   (GPU upscale)
```

1. Chrome Extension, izlediğiniz sayfadaki video akış URL'sini (M3U8, MP4 vb.) arka planda yakalar.
2. Popup'ta **"AnimeUP ile İzle"** butonuna basarsınız.
3. C# Native Host, MPV oynatıcısını Anime4K shader'larıyla başlatır.
4. Eğer video 1080p altındaysa shader otomatik devreye girer — gerçek zamanlı 4K upscale başlar.

---

## Kurulum

Detaylı kurulum adımları için [`docs/developer_document.md`](docs/developer_document.md) dosyasını inceleyin.

**Hızlı Başlangıç:**
1. `AnimeUP.Installer.exe`'yi çalıştırın.
2. Chrome eklentisini `src/AnimeUP.Extension/` klasöründen yükleyin.
3. Herhangi bir anime sitesine gidin ve popup'ı açın.

---

## Özellikler

- ✅ Chrome, Edge, Brave desteği (Manifest V3)
- ✅ Otomatik video URL tespiti (ağ sniffing + DOM MutationObserver)
- ✅ Anime4K Mode A/B/C profil geçişi (CTRL+1/2/3)
- ✅ Otomatik çözünürlük algılama — <1080p'de shader otomatik açılır
- ✅ SQLite tabanlı fonksiyon/endpoint loglama sistemi
- ✅ Premium Log Paneli arayüzü (filtre, arama, detay modal)
- ✅ yt-dlp entegrasyonu — iframe barındırıcılar desteklenir
- ✅ Portable — kurulum sonrası .NET runtime gerekmez

---

## Teknoloji Yığını

| Katman | Teknoloji |
|--------|-----------|
| Browser Extension | JavaScript ES6+ / Manifest V3 |
| Desktop Host | C# .NET 8 / SQLite |
| Media Player | MPV 0.38+ |
| Upscaling | Anime4K v4.0.1 GLSL Shaders |
| Player UI | uosc 5.0+ |
| URL Resolver | yt-dlp |

---

## Lisans

MIT — Ayrıntılar için [LICENSE](LICENSE) dosyasına bakın.
