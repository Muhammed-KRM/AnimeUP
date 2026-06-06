/**
 * AnimeUP — Log Paneli JavaScript
 * Dosya: src/AnimeUP.Extension/logs.js
 *
 * Özellikler:
 *  - Native Host'a getLogs isteği göndererek SQLite loglarını çeker.
 *  - Log türü (function/endpoint), önem (Success/Error), limit ve metin
 *    arama filtrelerini destekler.
 *  - İstatistik çiplerini (toplam, başarılı, hata, ortalama süre) günceller.
 *  - Satıra tıklandığında girdi/çıktı/hata detaylarını modal içinde gösterir.
 *  - Filtreler değiştiğinde otomatik yeniden yükler (debounce ile).
 */

'use strict';

// ─── Durum ──────────────────────────────────────────────────────────────────
const state = {
  logType:  'function',
  severity: '',
  limit:    50,
  search:   '',
  logs:     []
};

// ─── DOM Referansları ────────────────────────────────────────────────────────
const refs = {
  tbody:         document.getElementById('log-tbody'),
  loading:       document.getElementById('state-loading'),
  empty:         document.getElementById('state-empty'),
  errorState:    document.getElementById('state-error'),
  errorMsg:      document.getElementById('error-message'),
  statTotal:     document.getElementById('stat-total').querySelector('.stat-val'),
  statSuccess:   document.getElementById('stat-success').querySelector('.stat-val'),
  statError:     document.getElementById('stat-error').querySelector('.stat-val'),
  statAvg:       document.getElementById('stat-avg-ms').querySelector('.stat-val'),
  searchInput:   document.getElementById('search-input'),
  limitSelect:   document.getElementById('limit-select'),
  btnRefresh:    document.getElementById('btn-refresh'),
  btnClear:      document.getElementById('btn-clear'),
  logTypeToggle: document.getElementById('log-type-toggle'),
  severityToggle:document.getElementById('severity-toggle'),
  modalBackdrop: document.getElementById('modal-backdrop'),
  modalTitle:    document.getElementById('modal-title'),
  modalBody:     document.getElementById('modal-body'),
  modalClose:    document.getElementById('modal-close'),
};

// ─── Yardımcı: Süre renk sınıfı ─────────────────────────────────────────────
function durationClass(ms) {
  if (ms < 50)  return 'fast';
  if (ms < 300) return 'medium';
  return 'slow';
}

// ─── Yardımcı: Tarihi okunabilir formata çevir ──────────────────────────────
function formatDate(dateStr) {
  if (!dateStr) return '—';
  try {
    const d = new Date(dateStr.replace(' ', 'T') + 'Z');
    return d.toLocaleString('tr-TR', {
      day: '2-digit', month: '2-digit', year: 'numeric',
      hour: '2-digit', minute: '2-digit', second: '2-digit'
    });
  } catch {
    return dateStr;
  }
}

// ─── Yardımcı: Metin kesme ───────────────────────────────────────────────────
function truncate(str, len = 60) {
  if (!str) return '—';
  return str.length > len ? str.slice(0, len) + '…' : str;
}

// ─── Yardımcı: JSON güzelleştirme ───────────────────────────────────────────
function prettyJson(str) {
  if (!str) return '—';
  try {
    return JSON.stringify(JSON.parse(str), null, 2);
  } catch {
    return str;
  }
}

// ─── Yardımcı: Segmented control aktif sınıfını güncelle ────────────────────
function activateSegment(container, value) {
  container.querySelectorAll('.seg-btn').forEach(btn => {
    btn.classList.toggle('active', btn.dataset.value === value);
  });
}

// ─── Durum katmanlarını kontrol et ──────────────────────────────────────────
function showStateOverlay(which) {
  // which: 'loading' | 'empty' | 'error' | null
  refs.loading.style.display    = which === 'loading' ? 'flex' : 'none';
  refs.empty.style.display      = which === 'empty'   ? 'flex' : 'none';
  refs.errorState.style.display = which === 'error'   ? 'flex' : 'none';
}

