/**
 * AnimeUP — Popup Etkileşim Mantığı
 * Dosya: src/AnimeUP.Extension/popup.js
 *
 * Akış:
 *  1. DOMContentLoaded → aktif tab ID'sini al.
 *  2. Background'a "getDetectedVideo" mesajı gönder.
 *  3. Video varsa → durum göstergesini güncelle, oynat butonunu etkinleştir.
 *  4. Oynat butonuna basılırsa → "playWithMpv" mesajıyla background'u tetikle.
 *  5. Log butonuna basılırsa → chrome.tabs.create ile logs.html'i aç.
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
  // state: 'scanning' | 'found' | 'loading' | 'error'
  statusDot.className = `pulse-dot ${state}`;
  statusText.textContent = text;

  const badgeMap = {
    scanning: ['TARAMA',   ''],
    found:    ['BULUNDU',  'found'],
    loading:  ['BAŞLATILIYOR', ''],
    error:    ['HATA',     'error']
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
    'network':     'AĞDAN ALINDI',
    'dom-video':   'DOM TESPİTİ',
    'dom-iframe':  'IFRAME ALINDI',
    'page-ytdlp':  'SAYFA (yt-dlp)',
  };
  sourceBadge.textContent = sourceLabels[video.source] ?? 'TESPİT EDİLDİ';

}

// ─── Yardımcı: URL'den kısa başlık türet ────────────────────────────────────
function titleFromUrl(url) {
  try {
    const u = new URL(url);
    return u.hostname;
  } catch {
    return 'AnimeUP Stream';
  }
}

// ─── Ana başlatma: aktif tab'daki videoyu çek ───────────────────────────────
document.addEventListener('DOMContentLoaded', async () => {

  // Log paneli bağlantısı
  logsLink.addEventListener('click', (e) => {
    e.preventDefault();
    chrome.tabs.create({ url: chrome.runtime.getURL('logs.html') });
  });

  // Oynat butonu tıklama
  playBtn.addEventListener('click', handlePlay);

  // Aktif tab'ı bul
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

  // Background'dan tespit edilen videoyu iste
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

  const payload = {
    action:  'play',
    url:     currentVideo.url,
    title:   currentVideo.title,
    referer: currentVideo.pageUrl ? new URL(currentVideo.pageUrl).origin : '',
    pageUrl: currentVideo.pageUrl
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
