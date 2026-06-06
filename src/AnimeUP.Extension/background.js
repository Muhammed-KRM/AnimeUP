/**
 * AnimeUP — Background Service Worker (Manifest V3)
 * Dosya: src/AnimeUP.Extension/background.js
 *
 * Sorumluluklar:
 *  1. Tüm sekmelerdeki ağ trafiğini izleyerek video akışlarını yakalar.
 *  2. Yakalanan URL'leri chrome.storage.session'a (MV3 uyumlu, geçici hafıza)
 *     tab ID bazlı kaydeder — service worker uykuya geçse bile veri kaybolmaz.
 *  3. Popup veya content script'ten gelen mesajları dinler ve Native Host ile köprü kurar.
 *  4. Tab kapatıldığında ilgili video verisini temizler.
 *
 * Native Messaging Protokolü:
 *  → {"action":"play", "url":"...", "title":"...", "referer":"...", "pageUrl":"..."}
 *  ← {"success":true, "message":"MPV launched with PID 12345", "pid":12345}
 *  → {"action":"getLogs", "logType":"function"|"endpoint", "limit":50, "severity":"Error"|null}
 *  ← {"success":true, "logs": [...]}
 */

'use strict';

// ─── Sabitler ───────────────────────────────────────────────────────────────
const NATIVE_HOST_NAME = 'com.animeup.nativehost';

// Video URL tespiti için uzantı ve path desenleri
const VIDEO_EXTENSIONS = ['.m3u8', '.mp4', '.mpd', '.mkv', '.webm', '.flv'];
const VIDEO_PATH_PATTERNS = ['/video/', '/stream/', '/hls/', '/dash/', '/manifest'];
const VIDEO_MIME_PREFIXES = ['video/', 'application/x-mpegURL', 'application/dash+xml'];

// Bilinen iframe barındırıcılar (yt-dlp ile çözümlenir)
const IFRAME_HOSTS = [
  'streamtape', 'sibnet', 'mixdrop', 'fembed', 'ok.ru',
  'dailymotion', 'openload', 'vidoza', 'doodstream', 'filemoon'
];

// ─── Yardımcı: URL'nin video içerip içermediğini tespit eder ────────────────
function isVideoUrl(url) {
  if (!url || !url.startsWith('http')) return false;

  try {
    const lowerUrl = url.toLowerCase();
    const urlObj = new URL(url);
    const pathname = urlObj.pathname.toLowerCase();

    if (VIDEO_EXTENSIONS.some(ext => pathname.endsWith(ext))) return true;
    if (VIDEO_PATH_PATTERNS.some(pat => pathname.includes(pat))) return true;

    return false;
  } catch {
    return false;
  }
}

// ─── Yardımcı: URL'nin bilinen bir iframe barındırıcıya ait olup olmadığı ──
function isKnownIframeHost(url) {
  if (!url) return false;
  return IFRAME_HOSTS.some(host => url.includes(host));
}

// ─── Video kaydını session storage'a yazar ──────────────────────────────────
async function saveDetectedVideo(tabId, url, source = 'network') {
  if (!tabId || tabId < 0 || !url) return;

  const key = `video_${tabId}`;
  const record = { url, source, timestamp: Date.now() };

  await chrome.storage.session.set({ [key]: record });
}

// ─── Tab'a ait video kaydını session storage'dan siler ──────────────────────
async function clearVideoForTab(tabId) {
  await chrome.storage.session.remove([`video_${tabId}`]);
}

// ─── ağ isteği dinleyici: video URL'lerini yakala ───────────────────────────
chrome.webRequest.onBeforeRequest.addListener(
  (details) => {
    const { url, tabId } = details;

    if (isVideoUrl(url)) {
      // Fire-and-forget: await kullanamayız (listener sync olmalı)
      saveDetectedVideo(tabId, url, 'network').catch(() => {});
    }
  },
  { urls: ['<all_urls>'] }
);

// ─── Tab kapatıldığında temizlik ─────────────────────────────────────────────
chrome.tabs.onRemoved.addListener((tabId) => {
  clearVideoForTab(tabId).catch(() => {});
});

// ─── Mesaj Yönlendiricisi ────────────────────────────────────────────────────
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  const action = message?.action;

  if (!action) return false;

  switch (action) {

    // Popup → Background: Tespit edilen videoyu getir
    case 'getDetectedVideo': {
      const tabId = message.tabId;
      const key = `video_${tabId}`;

      chrome.storage.session.get([key]).then((result) => {
        sendResponse({ video: result[key] ?? null });
      }).catch((err) => {
        sendResponse({ video: null, error: err.message });
      });

      return true; // async
    }

    // Content Script → Background: DOM'dan yakalanan video URL'si
    case 'videoDetectedFromDOM': {
      const tabId = sender?.tab?.id;
      const url   = message?.url;
      const isIframe = message?.isIframe ?? false;

      if (tabId && url) {
        const source = isIframe ? 'dom-iframe' : 'dom-video';
        saveDetectedVideo(tabId, url, source).catch(() => {});
      }

      return false; // sync
    }

    // Popup → Background: MPV ile oynat
    case 'playWithMpv': {
      handlePlayWithMpv(message.data, sendResponse);
      return true; // async
    }

    // Popup → Background: Logları getir
    case 'getLogs': {
      handleGetLogs(message, sendResponse);
      return true; // async
    }

    default:
      return false;
  }
});

// ─── Native Host: Video oynatma isteği gönderir ─────────────────────────────
function handlePlayWithMpv(videoData, sendResponse) {
  if (!videoData?.url) {
    sendResponse({ success: false, error: 'Geçersiz video verisi.' });
    return;
  }

  chrome.runtime.sendNativeMessage(NATIVE_HOST_NAME, videoData, (response) => {
    if (chrome.runtime.lastError) {
      sendResponse({
        success: false,
        error: chrome.runtime.lastError.message
      });
      return;
    }

    sendResponse({ success: true, response });
  });
}

// ─── Native Host: Log sorgulama isteği gönderir ─────────────────────────────
function handleGetLogs(message, sendResponse) {
  const payload = {
    action: 'getLogs',
    logType: message.logType ?? 'function',
    limit: message.limit ?? 50,
    severity: message.severity ?? null
  };

  chrome.runtime.sendNativeMessage(NATIVE_HOST_NAME, payload, (response) => {
    if (chrome.runtime.lastError) {
      sendResponse({
        success: false,
        error: chrome.runtime.lastError.message
      });
      return;
    }

    sendResponse({ success: true, logs: response?.logs ?? [] });
  });
}
