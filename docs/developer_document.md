# AnimeUP — Geliştirici Dökümanı v1.0

> Bu döküman, AnimeUP projesinin teknik tasarımını, mimarisini, dosya yapısını, veri protokollerini ve
> adım adım implementasyon planını en ince ayrıntısına kadar içerir. Bir geliştirici bu dökümanı okuyarak
> projeyi sıfırdan ayağa kaldırabilmeli ve kodlayabilmelidir.

---

## BÖLÜM 1: PROJE TANIMI VE TASARIM KARARLARI

### 1.1 Projenin Amacı ve Vizyonu

AnimeUP, web tarayıcısında (Chrome, Edge, Opera, Brave vb.) herhangi bir anime sitesinde (turkanime.tv, openani.me, tranimeizle.io vb.) izlenen düşük çözünürlüklü (480p/720p) veya yüksek çözünürlüklü ancak düşük bitrate'li anime videolarının kalitesini, bilgisayara hiçbir video dosyası indirmeden, gerçek zamanlı olarak 1080p veya 4K seviyesine yükselten bir masaüstü araç setidir.

Sistem, tarayıcıda kurulu olan bir **Chrome Extension (Manifest V3)** aracılığıyla kullanıcının izlediği sayfadaki video akışının (stream) bağlantısını yakalar ve bu bağlantıyı yerel makinedeki **Native Messaging** protokolü üzerinden **C# (.NET 8/9)** ile yazılmış bir **Native Messaging Host** uygulamasına aktarır. Bu host, **MPV Media Player** oynatıcısını başlatarak yakalanan video akışını doğrudan MPV'ye paslar. MPV, ekran kartının (GPU) donanımsal gücünü kullanarak **Anime4K** yapay zeka shader'larını her bir kareye milisaniyeler içinde uygular.

### 1.2 Teknik Kararlar (Geliştirici Tercihleri)

Geliştiricinin .NET/C# ekosistemine olan aşinalığı ve projenin taşınabilirliği göz önünde bulundurularak aşağıdaki mimari ve teknolojik tercihler yapılmıştır:

