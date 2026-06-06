/**
 * AnimeUP — Content Script
 * Dosya: src/AnimeUP.Extension/content.js
 *
 * Sorumluluklar:
 *  1. Sayfa DOM'unu <video>, <source> ve <iframe> etiketleri için tarar.
 *  2. SPA (Single Page Application) sitelerinde dinamik DOM değişikliklerini
 *     MutationObserver ile izler.
 *  3. Yakalanan URL'leri background service worker'a iletir.
 *  4. Service worker uykuya girdiğinde de veri kaybolmaz çünkü mesajlar
 *     background'u otomatik uyandırır.
 *
 * Güvenlik:
 *  - Yalnızca "http" ile başlayan URL'ler işlenir (data: / blob: atlanır).
 *  - chrome.runtime.sendMessage hataları sessizce yakalanır (eklenti disable vb.).
 */

'use strict';

// ─── Sabitler ───────────────────────────────────────────────────────────────
const VIDEO_EXTENSIONS = ['.m3u8', '.mp4', '.mpd', '.mkv', '.webm', '.flv'];
const IFRAME_HOSTS = [
  'streamtape', 'sibnet', 'mixdrop', 'fembed', 'ok.ru',
  'dailymotion', 'openload', 'vidoza', 'doodstream', 'filemoon', 'openani'
];

// MutationObserver için debounce gecikmesi (ms) — her DOM değişikliğinde
// taramayı geciktirerek performans yitimini önler.
const DEBOUNCE_MS = 400;

// ─── Yardımcı: Güvenli URL doğrulama ────────────────────────────────────────
function isValidHttpUrl(url) {
  if (!url || typeof url !== 'string') return false;
  return url.startsWith('http://') || url.startsWith('https://');
}

// ─── Yardımcı: Uzantıya göre video URL tespiti ──────────────────────────────
function isVideoUrl(url) {
  if (!isValidHttpUrl(url)) return false;
  try {
    const pathname = new URL(url).pathname.toLowerCase();
    return VIDEO_EXTENSIONS.some(ext => pathname.endsWith(ext));
  } catch {
    return false;
  }
}

// ─── Yardımcı: Bilinen iframe barındırıcı tespiti ───────────────────────────
function isKnownIframeHost(url) {
  if (!isValidHttpUrl(url)) return false;
  return IFRAME_HOSTS.some(host => url.includes(host));
}

// ─── Background'a mesaj gönder ───────────────────────────────────────────────
function reportVideo(url, isIframe = false) {
  try {
    chrome.runtime.sendMessage({
      action: 'videoDetectedFromDOM',
      url,
      isIframe
    });
  } catch {
    // Eklenti devre dışı veya bağlantı yoksa sessizce geç.
  }
}

// ─── DOM tarayıcı: tüm <video>, <source>, <iframe> elemanlarını kontrol eder ─
function scanForVideos() {
  // 1. Doğrudan <video src=""> veya <video><source src=""></video>
  const videoElements = document.querySelectorAll('video');
  for (const video of videoElements) {
    if (isVideoUrl(video.src)) {
      reportVideo(video.src, false);
      return;
    }
    for (const source of video.querySelectorAll('source')) {
      if (isVideoUrl(source.src)) {
        reportVideo(source.src, false);
        return;
      }
    }
    // currentSrc: tarayıcının seçtiği aktif kaynak
    if (isVideoUrl(video.currentSrc)) {
      reportVideo(video.currentSrc, false);
      return;
    }
  }

  // 2. Bilinen iframe barındırıcılar (Streamtape, Sibnet, Mixdrop vb.)
  const iframes = document.querySelectorAll('iframe');
  for (const iframe of iframes) {
    const src = iframe.src || iframe.getAttribute('data-src') || '';
    if (isKnownIframeHost(src)) {
      reportVideo(src, true);
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

// document.body hazır değilse bekle
if (document.body) {
  observer.observe(document.body, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ['src', 'data-src']
  });
} else {
  document.addEventListener('DOMContentLoaded', () => {
    observer.observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['src', 'data-src']
    });
  });
}