// ─── İstatistikleri güncelle ─────────────────────────────────────────────────
function updateStats(logs) {
  const total   = logs.length;
  const success = logs.filter(l => !l.error_msg && !l.status || l.status === 'Success').length;
  const errors  = logs.filter(l => l.error_msg || l.status === 'Error').length;
  const durations = logs.map(l => l.duration_ms).filter(d => typeof d === 'number');
  const avg = durations.length > 0
    ? Math.round(durations.reduce((a, b) => a + b, 0) / durations.length)
    : 0;

  refs.statTotal.textContent   = total;
  refs.statSuccess.textContent = success;
  refs.statError.textContent   = errors;
  refs.statAvg.textContent     = avg > 0 ? `${avg} ms` : '—';
}

// ─── Tablo satırı oluşturucu ─────────────────────────────────────────────────
function createRow(log, logType) {
  const isError  = !!(log.error_msg || log.status === 'Error');
  const name     = logType === 'function'
    ? (log.method_name ?? log.action ?? '—')
    : (log.action ?? '—');
  const traceId  = truncate(log.trace_id, 12);
  const durationMs = log.duration_ms ?? 0;

  const tr = document.createElement('tr');
  if (isError) tr.classList.add('row-error');
  tr.dataset.logId = log.id;

  tr.innerHTML = `
    <td>
      <span class="badge ${isError ? 'badge-error' : 'badge-success'}">
        <span class="badge-dot"></span>
        ${isError ? 'HATA' : 'TAMAM'}
      </span>
    </td>
    <td>
      <div class="method-cell">
        <span class="method-name">${escHtml(name)}</span>
        <span class="trace-id">${escHtml(traceId)}</span>
      </div>
    </td>
    <td>
      <span class="duration ${durationClass(durationMs)}">${durationMs} ms</span>
    </td>
    <td>
      <span class="date-cell">${formatDate(log.created_at)}</span>
    </td>
    <td>
      <button class="expand-btn" title="Detayları Göster">⊕</button>
    </td>
  `;

  tr.querySelector('.expand-btn').addEventListener('click', (e) => {
    e.stopPropagation();
    openModal(log, logType);
  });

  tr.addEventListener('click', () => openModal(log, logType));

  return tr;
}

