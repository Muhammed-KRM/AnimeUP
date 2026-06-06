/**
 * AnimeUP — Popup Etkileşim Mantığı
 * Dosya: src/AnimeUP.Extension/popup.js
 */

'use strict';

// ─── DOM Referansları ────────────────────────────────────────────────────────
const statusDot    = document.getElementById('status-dot');
const statusText   = document.getElementById('status-text');
const statusBadge  = document.getElementById('status-badge');
const videoInfo    = document.getElementById('video-info');
const detectedUrl  = document.getElementById('detected-url');
const sourceBadge  = document.getElementById('source-badge');
const playBtn      = document.getElementById('play-btn');
const playBtnText  = document.getElementById('play-btn-text');
const logsLink     = document.getElementById('btn-logs');

// ─── Durum: anlık video verisi ───────────────────────────────────────────────
let currentVideo = null;

// ─── Yardımcı: Durum göstergesini güncelle ──────────────────────────────────
function setStatus(state, text) {
  statusDot.className = `pulse-dot ${state}`;
  statusText.textContent = text;

  const badgeMap = {
    scanning: ['TARAMA',       ''],
    found:    ['BULUNDU',      'found'],
    loading:  ['BAŞLATILIYOR', ''],
    error:    ['HATA',         'error']
  };

  const [label, cls] = badgeMap[state] ?? ['BEKLE', ''];
  statusBadge.textContent = label;
  statusBadge.className = `status-badge ${cls}`.trim();
}

// ─── Yardımcı: Video bilgisini arayüze yansıt ───────────────────────────────
function showVideoInfo(video) {
  videoInfo.style.display = 'flex';
  detectedUrl.textContent = video.url;
  detectedUrl.title = video.url;

  const sourceLabels = {
    'network':    'AĞDAN ALINDI',
    'dom-video':  'DOM TESPİTİ',
    'dom-iframe': 'IFRAME ALINDI',
    'page-ytdlp': 'SAYFA (yt-dlp)',
  };
  sourceBadge.textContent = sourceLabels[video.source] ?? 'TESPİT EDİLDİ';
}

// ─── Yardımcı: URL'den kısa başlık türet ────────────────────────────────────
function titleFromUrl(url) {
  try {
    return new URL(url).hostname;
  } catch {
    return 'AnimeUP Stream';
  }
}

// ─── Ana başlatma: aktif tab'daki videoyu çek ───────────────────────────────
document.addEventListener('DOMContentLoaded', async () => {

  logsLink.addEventListener('click', (e) => {
    e.preventDefault();
    chrome.tabs.create({ url: chrome.runtime.getURL('logs.html') });
  });

  playBtn.addEventListener('click', handlePlay);

  let activeTab;
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    activeTab = tab;
  } catch (err) {
    setStatus('error', 'Sekme bilgisi alınamadı.');
    return;
  }

  if (!activeTab?.id) {
    setStatus('error', 'Geçerli sekme bulunamadı.');
    return;
  }

  try {
    const response = await chrome.runtime.sendMessage({
      action: 'getDetectedVideo',
      tabId: activeTab.id
    });

    if (response?.video) {
      currentVideo = response.video;
      currentVideo.pageUrl = activeTab.url ?? '';
      currentVideo.title   = activeTab.title ?? titleFromUrl(currentVideo.url);

      setStatus('found', 'Video Yakalandı!');
      showVideoInfo(currentVideo);
      playBtn.disabled = false;
    } else {
      setStatus('scanning', 'Video Aranıyor...');
    }
  } catch (err) {
    setStatus('error', 'Eklenti bağlantı hatası.');
  }
});

// ─── Oynat butonu tıklama işleyicisi ────────────────────────────────────────
async function handlePlay() {
  if (!currentVideo) return;

  playBtn.disabled = true;
  setStatus('loading', 'MPV Başlatılıyor...');
  playBtnText.textContent = 'Başlatılıyor...';

  const pageUrl = currentVideo.pageUrl || '';
  const payload = {
    action:  'play',
    url:     currentVideo.url,
    title:   currentVideo.title,
    referer: pageUrl ? new URL(pageUrl).origin : '',
    pageUrl: pageUrl,
  };

  try {
    const response = await chrome.runtime.sendMessage({
      action: 'playWithMpv',
      data: payload
    });

    if (response?.success) {
      setStatus('found', 'MPV\'de Oynatılıyor!');
      playBtnText.textContent = '✓ Oynatılıyor';
    } else {
      const errMsg = response?.error ?? 'Bilinmeyen hata';
      setStatus('error', `Hata: ${errMsg}`);
      playBtnText.textContent = 'AnimeUP ile İzle';
      playBtn.disabled = false;
    }
  } catch (err) {
    setStatus('error', `Bağlantı hatası: ${err.message}`);
    playBtnText.textContent = 'AnimeUP ile İzle';
    playBtn.disabled = false;
  }
}