1. **Native Messaging Host Dili (C# - .NET 8/9):**
   - **Neden:** Python veya Node.js gibi ek runtime gereksinimlerini ortadan kaldırmak için C# tercih edilmiştir. C# ile yazılan host uygulaması, `PublishSingleFile=true`, `SelfContained=true` ve `PublishTrimmed=true` parametreleriyle derlenerek tek bir hafif `.exe` haline getirilir. Kullanıcının bilgisayarında .NET runtime kurulmasına gerek kalmaz.
   - **Boyut & Performans:**2. **Hedef Tarayıcılar (Chromium Tabanlı Tarayıcılar - Manifest V3 / Firefox Uyarısı):**
   - **Kapsam:** Google Chrome, Microsoft Edge, Brave, Opera ve diğer tüm Chromium altyapısını kullanan tarayıcılar varsayılan olarak desteklenir.
   - **Firefox Desteği Notu:** Mozilla Firefox, Native Messaging manifest dosyası için farklı kayıt defteri yolları (`HKCU\Software\Mozilla\NativeMessagingHosts\com.animeup.nativehost`) ve browser.runtime API'sini kullanır. Firefox entegrasyonu sonraki aşamada ele alınacaktır.
   - **Mimari:** Eklenti Manifest V3 standardında yazılacaktır. Arka planda çalışan bir `service_worker` (background.js) ve sayfadaki DOM yapısını inceleyen/ağ isteklerini izleyen bir `content_script.js` kullanılacaktır.

3. **Video URL Tespit Stratejisi (Hibrit Yöntem):**
   - **Birincil Yöntem (Ağ Sniffing):** Chrome Extension, `chrome.webRequest` veya `chrome.declarativeNetRequest` API'lerini kullanarak sayfada yüklenen medya akışlarını (.mp4, .m3u8, .mpd uzantılı veya `video/*` content-type içeren istekleri) dinamik olarak yakalar. MV3 uyumluluğu için, `webRequest.onBeforeRequest` asenkron ve salt okunur (non-blocking) olarak çalışır.
   - **Gereksiz Uykudan Kaçınma Fallback Yöntemi:** Service worker uyku moduna girdiğinde veya pasifleştiğinde ağ sniffing eyleminin durmaması için, `content_script.js` DOM üzerindeki video etiketlerini (`<video>`, `<source>` ve `<iframe>` kaynaklarını) `MutationObserver` ile izler. Yakalanan video URL'leri eklenti popup penceresi açıldığında veya oynatma talep edildiğinde doğrudan background worker'a fırlatılır ve worker'ı uyandırır.
   - **İkincil Yöntem (DOM Ayrıştırma):** Eğer video bir `<iframe>` içinde gömülüyse (örneğin Sibnet, Fembed, Streamtape, Mixdrop, Dailymotion player'ları), eklenti iframe kaynak URL'sini alır ve yerel hosta iletir. Hosta yerleşik olan `yt-dlp` bu popüler video barındırma platformlarının arkasındaki gerçek video akış URL'sini çözümler.
   - **Üçüncül Yöntem (Sayfa URL'si):** Eğer yukarıdakiler başarısız olursa, doğrudan sayfanın mevcut URL'si yt-dlp'ye gönderilir.

4. **Kurulum Deneyimi (Portable ZIP + C# Setup Engine):**
   - **Yapı:** Proje, taşınabilir bir ZIP paketi olarak sunulur. ZIP içerisinde MPV oynatıcı, uosc arayüzü, Anime4K shader'ları ve C# ile derlenmiş `AnimeUP.Installer.exe` bulunur.
   - **Kurulum Akışı:** Kullanıcı ZIP dosyasını dilediği bir klasöre açar ve `AnimeUP.Installer.exe` dosyasını çalıştırır. Bu yükleyici:
     - Bulunduğu klasörü temel alarak Windows Kayıt Defteri'ne (Registry) Native Messaging hostunu kaydeder (`HKCU\Software\Google\Chrome\NativeMessagingHosts\com.animeup.nativehost` ve Microsoft Edge için `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.animeup.nativehost`).
     - Eklenti ID'sini kurulum sırasında kullanıcıdan alarak manifest JSON şablonunu günceller.
     - Chrome eklentisinin yerel hosta erişebilmesi için gerekli izin tanımlamalarını yapar.
     - MPV ve shader yapılandırma dosyalarını (`mpv.conf`, `input.conf`) `%APPDATA%\mpv` dizinine kopyalar veya linkler.e\NativeMessagingHosts\com.animeup.nativehost`).
     - Chrome eklentisinin yerel hosta erişebilmesi için gerekli izin tanımlamalarını yapar.
     - MPV ve shader yapılandırma dosyalarını (`mpv.conf`, `input.conf`) `%APPDATA%\mpv` dizinine kopyalar veya linkler.

5. **Otomatik Anime4K Aktifleştirme:**
   - **Mantık:** MPV başlatıldığında video çözünürlüğü algılanır. Eğer video dikey çözünürlüğü `< 1080` (örneğin 480p veya 720p) ise, **Anime4K Mode A** (SD/HD -> 4K upscaler) otomatik olarak devreye sokulur. Kullanıcı `CTRL+0` ile shader'ları tamamen kapatabilir veya `CTRL+1` - `CTRL+3` kısayollarıyla farklı modlar arasında geçiş yapabilir.

---

## BÖLÜM 2: MİMARİ VE TEKNOLOJİ YIĞINI

### 2.1 Genel Mimari Şema

Sistemin veri ve kontrol akışı aşağıdaki gibidir:

```mermaid
graph TD
    subgraph Tarayıcı Katmanı (Chrome/Edge)
        A[Anime İzleme Sayfası] -->|Ağ Trafiği / DOM| B[AnimeUP Eklentisi - Content/Background]
        B -->|Kullanıcı Tıklaması| C[Native Messaging API]
    end

    subgraph OS Katmanı (Windows Registry)
        C -->|JSON stdin ile tetikleme| D[AnimeUP.NativeHost.exe]
    end

    subgraph Medya Oynatıcı Katmanı
        D -->|Process.Start + Args| E[MPV Media Player]
        E -->|Stream Çözümleme| F[yt-dlp Engine]
        F -->|Video Akışı| E
        E -->|GPU Shader Pipeline| G[Anime4K Shaders]
        E -->|Modern Arayüz| H[uosc UI Control]
    end

    G -->|Upscaled Görüntü| I[Kullanıcı Ekranı]
```

### 2.2 Teknoloji Yığını Tablosu

| Katman | Teknoloji / Kütüphane | Versiyon | Görevi ve Detayları |
|---|---|---|---|
| **Frontend (Tarayıcı)** | JavaScript / HTML / CSS | ES6+ / Manifest V3 | Video URL yakalama, kullanıcı arayüzü tetikleme. |
| **İletişim Köprüsü** | Chrome Native Messaging | MV3 standardı | Tarayıcı sandbox'ından çıkıp yerel işletim sistemiyle güvenli JSON iletişimi kurma. |
| **Masaüstü Host** | .NET C# Console Application | .NET 8.0 veya 9.0 | Standart Input/Output (stdin/stdout) akışını dinleme, JSON paketlerini byte düzeyinde okuma ve MPV'yi parametrelerle başlatma. |
| **Medya Oynatıcı** | MPV Player | 0.38+ | Yüksek performanslı donanım hızlandırmalı video oynatıcı. |
| **Çözümleyici Motoru** | yt-dlp | Güncel sürüm | Şifreli, token'lı veya gizli iframe video URL'lerini çözümleme. |
| **Görüntü İşleme** | Anime4K GLSL Shaders | v4.0.1 | GPU fragment shader'ları kullanarak çizgi keskinleştirme ve AI upscale yapma. |
| **Oynatıcı Arayüzü** | uosc (Lua Script) | 5.0+ | MPV için şık, yarı saydam ve modern kontrol menüsü/arayüzü. |
| **Yükleyici / Kurulum** | .NET C# Console Installer | .NET 8.0 / 9.0 | Windows Registry kayıtları, dosya kopyalama ve çevre değişkeni doğrulamaları. |

---

## BÖLÜM 3: COMPONENT DETAYLARI VE VERİ PROTOKOLLERİ

### 3.1 Chrome Native Messaging Protokolü

Chrome Native Messaging protokolü, standart CLI girdi/çıktı akışlarından (stdin/stdout) farklıdır. İletilen her JSON mesajının başında, mesajın karakter uzunluğunu belirten **4 byte'lık binary bir tamsayı (32-bit integer, Native Endian)** bulunur.

```
┌───────────────────────────┬──────────────────────────────────┐
│   Mesaj Uzunluğu (4 Byte)  │      JSON Verisi (UTF-8 String)  │
│  (Örn: 0x0000001B = 27)   │  {"url":"https://...","title":""}│
└───────────────────────────┴──────────────────────────────────┘
```

#### Mesaj Şemaları (Schema)

**1. Extension -> Native Host İstek Mesajı (PlayVideo):**
```json
{
  "action": "play",
  "url": "https://video.host.com/stream/file.m3u8",
  "pageUrl": "https://www.turkanime.tv/anime/naruto-1-bolum",
  "title": "Naruto - Bölüm 1",
  "referer": "https://www.turkanime.tv/"
}
```

**2. Native Host -> Extension Yanıt Mesajı (StatusResponse):**
```json
{
  "success": true,
  "message": "MPV process started successfully with PID 14202",
  "pid": 14202
}
```

### 3.2 Windows Registry Kayıt Defteri Entegrasyonu

Native Messaging hostunun Chrome tarafından tanınabilmesi için Windows Registry'ye kaydedilmesi zorunludur.

- **Anahtar Yolu:** `HKEY_CURRENT_USER\Software\Google\Chrome\NativeMessagingHosts\com.animeup.nativehost`
- **Varsayılan Değer (Default):** `D:\AnimeUP\app\com.animeup.nativehost.json` (Manifest dosyasının mutlak yolu)

Aynı kayıt Edge tarayıcısı için de şu yola yazılmalıdır:
- **Edge Anahtar Yolu:** `HKEY_CURRENT_USER\Software\Microsoft\Edge\NativeMessagingHosts\com.animeup.nativehost`

#### com.animeup.nativehost.json İçeriği:
```json
{
  "name": "com.animeup.nativehost",
  "description": "AnimeUP Chrome Extension Companion Host",
  "path": "AnimeUP.NativeHost.exe",
  "type": "stdio",
  "allowed_origins": [
    "chrome-extension://[EXTENSION_ID]/"
  ]
}
```

### 3.3 MPV Parametre Oluşturma Algoritması

C# Host, eklentiden gelen JSON verisine dayanarak MPV'yi aşağıdaki argüman dizisiyle başlatır:

```csharp
var arguments = new List<string>
{
    // Video akış URL'si
    $"\"{request.Url}\"",
    
    // Pencere Başlığı
    $"--title=\"AnimeUP - {request.Title}\"",
    
    // HTTP Headers (CORS engellerini aşmak için referer ve user-agent taklidi)
    $"--http-header-fields=Referer: {request.Referer},User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
    
    // Donanım hızlandırma ve render API'si
    "--vo=gpu-next",
    "--gpu-api=vulkan", // Desteklenmiyorsa d3d11 veya opengl'e düşer
    "--hwdec=auto-safe",
    
    // Yükleme sırasında video bilgilerini ekrana basma
    "--osd-level=1",
    
    // Anime4K ve uosc yapılandırması
    "--config=yes",
    $"--config-dir=\"{MpvConfigDirectoryPath}\""
};
```

---

## BÖLÜM 4: DETAYLI DOSYA AĞACI VE AÇIKLAMALARI

Aşağıda projenin tamamlanmış halindeki klasör ve dosya yapısı en ince ayrıntısına kadar listelenmiştir.

```
D:\AnimeUP\
├── AnimeUP.sln                        # .NET Solution dosyası
├── LICENSE                            # Lisans belgesi
├── README.md                          # Genel beni oku dosyası
│
├── docs/                              # Dokümantasyonlar
│   └── developer_document.md          # Bu döküman
│
├── dist/                              # Derlenmiş ve paketlenmeye hazır çıktılar
│   └── AnimeUP-v1.0.zip               # Dağıtıma hazır portable paket
│       ├── AnimeUP.NativeHost.exe     # Derlenmiş Native Host uygulaması
│       ├── AnimeUP.Installer.exe      # Kurulum motoru uygulaması
│       ├── yt-dlp.exe                 # YouTube-DL çatalı video URL çözümleyici (installer tarafından mpv klasörüne kopyalanır)
│       └── mpv/                       # Dağıtılacak taşınabilir MPV oynatıcı klasörü
│           ├── mpv.exe                # MPV ana yürütülebilir dosyası
│           └── ...
│
└── src/                               # Kaynak kodlar
    │
    ├── AnimeUP.Extension/             # Chrome Extension (Manifest V3)
    │   ├── manifest.json              # Eklenti manifest dosyası
    │   ├── background.js              # Background Service Worker (Ağ isteklerini yakalama ve Native Host iletişimi)
    │   ├── content.js                 # Sayfa DOM'unu inceleyen ve video etiketlerini yakalayan script
    │   ├── popup.html                 # Eklenti ikona tıklanınca açılan modern popup arayüzü
    │   ├── popup.css                  # Popup arayüzü görsel tasarımı (Glassmorphism & Dark Mode)
    │   ├── popup.js                   # Popup etkileşim mantığı ve oynatma butonu tetikleyicisi
    │   ├── logs.html                  # Logların sorgulanıp listeleneceği arayüz paneli
    │   ├── logs.css                   # Log arayüzü glassmorphism / dark mod tasarımı
    │   ├── logs.js                    # Log arayüzü filtreleme ve Native Host ile sorgu mantığı
    │   └── assets/                    # Eklenti ikonları ve görseller
    │       ├── icon16.png             # 16x16 eklenti ikonu
    │       ├── icon48.png             # 48x48 eklenti ikonu
    │       └── icon128.png            # 128x128 eklenti ikonu
    │
    ├── AnimeUP.NativeHost/            # Yerel C# Köprü Uygulaması (.NET 8/9 Console App)
    │   ├── AnimeUP.NativeHost.csproj  # C# Proje dosyası (Self-contained, trimmed optimizasyonları ile)
    │   ├── Program.cs                 # Ana giriş noktası. Sonsuz stdin dinleme döngüsü
    │   ├── com.animeup.nativehost.json# Chrome Native Messaging manifest şablonu
    │   ├── Interfaces/
    │   │   └── ILogService.cs         # Loglama servis arayüzü (Endpoint ve Fonksiyon takibi)
    │   ├── Services/
    │   │   └── LogManager.cs          # SQLite loglama servisi gerçeklemesi
    │   ├── Data/
    │   │   └── DbInitializer.cs       # İlk başlangıçta veritabanını ve tablolarını oluşturan sınıf
    │   ├── Models/
    │   │   ├── PlayRequest.cs         # Eklentiden gelen JSON şeması modeli
    │   │   └── StatusResponse.cs      # Eklentiye yollanan yanıt modeli
    │   └── Helpers/
    │       ├── NativeMessaging.cs     # Stdin/Stdout üzerinden 4-byte uzunluk önekli okuma/yazma sınıfı
    │       ├── MpvLauncher.cs         # MPV process'ini güvenli argümanlarla başlatan sınıf
    │       ├── Logger.cs              # Hata ayıklama için yerel log dosyası yazıcı (%TEMP%\AnimeUP\host.log)
    │       └── yt-dlp.exe             # YouTube-DL çatalı video URL çözümleyici (MPV dizinine kopyalanır)
    │
    ├── AnimeUP.Installer/             # C# Kurulum ve Yapılandırma Motoru (.NET 8/9 Console App)
    │   ├── AnimeUP.Installer.csproj   # Kurulum projesi dosyası
    │   ├── Program.cs                 # Registry yazma, klasör oluşturma ve entegrasyonu sağlayan installer kodu
    │   └── Assets/
    │       └── (Gömülü kaynaklar: mpv.conf, input.conf şablonları)
    │
    └── mpv-config/                    # Dağıtılacak varsayılan MPV yapılandırma şablonları
        ├── mpv.conf                   # Donanım hızlandırma ve cache ayarları
        ├── input.conf                 # Kısayollar ve Anime4K shader tetikleyicileri
        ├── scripts/
        │   └── uosc.lua               # Modern uosc arayüz lua scripti
        ├── script-opts/
        │   └── uosc.conf              # uosc özelleştirme ayarları
        └── shaders/                   # Anime4K GLSL shader dosyaları
            ├── Anime4K_Clamp_Highlights.glsl
            ├── Anime4K_Restore_CNN_VL.glsl
            ├── Anime4K_Upscale_CNN_x2_VL.glsl
            ├── Anime4K_AutoDownscalePre_x2.glsl
            ├── Anime4K_AutoDownscalePre_x4.glsl
            └── Anime4K_Upscale_CNN_x2_M.glsl
```

---

## BÖLÜM 5: ADIM ADIM İMPLEMENTASYON PLANI

### FAZ 1: Çekirdek Yapı ve MPV Yapılandırması (Süre: 2 Gün)

Bu fazda, projenin temelini oluşturan video oynatıcı altyapısı ve Anime4K entegrasyonu yerel olarak kurulup test edilecektir.

#### Adım 1.1: MPV ve Shaders Dizin Şablonunun Kurulması
- `D:\AnimeUP\mpv-config` klasörü oluşturulur.
- Anime4K v4.0.1 shader dosyaları indirilir ve `mpv-config/shaders` altına yerleştirilir.
- Modern `uosc` arayüzü indirilir ve `mpv-config/scripts/uosc.lua` olarak yerleştirilir.

#### Adım 1.2: `mpv.conf` Dosyasının Detaylı Yapılandırılması
Oynatıcının donanımı maksimum verimle kullanması için şu ayarlar `mpv.conf` içerisine yazılır:
```ini
# Render altyapısı ve donanım hızlandırma (Dahili GPU çökme korumalı fallback sırası)
vo=gpu-next
gpu-api=vulkan,d3d11,opengl
hwdec=auto-safe

# Yüksek kaliteli ölçeklendirme algoritmaları (Shader'lar kapalıyken bile etkilidir)
scale=ewa_lanczossharp
cscale=ewa_lanczossharp
dscale=mitchell

# Cache ve Buffer ayarları (Online stream akışlarında donmayı engeller)
cache=yes
cache-secs=300
demuxer-max-bytes=150MiB
demuxer-max-back-bytes=50MiB

# Arayüzü gizle (uosc kendi arayüzünü çizecek)
osc=no
border=no
```

#### Adım 1.3: `input.conf` Kısayol Dosyasının Yapılandırılması
Kullanıcının Anime4K modlarını klavyeden dinamik olarak açıp kapatması sağlanır:
```ini
# Anime4K Shader Profil Geçişleri
CTRL+1 no-osd change-list glsl-shaders set "~~/shaders/Anime4K_Clamp_Highlights.glsl;~~/shaders/Anime4K_Restore_CNN_VL.glsl;~~/shaders/Anime4K_Upscale_CNN_x2_VL.glsl;~~/shaders/Anime4K_AutoDownscalePre_x2.glsl;~~/shaders/Anime4K_AutoDownscalePre_x4.glsl;~~/shaders/Anime4K_Upscale_CNN_x2_M.glsl"; show-text "Anime4K: Mode A (SD/HD -> 4K)"
CTRL+2 no-osd change-list glsl-shaders set "~~/shaders/Anime4K_Clamp_Highlights.glsl;~~/shaders/Anime4K_Restore_CNN_Soft_VL.glsl;~~/shaders/Anime4K_Upscale_CNN_x2_VL.glsl;~~/shaders/Anime4K_AutoDownscalePre_x2.glsl;~~/shaders/Anime4K_AutoDownscalePre_x4.glsl;~~/shaders/Anime4K_Upscale_CNN_x2_M.glsl"; show-text "Anime4K: Mode B (HD -> 4K)"
CTRL+3 no-osd change-list glsl-shaders set "~~/shaders/Anime4K_Clamp_Highlights.glsl;~~/shaders/Anime4K_Denoise_Bilateral_Mode.glsl;~~/shaders/Anime4K_Upscale_CNN_x2_VL.glsl;~~/shaders/Anime4K_AutoDownscalePre_x2.glsl;~~/shaders/Anime4K_AutoDownscalePre_x4.glsl;~~/shaders/Anime4K_Upscale_CNN_x2_M.glsl"; show-text "Anime4K: Mode C (Denoise & Upscale)"
CTRL+0 no-osd change-list glsl-shaders clr ""; show-text "Anime4K: KAPALI"

# uosc Menü Tetikleyicileri
MBTN_RIGHT script-binding uosc/menu
```

---

### FAZ 2: Chrome Extension (Manifest V3) Geliştirme (Süre: 3 Gün)

Tarayıcıda izlenen animenin video linkini yakalayan ve kullanıcıya bunu tek tıkla MPV'de açma imkanı sunan eklenti kodlanacaktır.

#### Adım 2.1: `manifest.json` Oluşturulması
Eklentinin izinleri ve bileşenleri Manifest V3 standardında tanımlanır:
```json
{
  "manifest_version": 3,
  "name": "AnimeUP - Real-Time Anime Upscaler",
  "version": "1.0.0",
  "description": "İzlediğiniz animeleri tek tıkla yerel MPV oynatıcısında Anime4K shader'ları ile izleyin.",
  "key": "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAzq19t1fD56N8l293J1849a93lDfl3df4dfljkls... (Eklenti ID'sini sabitlemek için public key)",
  "permissions": [
    "webRequest",
    "nativeMessaging",
    "activeTab",
    "declarativeNetRequest",
    "storage"
  ],
  "host_permissions": [
    "<all_urls>"
  ],
  "background": {
    "service_worker": "background.js"
  },
  "action": {
    "default_popup": "popup.html",
    "default_icon": {
      "16": "assets/icon16.png",
      "48": "assets/icon48.png",
      "128": "assets/icon128.png"
    }
  },
  "icons": {
    "16": "assets/icon16.png",
    "48": "assets/icon48.png",
    "128": "assets/icon128.png"
  }
}
```

#### Adım 2.2: `background.js` Video Sniffing ve Native Bridge Yazılması
Background script, sayfa yüklenirken giden ağ isteklerini tarar ve video akışlarını önbelleğe alır. Kullanıcı butona bastığında Native Host'a gönderir.

```javascript
// Tab ID bazlı video tespitlerini saklamak için chrome.storage.session kullanılır (MV3 Service Worker uyku kaybını önler)

// Ağ isteklerini dinle (M3U8, MP4, MPD veya Video MIME tipleri)
chrome.webRequest.onBeforeRequest.addListener(
    (details) => {
        const url = details.url;
        const tabId = details.tabId;
        
        // Video uzantı ve format kontrolü (Non-blocking webRequest MV3 sniffing)
        if (url.includes(".m3u8") || url.includes(".mp4") || url.includes(".mpd") || url.includes("/video/")) {
            const data = {};
            data[`video_${tabId}`] = {
                url: url,
                timestamp: Date.now()
            };
            chrome.storage.session.set(data);
        }
    },
    { urls: ["<all_urls>"] }
);

// Tab kapandığında veriyi temizle
chrome.tabs.onRemoved.addListener((tabId) => {
    chrome.storage.session.remove([`video_${tabId}`]);
});

// Popup'tan veya content script'ten gelen mesajları dinle
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.action === "getDetectedVideo") {
        const tabId = message.tabId;
        chrome.storage.session.get([`video_${tabId}`], (result) => {
            sendResponse({ video: result[`video_${tabId}`] || null });
        });
        return true; // Asenkron yanıt
    } else if (message.action === "videoDetectedFromDOM") {
        const tabId = sender.tab.id;
        const data = {};
        data[`video_${tabId}`] = {
            url: message.url,
            timestamp: Date.now()
        };
        chrome.storage.session.set(data);
        return false;
    } else if (message.action === "playWithMpv") {
        playVideoOnLocalPlayer(message.data, sendResponse);
        return true; // Asenkron yanıt için true dönüyoruz
    }
});

// Native Messaging Host ile bağlantı kuran fonksiyon
function playVideoOnLocalPlayer(videoData, sendResponse) {
    const hostName = "com.animeup.nativehost";
    
    chrome.runtime.sendNativeMessage(hostName, videoData, (response) => {
        if (chrome.runtime.lastError) {
            sendResponse({ success: false, error: chrome.runtime.lastError.message });
        } else {
            sendResponse({ success: true, response: response });
        }
    });
}
```

#### Adım 2.2.1: `content.js` DOM Video Yakalama ve Takip Scripti
React/Vue gibi SPA tabanlı sitelerde video veya iframe elementleri DOM'a dinamik olarak sonradan enjekte edilmektedir. Bu elementleri anlık yakalayabilmek için `MutationObserver` ve fallback DOM tarama mekanizması eklenmiştir:

```javascript
// src/AnimeUP.Extension/content.js

function scanForVideos() {
    // 1. Doğrudan video etiketlerini tara
    const videoElements = document.querySelectorAll("video");
    for (const video of videoElements) {
        if (video.src && video.src.startsWith("http")) {
            sendVideoDetected(video.src);
            return;
        }
        const sources = video.querySelectorAll("source");
        for (const src of sources) {
            if (src.src && src.src.startsWith("http")) {
                sendVideoDetected(src.src);
                return;
            }
        }
    }

    // 2. Gömülü iframe oynatıcıları tara (Mixdrop, Streamtape, Sibnet, Fembed vb.)
    const iframes = document.querySelectorAll("iframe");
    for (const iframe of iframes) {
        const src = iframe.src || iframe.getAttribute("data-src");
        if (src && (src.includes("streamtape") || src.includes("sibnet") || src.includes("mixdrop") || src.includes("fembed") || src.includes("openani"))) {
            sendVideoDetected(src);
            return;
        }
    }
}

function sendVideoDetected(url) {
    chrome.runtime.sendMessage({
        action: "videoDetectedFromDOM",
        url: url
    });
}

// Sayfa ilk yüklendiğinde tara
scanForVideos();

// Dinamik DOM güncellemelerini MutationObserver ile takip et (SPA Desteği)
const observer = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
        if (mutation.addedNodes.length > 0) {
            scanForVideos();
        }
    }
});

