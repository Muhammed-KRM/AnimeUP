/**
 * AnimeUP — Content Script v2.0
 * Dosya: src/AnimeUP.Extension/content.js
 *
 * Sorumluluklar:
 *  1. Sayfa DOM'unu <video>, <source> ve <iframe> etiketleri için tarar.
 *  2. Ağ trafiğinden yakalanan URL'leri de destekler (background.js ile).
 *  3. Bilinen embed player host'larını (Guaj, Alucard, Mave, Sibnet vb.) tanır.
 *  4. YouTube URL'lerini doğrudan page URL olarak iletir (yt-dlp ile oynatılır).
 *  5. SPA'larda MutationObserver ile dinamik DOM değişikliklerini izler.
 */

'use strict';

// ─── Video URL Uzantıları ───────────────────────────────────────────────────
const VIDEO_EXTENSIONS = ['.m3u8', '.mp4', '.mpd', '.mkv', '.webm', '.flv', '.ts', '.avi'];

// ─── Bilinen Embed/Iframe Player Host'ları ──────────────────────────────────
// Bunlar yt-dlp ile oynatılabilir (page URL olarak gönderilir)
const IFRAME_HOSTS = [
  // ── Global genel amaçlı video host'ları ──────────────────────────────────
  'streamtape', 'sibnet',   'mixdrop',    'fembed',     'ok.ru',
  'dailymotion','openload', 'vidoza',     'doodstream', 'filemoon',
  'streamlare', 'streamhide','gogo-stream','gogocdn',   'mp4upload',
  'sendvid',    'vudeo',    'voe.sx',     'voe.',       'upstream',
  'embedsito',  'vidplay',  'vidmoly',    'filelions',  'videovard',
  'vidhide',    'vidhidepro','uqload',    'userload',   'streamwish',
  'wishembed',  'odnoklassniki',

  // ── turkanime.tv player'ları ──────────────────────────────────────────────
  'alucard',        // ALUCARD(BETA)
  'alucarx',        // ALUCARX(BETA)
  'player.alucard', // alucard subdomain
  'banka',          // BANKA(BETA)
  'hdvid',          // HDVID
  'sunl',           // SUNL
  'cloneplayer',    // CLONE
  'volplayer',      // VOL
  'trguaj',         // Guaj varyant
  'guaj',           // Guaj player
  'turkanime',      // turkanime kendi embed'leri

  // ── tranimeizle.io / tranimaci.com player'ları ───────────────────────────
  'altrvip',        // AltrVip
  'altrvip.net',
  'babyts',         // Babyts
  'babyts.net',
  'gdrive',         // Gdrive (Google Drive embed)
  'drive.google',   // Google Drive direct
  'mave',           // Mave player
  'tranimeizle',    // kendi embed'leri
  'tranimaci',

  // ── diziwatch.ac / yabancidizi player'ları ───────────────────────────────
  'diziwatch',
  'yabancidizi',
  'clv.tr',
  'cloudup',
  'cloudvideo',

  // ── Türk anime genelinde kullanılan ──────────────────────────────────────
  'openani',        // openani.me
  'anizm',          // anizyum
  'animeciix',
  'animecix',
  'alp.',           // alp. ile başlayan player subdomainleri
  'jplayer',
  'superplayer',
  'easyvideo',

  // ── Crunchyroll / Funimation gibi lisanslı siteler ───────────────────────
  // Bu siteler yt-dlp page URL modunda ele alınıyor (YTDLP_PAGE_HOSTS)

  // ── Ek popüler embed host'lar ────────────────────────────────────────────
  'streamable',
  'rumble.com',
  'bilibili',
  'nicovideo',
  'tune.pk',
  'veoh.com',
  'rapidvideo',
  'clipwatching',
  'evoload',
  'jetload',
  'mixdrp',
  'netu.ac',
  'netuplayer',
  'vstream',
  'hydrax',
  'playtaku',
  'kwik.',          // kwik.cx ve benzeri (gogoanime için)
  'embtaku',
  'goload',
  'playtaku',
  'yugen.to',
  'gounlimited',
  'videomass',
];


// ─── YouTube / yt-dlp ile oynatılacak siteler ──────────────────────────────
// Bu sitelerde page URL'nin kendisi yt-dlp'ye verilir
const YTDLP_PAGE_HOSTS = [
  'youtube.com',
  'youtu.be',
  'nicovideo.jp',
  'bilibili.com',
  'crunchyroll.com',
  'hidive.com',
  'funimation.com',
];

const DEBOUNCE_MS = 500;

// ─── Yardımcılar ────────────────────────────────────────────────────────────
function isValidHttpUrl(url) {
  if (!url || typeof url !== 'string') return false;
  return url.startsWith('http://') || url.startsWith('https://');
}

function isVideoUrl(url) {
  if (!isValidHttpUrl(url)) return false;
  try {
    const pathname = new URL(url).pathname.toLowerCase();
    return VIDEO_EXTENSIONS.some(ext => pathname.endsWith(ext));
  } catch {
    return false;
  }
}

function isKnownIframeHost(url) {
  if (!isValidHttpUrl(url)) return false;
  const lower = url.toLowerCase();
  return IFRAME_HOSTS.some(host => lower.includes(host));
}