// ─── HTML kaçış yardımcısı ───────────────────────────────────────────────────
function escHtml(str) {
  return String(str ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

// ─── Tabloyu filtreli loglarla doldur ────────────────────────────────────────
function renderTable(logs) {
  refs.tbody.innerHTML = '';

  if (!logs || logs.length === 0) {
    showStateOverlay('empty');
    return;
  }

  // Metin arama filtresi (istemci tarafı)
  const query = state.search.toLowerCase().trim();
  const filtered = query
    ? logs.filter(l => {
        const haystack = [
          l.method_name, l.action, l.trace_id,
          l.error_msg, l.input_args, l.output_data
        ].join(' ').toLowerCase();
        return haystack.includes(query);
      })
    : logs;

  if (filtered.length === 0) {
    showStateOverlay('empty');
    return;
  }

  showStateOverlay(null);
  updateStats(filtered);

  const fragment = document.createDocumentFragment();
  filtered.forEach(log => fragment.appendChild(createRow(log, state.logType)));
  refs.tbody.appendChild(fragment);
}

// ─── Logları Native Host'tan çek ────────────────────────────────────────────
async function fetchLogs() {
  showStateOverlay('loading');
  refs.tbody.innerHTML = '';

  try {
    const response = await chrome.runtime.sendMessage({
      action:   'getLogs',
      logType:  state.logType,
      limit:    state.limit,
      severity: state.severity || null
    });

    if (!response?.success) {
      const errText = response?.error ?? 'Bilinmeyen hata';
      refs.errorMsg.textContent = errText;
      showStateOverlay('error');
      return;
    }

    state.logs = response.logs ?? [];
    renderTable(state.logs);
  } catch (err) {
    refs.errorMsg.textContent = err.message ?? 'Native Host bağlantısı kurulamadı.';
    showStateOverlay('error');
  }
}

// ─── Detay Modali ────────────────────────────────────────────────────────────
function openModal(log, logType) {
  const isError = !!(log.error_msg || log.status === 'Error');
  const name = logType === 'function' ? log.method_name : log.action;

  refs.modalTitle.textContent = `Log Detayı — ${name ?? ''}`;
  refs.modalBody.innerHTML = '';

  const rows = logType === 'function'
    ? [
        { label: 'Trace ID',      value: log.trace_id,    mono: true },
        { label: 'Metot Adı',     value: log.method_name, mono: true },
        { label: 'Dosya',         value: log.file_path,   mono: true },
        { label: 'Satır',         value: log.line_number, mono: true },
        { label: 'Süre (ms)',     value: log.duration_ms, mono: true },
        { label: 'Giriş Verisi',  value: prettyJson(log.input_args),  mono: true },
        { label: 'Çıkış Verisi',  value: prettyJson(log.output_data), mono: true },
        { label: 'Hata Mesajı',   value: log.error_msg,  error: true },
        { label: 'Stack Trace',   value: log.stack_trace, error: true },
        { label: 'Oluşturulma',   value: formatDate(log.created_at) },
      ]
    : [
        { label: 'Trace ID',    value: log.trace_id  },
        { label: 'Aksiyon',     value: log.action    },
        { label: 'İstek',       value: prettyJson(log.req_payload), mono: true },
        { label: 'Yanıt',       value: prettyJson(log.res_payload), mono: true },
        { label: 'Süre (ms)',   value: log.duration_ms },
        { label: 'Durum',       value: log.status    },
        { label: 'Oluşturulma', value: formatDate(log.created_at) },
      ];

  for (const row of rows) {
    if (!row.value && row.value !== 0) continue;

    const div = document.createElement('div');
    div.className = 'detail-row';
    const cls = row.error ? 'detail-value error-value' : 'detail-value';
    div.innerHTML = `
      <span class="detail-label">${escHtml(row.label)}</span>
      <pre class="${cls}">${escHtml(String(row.value))}</pre>
    `;
    refs.modalBody.appendChild(div);
  }

  refs.modalBackdrop.classList.add('open');
}

function closeModal() {
  refs.modalBackdrop.classList.remove('open');
  refs.modalBody.innerHTML = '';
}

// ─── Debounce ───────────────────────────────────────────────────────────────
function debounce(fn, ms) {
  let t;
  return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
}

// ─── Olay Dinleyiciler ───────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {

  // Logları yenile
  refs.btnRefresh.addEventListener('click', fetchLogs);

  // Logları temizle (sadece UI — DB tarafı ileride eklenecek)
  refs.btnClear.addEventListener('click', () => {
    refs.tbody.innerHTML = '';
    state.logs = [];
    updateStats([]);
    showStateOverlay('empty');
  });

  // Log türü geçişi
  refs.logTypeToggle.addEventListener('click', (e) => {
    const btn = e.target.closest('.seg-btn');
    if (!btn) return;
    state.logType = btn.dataset.value;
    activateSegment(refs.logTypeToggle, state.logType);
    fetchLogs();
  });

  // Önem derecesi geçişi
  refs.severityToggle.addEventListener('click', (e) => {
    const btn = e.target.closest('.seg-btn');
    if (!btn) return;
    state.severity = btn.dataset.value;
    activateSegment(refs.severityToggle, state.severity);
    fetchLogs();
  });

  // Limit değişimi
  refs.limitSelect.addEventListener('change', () => {
    state.limit = parseInt(refs.limitSelect.value, 10);
    fetchLogs();
  });

  // Metin arama (debounce ile)
  refs.searchInput.addEventListener('input', debounce(() => {
    state.search = refs.searchInput.value;
    renderTable(state.logs);
  }, 300));

  // Modal kapatma
  refs.modalClose.addEventListener('click', closeModal);
  refs.modalBackdrop.addEventListener('click', (e) => {
    if (e.target === refs.modalBackdrop) closeModal();
  });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') closeModal();
  });

  // İlk yükleme
  fetchLogs();
});