observer.observe(document.body, {
    childList: true,
    subtree: true
});

```

#### Adım 2.3: Modern Popup UI (`popup.html`, `popup.css`, `popup.js`)
Eklenti ikonu tıklandığında açılan modern, dark theme ve glassmorphism esintili arayüz.

**popup.html:**
```html
<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="UTF-8">
    <link rel="stylesheet" href="popup.css">
</head>
<body>
    <div class="card">
        <h2 class="title">Anime<span class="highlight">UP</span></h2>
        <div id="status-container" class="status-container">
            <span class="status-dot orange"></span>
            <span id="status-text">Video Aranıyor...</span>
        </div>
        <div class="video-info" id="video-info" style="display: none;">
            <p class="label">Yakalanan Akış:</p>
            <p class="url-text" id="detected-url"></p>
        </div>
        <button id="play-btn" class="play-btn" disabled>
            <svg class="play-icon" viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>
            AnimeUP ile İzle
        </button>
    </div>
    <script src="popup.js"></script>
</body>
</html>
```

**popup.css:**
```css
body {
    width: 300px;
    margin: 0;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    background: #0d0e12;
    color: #e2e8f0;
}
.card {
    padding: 20px;
    background: linear-gradient(135deg, rgba(26,27,38,0.9) 0%, rgba(17,18,26,0.95) 100%);
    box-shadow: 0 8px 32px 0 rgba(0, 0, 0, 0.37);
    border: 1px solid rgba(255, 255, 255, 0.05);
}
.title {
    margin: 0 0 15px 0;
    font-size: 22px;
    letter-spacing: 1px;
    text-align: center;
}
.highlight {
    color: #3b82f6;
    font-weight: bold;
    text-shadow: 0 0 10px rgba(59, 130, 246, 0.5);
}
.status-container {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    margin-bottom: 20px;
    background: rgba(255,255,255,0.02);
    padding: 10px;
    border-radius: 8px;
}
.status-dot {
    width: 10px;
    height: 10px;
    border-radius: 50%;
}
.status-dot.green { background: #10b981; box-shadow: 0 0 8px #10b981; }
.status-dot.orange { background: #f59e0b; box-shadow: 0 0 8px #f59e0b; }
.play-btn {
    width: 100%;
    background: #3b82f6;
    color: white;
    border: none;
    padding: 12px;
    border-radius: 8px;
    font-weight: bold;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    transition: all 0.3s ease;
}
.play-btn:hover:not(:disabled) {
    background: #2563eb;
    box-shadow: 0 0 15px rgba(59,130,246,0.4);
}
.play-btn:disabled {
    background: #1e293b;
    color: #64748b;
    cursor: not-allowed;
}
.play-icon {
    width: 18px;
    height: 18px;
    fill: currentColor;
}
```

#### Adım 2.4: `popup.js` ve Log Panel Arayüz Javascript Dosyalarının Yazılması
Popup arayüzü ile background script arasındaki mesajlaşmayı sağlayan ve yakalanan videoları listeleyen `popup.js` kodu:

```javascript
// src/AnimeUP.Extension/popup.js
document.addEventListener("DOMContentLoaded", () => {
    const playBtn = document.getElementById("play-btn");
    const statusText = document.getElementById("status-text");
    const statusDot = document.querySelector(".status-dot");
    const videoInfo = document.getElementById("video-info");
    const detectedUrl = document.getElementById("detected-url");

    let currentVideo = null;

    // Aktif tab'daki tespit edilen videoyu al
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
        const activeTab = tabs[0];
        if (!activeTab) return;

        chrome.runtime.sendMessage(
            { action: "getDetectedVideo", tabId: activeTab.id },
            (response) => {
                if (response && response.video) {
                    currentVideo = response.video;
                    statusDot.className = "status-dot green";
                    statusText.textContent = "Video Yakalandı!";
                    videoInfo.style.display = "block";
                    detectedUrl.textContent = currentVideo.url;
                    playBtn.disabled = false;
                } else {
                    statusDot.className = "status-dot orange";
                    statusText.textContent = "Video Aranıyor...";
                    playBtn.disabled = true;
                }
            }
        );
    });

    // Oynatma butonuna basılınca
    playBtn.addEventListener("click", () => {
        if (!currentVideo) return;

        playBtn.disabled = true;
        statusText.textContent = "MPV Başlatılıyor...";

        chrome.runtime.sendMessage(
            {
                action: "playWithMpv",
                data: {
                    action: "play",
                    url: currentVideo.url,
                    title: "AnimeUP Stream",
                    referer: window.location.origin
                }
            },
            (response) => {
                if (response && response.success) {
                    statusText.textContent = "Oynatılıyor!";
                } else {
                    statusText.textContent = "Hata: " + (response.error || "Bağlantı başarısız");
                    playBtn.disabled = false;
                }
            }
        );
    });
});
```

---

### FAZ 3: C# Native Messaging Host Geliştirme (Süre: 4 Gün)

Tarayıcıdan gelen talepleri dinleyip MPV oynatıcısını yöneten C# uygulaması yazılacaktır.

#### Adım 3.1: Proje Oluşturma ve Ayarlar
- `src/AnimeUP.NativeHost` altında bir .NET 8.0/9.0 Console projesi oluşturulur.
- `.csproj` dosyası trimmed ve self-contained ayarları ile optimize edilir:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <PublishTrimmed>true</PublishTrimmed>
    <PublishReadyToRun>true</PublishReadyToRun>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.3" />
  </ItemGroup>
</Project>
```