function isYtdlpPageHost(url) {
  if (!isValidHttpUrl(url)) return false;
  try {
    const hostname = new URL(url).hostname.toLowerCase();
    return YTDLP_PAGE_HOSTS.some(host => hostname.includes(host));
  } catch {
    return false;
  }
}

// ─── Background'a mesaj gönder ───────────────────────────────────────────────
function reportVideo(url, isIframe = false, isPageUrl = false) {
  try {
    chrome.runtime.sendMessage({
      action: 'videoDetectedFromDOM',
      url,
      isIframe,
      isPageUrl,
    });
  } catch {
    // Eklenti devre dışı veya bağlantı yoksa sessizce geç
  }
}

// ─── Mevcut sayfanın yt-dlp ile oynatılıp oynatılamayacağını kontrol et ──
function checkCurrentPageAsVideo() {
  const pageUrl = window.location.href;
  if (isYtdlpPageHost(pageUrl)) {
    reportVideo(pageUrl, false, true);
    return true;
  }
  return false;
}

// ─── DOM tarayıcı ────────────────────────────────────────────────────────────
function scanForVideos() {
  // 1. Mevcut sayfa yt-dlp ile oynatılabilir mi? (YouTube, Crunchyroll vb.)
  if (checkCurrentPageAsVideo()) return;

  // 2. Doğrudan <video> elementleri
  const videoElements = document.querySelectorAll('video');
  for (const video of videoElements) {
    const srcs = [video.src, video.currentSrc];
    for (const source of video.querySelectorAll('source')) {
      srcs.push(source.src);
    }
    for (const src of srcs) {
      if (isVideoUrl(src)) {
        reportVideo(src, false);
        return;
      }
    }
    // blob: URL'leri de kaydet (HLS.js veya Shaka Player için)
    if (video.src && video.src.startsWith('blob:')) {
      // blob URL'yi göster ama ağ isteğinden gerçek URL gelecek
      // background.js'in webRequest dinleyicisi bunu yakalar
    }
  }

  // 3. <source> elementleri (video dışında)
  const sourceEls = document.querySelectorAll('source[src]');
  for (const s of sourceEls) {
    if (isVideoUrl(s.src)) {
      reportVideo(s.src, false);
      return;
    }
  }

  // 4. iframe embed player'ları
  const iframes = document.querySelectorAll('iframe');
  for (const iframe of iframes) {
    const src = iframe.src
      || iframe.getAttribute('data-src')
      || iframe.getAttribute('data-lazy-src')
      || '';

    if (!src) continue;

    // Bilinen player host'u mu?
    if (isKnownIframeHost(src)) {
      reportVideo(src, true);
      return;
    }

    // iframe içinde video URL var mı?
    if (isVideoUrl(src)) {
      reportVideo(src, false);
      return;
    }
  }

  // 5. <script> ve data attribute'larında gizli video URL ara
  scanScriptDataAttributes();
}

// ─── Script ve data attribute tarayıcı ──────────────────────────────────────
function scanScriptDataAttributes() {
  // data-video-url, data-src, data-stream gibi attribute'lara bak
  const candidates = document.querySelectorAll('[data-video-url],[data-stream],[data-hls],[data-source],[data-file]');
  for (const el of candidates) {
    const attrs = ['data-video-url', 'data-stream', 'data-hls', 'data-source', 'data-file'];
    for (const attr of attrs) {
      const val = el.getAttribute(attr);
      if (val && isVideoUrl(val)) {
        reportVideo(val, false);
        return;
      }
    }
  }

  // JSON içinde m3u8/mp4 URL ara (inline script'lerde)
  const scripts = document.querySelectorAll('script:not([src])');
  for (const script of scripts) {
    const text = script.textContent || '';
    // m3u8 veya mp4 URL'si içeren JSON string bul
    const m3u8Match = text.match(/["'](https?:\/\/[^"']*\.m3u8[^"']*?)["']/);
    if (m3u8Match) {
      reportVideo(m3u8Match[1], false);
      return;
    }
    const mp4Match = text.match(/["'](https?:\/\/[^"']*\.mp4[^"']*?)["']/);
    if (mp4Match) {
      reportVideo(mp4Match[1], false);
      return;
    }
  }
}

// ─── Debounce yardımcısı ─────────────────────────────────────────────────────
function debounce(fn, delay) {
  let timer = null;
  return (...args) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delay);
  };
}

// ─── Başlangıç taraması ──────────────────────────────────────────────────────
scanForVideos();

// ─── MutationObserver: SPA ve dinamik içerik için sürekli izleme ────────────
const debouncedScan = debounce(scanForVideos, DEBOUNCE_MS);

const observer = new MutationObserver((mutations) => {
  const hasRelevantChanges = mutations.some(
    (m) => m.addedNodes.length > 0 || m.type === 'attributes'
  );
  if (hasRelevantChanges) {
    debouncedScan();
  }
});

function startObserver() {
  observer.observe(document.body, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ['src', 'data-src', 'data-lazy-src', 'data-video-url'],
  });
}

if (document.body) {
  startObserver();
} else {
  document.addEventListener('DOMContentLoaded', startObserver);
}