#### Adım 3.2: `Program.cs` ve Stdin Okuyucu Algoritması
Chromium standart input verisini okuyabilen ana program döngüsü.

```csharp
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using AnimeUP.NativeHost.Models;
using AnimeUP.NativeHost.Helpers;

namespace AnimeUP.NativeHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger.Log("Native Host started.");
            
            // yt-dlp güncelleme kontrolünü arka planda başlat (Auto-update mekanizması)
            UpdateYtDlp();

            try
            {
                // Chrome Native Messaging Host sonsuz döngüde stdin dinlemelidir
                while (true)
                {
                    var request = ReadMessage();
                    if (request == null) break;

                    Logger.Log($"Received play request: {request.Url}");

                    // MPV Oynatıcıyı Başlat
                    var pid = MpvLauncher.Launch(request);

                    // Yanıtı Hazırla ve Gönder
                    var response = new StatusResponse
                    {
                        Success = pid > 0,
                        Message = pid > 0 ? $"MPV launched with PID {pid}" : "Failed to launch MPV",
                        Pid = pid
                    };
                    
                    WriteMessage(response);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Critical Error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static PlayRequest? ReadMessage()
        {
            var stdin = Console.OpenStandardInput();
            
            // İlk 4 byte'ı oku (Mesaj boyutu)
            byte[] lengthBuffer = ReadFully(stdin, 4);
            if (lengthBuffer.Length < 4) return null;

            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            
            // JSON verisini oku
            byte[] messageBuffer = ReadFully(stdin, messageLength);
            if (messageBuffer.Length != messageLength) return null;

            string jsonString = Encoding.UTF8.GetString(messageBuffer);
            return JsonSerializer.Deserialize<PlayRequest>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private static byte[] ReadFully(Stream stream, int length)
        {
            byte[] buffer = new byte[length];
            int totalRead = 0;
            while (totalRead < length)
            {
                int read = stream.Read(buffer, totalRead, length - totalRead);
                if (read <= 0) break;
                totalRead += read;
            }
            if (totalRead < length)
            {
                Array.Resize(ref buffer, totalRead);
            }
            return buffer;
        }

        private static byte[] ReadFully(Stream stream, int length)
        {
            byte[] buffer = new byte[length];
            int totalRead = 0;
            while (totalRead < length)
            {
                int read = stream.Read(buffer, totalRead, length - totalRead);
                if (read <= 0) break;
                totalRead += read;
            }
            if (totalRead < length)
            {
                Array.Resize(ref buffer, totalRead);
            }
            return buffer;
        }

        private static void UpdateYtDlp()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string ytDlpPath = Path.Combine(appData, "AnimeUP", "mpv", "yt-dlp.exe");
                    if (File.Exists(ytDlpPath))
                    {
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = ytDlpPath,
                            Arguments = "-U",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        var proc = System.Diagnostics.Process.Start(startInfo);
                        proc?.WaitForExit();
                        Logger.Log("yt-dlp auto-update check completed.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"yt-dlp auto-update check failed: {ex.Message}");
                }
            });
        }

        private static void WriteMessage(StatusResponse response)
        {
            var stdout = Console.OpenStandardOutput();
            string jsonString = JsonSerializer.Serialize(response);
            byte[] messageBuffer = Encoding.UTF8.GetBytes(jsonString);
            byte[] lengthBuffer = BitConverter.GetBytes(messageBuffer.Length);

            // Önce 4 byte boyutu yaz, ardından JSON'ı yaz
            stdout.Write(lengthBuffer, 0, 4);
            stdout.Write(messageBuffer, 0, messageBuffer.Length);
            stdout.Flush();
        }
    }
}
```

#### Adım 3.3: `MpvLauncher.cs` ile Alt Süreç Yönetimi
MPV oynatıcısının sistemde bulunup bulunmadığını denetleyen ve doğru shader yapılandırmasıyla çalıştıran sınıf.

```csharp
using System.Diagnostics;
using System.IO;
using AnimeUP.NativeHost.Models;

namespace AnimeUP.NativeHost.Helpers
{
    public static class MpvLauncher
    {
        public static int Launch(PlayRequest request)
        {
            // Oynatıcı yolları belirlenir
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string animeUpDir = Path.Combine(appData, "AnimeUP");
            string mpvExePath = Path.Combine(animeUpDir, "mpv", "mpv.exe");
            string configDirPath = Path.Combine(animeUpDir, "mpv-config");

            // Eğer lokal MPV yoksa sistem PATH'indekini dene
            if (!File.Exists(mpvExePath))
            {
                mpvExePath = "mpv.exe"; // Path'ten bulması için
            }

            // Önceki MPV süreçlerini sonlandır (Tek instance kontrolü)
            try
            {
                var existingProcesses = Process.GetProcessesByName("mpv");
                foreach (var proc in existingProcesses)
                {
                    proc.Kill();
                }
            }
            catch { }

            var startInfo = new ProcessStartInfo
            {
                FileName = mpvExePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Argüman listesini hazırla
            startInfo.ArgumentList.Add(request.Url);
            startInfo.ArgumentList.Add($"--title=AnimeUP - {request.Title}");
            startInfo.ArgumentList.Add($"--config-dir={configDirPath}");
            
            // HTTP Header taklitlerini ekle
            if (!string.IsNullOrEmpty(request.Referer))
            {
                startInfo.ArgumentList.Add($"--http-header-fields=Referer: {request.Referer},User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            }

            try
            {
                var process = Process.Start(startInfo);
                return process?.Id ?? -1;
            }
            catch (Exception ex)
            {
                Logger.Log($"Launch Exception: {ex.Message}");
                return -1;
            }
        }
    }
}
```

---

### FAZ 4: Kurulum ve Yapılandırma Yöneticisi (Süre: 2 Gün)

Bu aşamada kullanıcının sıfır eforla projeyi bilgisayarına kaydetmesi için C# Installer Console aracı kodlanacaktır.

#### Adım 4.1: `Program.cs` Kurulum Algoritması
Yükleyici, çalıştırıldığı klasörü kaynak alarak sistemdeki Chrome ve Edge için Native Host JSON dosyasını oluşturur ve Windows Registry kayıtlarını yazar.

```csharp
using System;
using System.IO;
using Microsoft.Win32;

namespace AnimeUP.Installer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== AnimeUP Kurulum Yapılandırıcı ===");
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string animeUpDir = Path.Combine(appData, "AnimeUP");

                // Gerekli klasörleri oluştur
                Directory.CreateDirectory(animeUpDir);
                
                // Kaynakları kopyala
                Console.WriteLine("Dosyalar kopyalanıyor...");
                CopyDirectory(Path.Combine(baseDir, "mpv-config"), Path.Combine(animeUpDir, "mpv-config"));
                
                Console.Write("Lütfen Chrome/Edge Eklenti ID'sini girin (chrome://extensions adresinden alabilirsiniz): ");
                string? extensionId = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(extensionId))
                {
                    extensionId = "com.animeup.extension"; // Yedek varsayılan şablon değeri
                }

                string hostExePath = Path.Combine(baseDir, "AnimeUP.NativeHost.exe");
                string manifestPath = Path.Combine(animeUpDir, "com.animeup.nativehost.json");

                string manifestJson = $@"{{
  ""name"": ""com.animeup.nativehost"",
  ""description"": ""AnimeUP Chrome Companion Host"",
  ""path"": ""{hostExePath.Replace("\\", "\\\\")}"",
  ""type"": ""stdio"",
  ""allowed_origins"": [
    ""chrome-extension://{extensionId}/""
  ]
}}";
                File.WriteAllText(manifestPath, manifestJson);

                // Windows Registry'ye Kayıt Yap
                Console.WriteLine("Registry anahtarları yazılıyor...");
                RegisterHost("Google\\Chrome", manifestPath);
                RegisterHost("Microsoft\\Edge", manifestPath);

                Console.WriteLine("\nKurulum BAŞARIYLA tamamlandı!");
                Console.WriteLine("Şimdi Chrome/Edge tarayıcınıza eklentiyi yükleyebilirsiniz.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nHata oluştu: {ex.Message}");
                Console.ResetColor();
            }
            Console.ReadLine();
        }

        private static void RegisterHost(string browserPath, string manifestPath)
        {
            string keyPath = $@"Software\{browserPath}\NativeMessagingHosts\com.animeup.nativehost";
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                key.SetValue("", manifestPath);
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) return;

            Directory.CreateDirectory(destinationDir);
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string targetDirPath = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, targetDirPath);
            }
        }
    }
}
```

### FAZ 5: Detaylı Loglama Sistemi (Süre: 3 Gün)

Uygulamanın çalışmasını, gelen Native Messaging isteklerini ve her iç fonksiyonun girdisini, çıktısını ve çalışma süresini yerel bir SQLite veritabanına kaydeden sistem kurulacaktır.

#### Adım 5.1: Projeye SQLite Desteği Eklenmesi
`src/AnimeUP.NativeHost/AnimeUP.NativeHost.csproj` dosyasına **Microsoft.Data.Sqlite** paketi eklenir:
```xml
  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.3" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.4" />
  </ItemGroup>
```

#### Adım 5.2: ILogService, DbInitializer ve LogManager Sınıflarının Yazılması
- `Interfaces/ILogService.cs` dosyası oluşturularak log servisinin metot imzaları tanımlanır.
- `Data/DbInitializer.cs` dosyası yazılır. Bu sınıf uygulama ilk ayağa kalktığında `%APPDATA%\AnimeUP\animeup.db` dosyasını oluşturur ve `endpoint_logs` ile `function_logs` tablolarını kurar.
- `Services/LogManager.cs` dosyası yazılır. Veritabanına veri ekleme (INSERT) ve filtreli sorgulama (SELECT) metotlarını barındırır.

#### Adım 5.3: Fonksiyon Sarmalayıcı (Logging Wrapper) Tasarımı
Fonksiyonların başına ve sonuna elle log yazmak yerine, C# tarafında generic bir yürütme sarmalayıcısı oluşturulur. Bu sarmalayıcı, parametreleri JSON serialize ederek girdiyi alır, fonksiyonu çalıştırır, çıktıyı yakalar, süreyi ölçer ve hata durumunda exception detaylarını SQLite'a yazar.

#### Adım 5.4: Chrome Extension Log İzleme Ekranının (`logs.html`, `logs.css`, `logs.js`) Yazılması
Eklenti kök dizinine `logs.html` eklenir. Eklentinin popup arayüzündeki ufak bir buton ile bu sayfa yeni sekmede açılır. Sayfa açıldığında Native Host'a `{"action": "getLogs"}` mesajı yollayarak SQLite'taki tüm kayıtları getirir. Arayüzde arama, tarih filtreleme ve sadece hata durumundaki logları listeleme seçenekleri bulunur.

---

## BÖLÜM 6: VERİ YAPILARI VE ALGORİTMALAR

### 6.1 Chromium Stdin Byte Parse Algoritması
Chromium Native Messaging iletişiminde en kritik nokta, standard stream girdilerini bloke olmadan ve doğru byte sıralamasıyla (little-endian) okumaktır. C# üzerinde binary akışları buffer üzerinden okurken ağ paketlerinin parça parça gelme ihtimaline karşı şu algoritma kullanılır:

```csharp
public static byte[] ReadFully(Stream stream, int length)
{
    byte[] buffer = new byte[length];
    int totalRead = 0;
    while (totalRead < length)
    {
        int read = stream.Read(buffer, totalRead, length - totalRead);
        if (read <= 0) break; // End of File / Pipe closed
        totalRead += read;
    }
    if (totalRead < length)
    {
        Array.Resize(ref buffer, totalRead);
    }
    return buffer;
}
```

### 6.2 Otomatik Çözünürlük Algılama ve Shader Enjeksiyon Algoritması
MPV içerisinde, video yüklenirken çözünürlüğü inceleyip uygun Anime4K profilini tetikleyen Lua betiği (`animeup-hook.lua`):

```lua
-- mpv-config/scripts/animeup-hook.lua
local utils = require 'mp.utils'

function on_file_loaded()
    -- Video genişlik ve yükseklik verilerini al
    local width = mp.get_property_number("width", 0)
    local height = mp.get_property_number("height", 0)
    
    if width == 0 or height == 0 then
        return -- Görüntü yoksa (ses dosyasıysa) çık
    end
    
    mp.osd_message("Çözünürlük: " .. width .. "x" .. height)
    
    -- Eğer video dikey çözünürlüğü 1080p'den düşükse SD->4K Shader'ını otomatik aktif et (4:3 en-boy oranı güvenli kontrolü)
    if height < 1080 then
        local shaders_path = mp.command_native({"expand-path", "~~/shaders/"})
        local shader_list = {
            shaders_path .. "Anime4K_Clamp_Highlights.glsl",
            shaders_path .. "Anime4K_Restore_CNN_VL.glsl",
            shaders_path .. "Anime4K_Upscale_CNN_x2_VL.glsl",
            shaders_path .. "Anime4K_AutoDownscalePre_x2.glsl",
            shaders_path .. "Anime4K_AutoDownscalePre_x4.glsl",
            shaders_path .. "Anime4K_Upscale_CNN_x2_M.glsl"
        }
        
        -- Shader listesini string olarak birleştir
        local shader_string = table.concat(shader_list, ";")
        mp.set_property("glsl-shaders", shader_string)
        mp.osd_message("AnimeUP: SD->4K Real-Time AI Upscale Aktif", 4)
    else
        mp.osd_message("AnimeUP: Video kalitesi yeterli (Shader kapalı)", 3)
    end
end

-- Dosya oynatılmaya hazır olduğunda event'i dinle
mp.register_event("file-loaded", on_file_loaded)
```

### 6.3 SQLite Loglama ve Otomatik Fonksiyon Sarmalayıcı Algoritması

#### SQLite Log Veritabanı Şeması
Loglar, SQLite veritabanındaki aşağıdaki iki tabloda saklanır:

```sql
-- Endpoint Logları (Eklentiden gelen istekler)
CREATE TABLE IF NOT EXISTS endpoint_logs (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    trace_id     TEXT NOT NULL,
    action       TEXT NOT NULL,
    req_payload  TEXT,
    res_payload  TEXT,
    duration_ms  INTEGER,
    status       TEXT,
    created_at   TEXT DEFAULT CURRENT_TIMESTAMP
);

-- Fonksiyon Logları (Metot çağrıları ve girdiler/çıktılar)
CREATE TABLE IF NOT EXISTS function_logs (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    trace_id     TEXT NOT NULL,
    class_name   TEXT,
    method_name  TEXT,
    file_path    TEXT,
    line_number  INTEGER,
    input_args   TEXT,
    output_data  TEXT,
    error_msg    TEXT,
    stack_trace  TEXT,
    duration_ms  INTEGER,
    created_at   TEXT DEFAULT CURRENT_TIMESTAMP
);
```

#### Otomatik Loglama Wrapper Gerçeklemesi
C# tarafında `CallerMemberName` ve `CallerFilePath` öznitelikleriyle çalışan sarmalayıcı sınıf metodu:

```csharp
public static async Task<T> ExecuteWithLoggingAsync<T>(
    string traceId,
    Func<Task<T>> func,
    object? inputArgs = null,
    [CallerMemberName] string methodName = "",
    [CallerFilePath] string filePath = "",
    [CallerLineNumber] int lineNumber = 0)
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    string? inputJson = inputArgs != null ? JsonSerializer.Serialize(inputArgs) : null;
    
    try
    {
        T result = await func();
        stopwatch.Stop();
        
        string? outputJson = result != null ? JsonSerializer.Serialize(result) : null;
        
        // Loglama arayüzüne asenkron yazma
        _ = Task.Run(() => _logService.LogFunctionAsync(
            traceId, inputJson, outputJson, null, null, 
            (int)stopwatch.ElapsedMilliseconds, methodName, filePath, lineNumber));
            
        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _ = Task.Run(() => _logService.LogFunctionAsync(
            traceId, inputJson, null, ex.Message, ex.StackTrace, 
            (int)stopwatch.ElapsedMilliseconds, methodName, filePath, lineNumber));
            
        throw;
    }
}
```

---

## BÖLÜM 7: TEST VE DOĞRULAMA PLANI

Projenin doğrulanması için hem otomatik birim testleri hem de manual kullanıcı senaryo testleri uygulanacaktır.

### 7.1 Otomatik Testler

1. **Native Host StdIn/StdOut Paket Testi (C# - Unit Test):**
   - **Metot:** `Test_ReadMessage_Valid_Stream_Returns_Payload`
   - **Senaryo:** Mock bir Stream nesnesine 4-byte uzunluk öneki + JSON formatlı bir `PlayRequest` yazılır. `ReadMessage` fonksiyonunun bunu doğru şekilde deserialize ettiği test edilir.
   - **Komut:** `dotnet test tests/AnimeUP.UnitTests`

2. **MPV Argüman Oluşturma Doğrulama Testi:**
   - **Metot:** `Test_MpvLauncher_Builds_Correct_Argument_List`
   - **Senaryo:** `PlayRequest` nesnesine özel header'lar ve referer atanır. Oluşan argument listesinde doğru parametrelerin tırnak işaretleriyle korunduğu onaylanır.

3. **SQLite Loglama Entegrasyon Testi:**
   - **Metot:** `Test_LogService_Writes_To_Sqlite_Correctly`
   - **Senaryo:** `LogFunctionAsync` metodu çağrıldıktan sonra SQLite veritabanı sorgulanarak kaydın başarıyla eklendiği ve girdilerin/çıktıların doğru serialize edildiği kontrol edilir.

### 7.2 Manuel Doğrulama Matrix'i (Kullanıcı Testleri)

| Site Grubu | Hedef URL Tipi | Test Adımları | Beklenen Sonuç |
|---|---|---|---|
| **Türkanime (turkanime.tv)** | Iframe / Sibnet, Streamtape | Bölüm sayfasına gir, eklenti ikonuna tıkla, oyna de. | MPV açılmalı, videoyu yt-dlp ile çözüp yüklemeli, reklamlar elenmeli. |
| **Openani (openani.me)** | Doğrudan .mp4 / .m3u8 | Bölüm açıldığında eklentiden play butonunun aktif olduğunu gör. | Video kesintisiz oynamalı, `CTRL+1` ile çizgiler anında netleşmeli. |
| **Düşük Kaliteli Kaynak** | 480p Anime Bölümü | 480p bir bölüm açıp MPV penceresinin yüklenmesini bekle. | OSD ekranında otomatik olarak "AnimeUP: SD->4K Real-Time AI Upscale Aktif" yazısı belirmeli. |
