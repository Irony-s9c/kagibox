// KagiBox – main.js
// Tauri v2: window.__TAURI__ available because withGlobalTauri:true in tauri.conf.json

const invoke = (...args) => window.__TAURI__.core.invoke(...args);
const listen  = (...args) => window.__TAURI__.event.listen(...args);

// ==================== i18n ====================

const I18N = {
  ja: {
    app_name: 'KagiBox', unlock: 'ロック解除', lock: 'ロック',
    master_password: 'マスターパスワード', wrong_password: 'パスワードが違います',
    add_entry: 'エントリを追加', edit_entry: 'エントリを編集',
    delete_entry: 'エントリを削除', delete_confirm: 'このエントリを削除してよろしいですか？',
    search: '検索...', service: 'サービス名', username: 'ユーザー名',
    password: 'パスワード', url: 'URL', memo: 'メモ',
    generate: '生成', length: '文字数', count: '個数',
    digits: '数字', upper: '英大文字', lower: '英小文字', symbols: '記号',
    exclude_similar: '似た文字を除く (I/l/1/O/o/0)', first_char: '頭文字',
    first_char_none: '指定なし', first_char_upper: '英大文字',
    first_char_lower: '英小文字', first_char_digit: '数字',
    export: 'データ書き出し',
    export_warn: 'テキスト保存は暗号化されません。取り扱いにご注意ください。',
    copy: 'コピー', copied: 'コピーしました', clipboard_cleared: 'クリップボードをクリアしました',
    auto_lock: '自動ロックまでの時間（分）', auto_lock_desc: '無操作が続くと自動的にロックします',
    clipboard_clear_sec: 'クリップボード自動クリア（秒）',
    language: '言語', language_select: '言語を選択してください',
    settings: '設定', save: '保存', cancel: 'キャンセル', ok: 'OK',
    error: 'エラー', success: '成功', reveal: '表示', hide: '非表示',
    char_type_required: '文字種を1つ以上選択してください',
    update_available: 'アップデートがあります', update_installing: 'インストール中...',
    update_install_now: '今すぐインストール', update_later: '後で',
    no_chars_available: '使用可能な文字がありません',
    all_services: 'すべてのサービス', entries_count: '件',
    auto_locked: '自動ロックされました', save_saving: '保存中…',
    save_saved: '保存済み', save_error: '保存失敗: ',
    welcome: 'KagiBox へようこそ', set_password_desc: 'まずマスターパスワードを設定してください',
    password_too_short: '4文字以上のパスワードを設定してください',
    password_mismatch: '2つのパスワードが一致しません',
    password_enter: 'パスワードを入力してください',
    init_failed: '初期化に失敗しました: ', unlock_error: 'エラー: ',
    pw_fetch_fail: 'パスワードの取得に失敗しました',
    copy_fail: 'コピーに失敗しました', del_fail: '削除に失敗しました: ',
    save_fail: '保存に失敗: ', cfg_save_fail: '保存に失敗: ',
    export_fail: '書き出しに失敗: ', exported: '書き出しました: ',
    service_required: 'サービス名を入力してください',
    auto_lock_min_error: '自動ロックは1分以上を指定してください',
    clip_clear_min_error: 'クリップボードクリアは5秒以上を指定してください',
    cfg_saved: '設定を保存しました',
    memo_show: 'メモを表示 ▾', memo_hide: 'メモを非表示 ▴',
    delete_btn: '削除', edit_btn: '編集', start_btn: 'KagiBox を始める',
    no_entries: 'まだエントリが登録されていません。\n左の「エントリを追加」または右上の「＋」から登録しましょう。',
    no_svc_entries: 'このサービスにエントリがありません。\n右上の「＋ エントリを追加」から登録しましょう。',
    pw_use: '使用', pw_generate_btn: '生成する',
    lockout_msg: '回失敗。',  lockout_after: '秒後に再試行できます',
    min_label: '分', sec_label: '秒', autolock_label: '後に自動ロック',
    confirm_delete_title: 'エントリを削除',
    confirm_delete_msg: 'このエントリを削除してよろしいですか？この操作は元に戻せません。',
    click_to_resume: 'クリックして再表示',
  },
  en: {
    app_name: 'KagiBox', unlock: 'Unlock', lock: 'Lock',
    master_password: 'Master Password', wrong_password: 'Wrong password',
    add_entry: 'Add Entry', edit_entry: 'Edit Entry',
    delete_entry: 'Delete Entry', delete_confirm: 'Are you sure you want to delete this entry?',
    search: 'Search...', service: 'Service', username: 'Username',
    password: 'Password', url: 'URL', memo: 'Notes',
    generate: 'Generate', length: 'Length', count: 'Count',
    digits: 'Digits', upper: 'Uppercase', lower: 'Lowercase', symbols: 'Symbols',
    exclude_similar: 'Exclude similar (I/l/1/O/o/0)', first_char: 'First char',
    first_char_none: 'Any', first_char_upper: 'Uppercase',
    first_char_lower: 'Lowercase', first_char_digit: 'Digit',
    export: 'Export Data',
    export_warn: 'Text export is not encrypted. Handle with care.',
    copy: 'Copy', copied: 'Copied', clipboard_cleared: 'Clipboard cleared',
    auto_lock: 'Auto-lock after (minutes)', auto_lock_desc: 'Lock automatically after inactivity',
    clipboard_clear_sec: 'Clipboard auto-clear (seconds)',
    language: 'Language', language_select: 'Select Language',
    settings: 'Settings', save: 'Save', cancel: 'Cancel', ok: 'OK',
    error: 'Error', success: 'Success', reveal: 'Show', hide: 'Hide',
    char_type_required: 'Select at least one character type',
    update_available: 'Update available', update_installing: 'Installing...',
    update_install_now: 'Install now', update_later: 'Later',
    no_chars_available: 'No characters available',
    all_services: 'All Services', entries_count: '',
    auto_locked: 'Auto-locked', save_saving: 'Saving…',
    save_saved: 'Saved', save_error: 'Save failed: ',
    welcome: 'Welcome to KagiBox', set_password_desc: 'Set your master password to get started',
    password_too_short: 'Password must be at least 4 characters',
    password_mismatch: 'Passwords do not match',
    password_enter: 'Enter your password',
    init_failed: 'Initialization failed: ', unlock_error: 'Error: ',
    pw_fetch_fail: 'Failed to retrieve password',
    copy_fail: 'Copy failed', del_fail: 'Delete failed: ',
    save_fail: 'Save failed: ', cfg_save_fail: 'Save failed: ',
    export_fail: 'Export failed: ', exported: 'Exported: ',
    service_required: 'Service name is required',
    auto_lock_min_error: 'Auto-lock must be at least 1 minute',
    clip_clear_min_error: 'Clipboard clear must be at least 5 seconds',
    cfg_saved: 'Settings saved',
    memo_show: 'Show notes ▾', memo_hide: 'Hide notes ▴',
    delete_btn: 'Delete', edit_btn: 'Edit', start_btn: 'Start KagiBox',
    no_entries: 'No entries yet.\nClick "Add Entry" in the sidebar or the + button above.',
    no_svc_entries: 'No entries for this service.\nClick "+ Add Entry" to get started.',
    pw_use: 'Use', pw_generate_btn: 'Generate',
    lockout_msg: ' failures. ',  lockout_after: 's before retry',
    min_label: 'm', sec_label: 's', autolock_label: ' until auto-lock',
    confirm_delete_title: 'Delete Entry',
    confirm_delete_msg: 'Are you sure you want to delete this entry? This cannot be undone.',
    click_to_resume: 'Click to resume',
  }
};

let _lang = 'ja';
function t(key) { return (I18N[_lang] || I18N.ja)[key] || key; }

async function initLang() {
  try {
    const cfg = await invoke('get_config');
    _lang = cfg.language || 'ja';
  } catch (_) { _lang = 'ja'; }
  applyLangToPage();
}

function applyLangToPage() {
  const map = {
    'lock-screen': null,
    'sidebar-search': { placeholder: t('search') },
  };
  document.querySelectorAll('[data-i18n]').forEach(el => {
    const key = el.dataset.i18n;
    el.textContent = t(key);
  });
  document.querySelectorAll('[data-i18n-ph]').forEach(el => {
    el.placeholder = t(el.dataset.i18nPh);
  });
}

// ==================== STATE ====================

let _entries = [];       // EntrySummary[]
let _currentSvc = null;  // string|null – currently selected service name
let _editingId  = null;  // string|null – id being edited (null = new)
let _genForEdit = false; // true when generator was opened from edit modal
let _editingIcon = null; // data URL for icon being edited
let _autoLockMinutes = 5;
let _lastActivity = Date.now();
let _autoLockTimer = null;
let _failCount = 0;
let _lockUntil = 0;
let _lockoutTimer = null;

// ==================== INIT ====================

window.addEventListener('DOMContentLoaded', async () => {
  // Build symbol picker checkboxes
  const GEN_SYMBOLS = '/*-+.,!#$%&()~|_';
  const symContainer = document.getElementById('gen-symbol-checks');
  if (symContainer) {
    GEN_SYMBOLS.split('').forEach(s => {
      const label = document.createElement('label');
      label.className = 'gen-check';
      const cb = document.createElement('input');
      cb.type = 'checkbox'; cb.className = 'gen-sym-cb'; cb.value = s; cb.checked = true;
      label.append(cb, ' ' + s);
      symContainer.appendChild(label);
    });
  }

  // Always register global event listeners
  listen('auto-locked', () => {
    lockApp();
    showToast(t('auto_locked'));
  });
  listen('clipboard-cleared', () => {
    showToast(t('clipboard_cleared'));
  });

  ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'].forEach(ev =>
    document.addEventListener(ev, resetActivity, { passive: true })
  );

  document.addEventListener('visibilitychange', () => {
    const blur = document.getElementById('screen-blur');
    if (!blur) return;
    blur.style.display = (document.hidden && isAppVisible()) ? 'flex' : 'none';
  });
  document.getElementById('screen-blur').addEventListener('click', () => {
    document.getElementById('screen-blur').style.display = 'none';
  });

  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') {
      const open = [...document.querySelectorAll('.overlay.show')]
        .filter(el => el.id !== 'lang-overlay');
      if (open.length) closeModal(open[open.length - 1].id);
    }
  });

  document.querySelectorAll('.overlay').forEach(el => {
    let downOnBackdrop = false;
    el.addEventListener('mousedown', e => { downOnBackdrop = e.target === el; });
    el.addEventListener('click', e => {
      if (downOnBackdrop && e.target === el && el.id !== 'lang-overlay') closeModal(el.id);
    });
  });

  document.getElementById('master-input').addEventListener('keydown', e => {
    if (e.key === 'Enter') doUnlock();
  });
  document.getElementById('setup-pw1').addEventListener('keydown', e => {
    if (e.key === 'Enter') document.getElementById('setup-pw2').focus();
  });
  document.getElementById('setup-pw2').addEventListener('keydown', e => {
    if (e.key === 'Enter') doSetup();
  });

  ['edit-service','edit-username','edit-password','edit-url'].forEach(id => {
    document.getElementById(id).addEventListener('keydown', e => {
      if (e.key === 'Enter') saveEntry();
    });
  });

  ['gen-len','gen-count'].forEach(id => {
    document.getElementById(id).addEventListener('keydown', e => {
      if (e.key === 'Enter') runGenerator();
    });
  });

  ['cfg-auto-lock','cfg-clip-clear'].forEach(id => {
    document.getElementById(id).addEventListener('keydown', e => {
      if (e.key === 'Enter') saveConfig();
    });
  });

  document.getElementById('edit-icon-input').addEventListener('change', function() {
    const file = this.files[0];
    if (!file) return;
    if (file.size > 200 * 1024) { showToast('画像は200KB以下にしてください'); this.value = ''; return; }
    const reader = new FileReader();
    reader.onload = ev => { _editingIcon = ev.target.result; applyIconPreview(_editingIcon); };
    reader.readAsDataURL(file);
  });

  // Load language config
  await initLang();
  const created = await invoke('is_vault_created');

  if (!created) {
    // First launch: show language selection before setup
    openModal('lang-overlay');
  } else {
    showLockScreen(false);
  }
});

function isAppVisible() {
  return document.getElementById('app').classList.contains('visible');
}

// ==================== LOCK SCREEN ====================

function showLockScreen(isSetup) {
  document.getElementById('unlock-section').style.display = isSetup ? 'none' : 'block';
  document.getElementById('setup-section').style.display  = isSetup ? 'block' : 'none';
  document.getElementById('lock-err').textContent = '';
  document.getElementById('lockout-msg').textContent = '';

  if (isSetup) {
    document.getElementById('lock-title').textContent = t('welcome');
    document.getElementById('lock-desc').textContent  = t('set_password_desc');
    document.getElementById('setup-pw1').value = '';
    document.getElementById('setup-pw2').value = '';
    setTimeout(() => document.getElementById('setup-pw1').focus(), 50);
  } else {
    document.getElementById('lock-title').textContent = t('app_name');
    document.getElementById('lock-desc').textContent  = t('master_password');
    document.getElementById('master-input').value = '';
    setTimeout(() => document.getElementById('master-input').focus(), 50);
  }

  document.getElementById('lock-screen').style.display = 'flex';
  document.getElementById('app').classList.remove('visible');
}

async function doSetup() {
  const pw1 = document.getElementById('setup-pw1').value;
  const pw2 = document.getElementById('setup-pw2').value;
  if (!pw1)           { setErr('lock-err', t('password_enter')); return; }
  if (pw1.length < 4) { setErr('lock-err', t('password_too_short')); return; }
  if (pw1 !== pw2)    { setErr('lock-err', t('password_mismatch')); return; }
  try {
    await invoke('unlock', { master: pw1 });
    showApp();
  } catch (e) {
    setErr('lock-err', t('init_failed') + e);
  }
}

async function doUnlock() {
  if (isLockedOut()) return;
  const pw = document.getElementById('master-input').value;
  if (!pw) { setErr('lock-err', t('password_enter')); return; }
  try {
    await invoke('unlock', { master: pw });
    recordSuccess();
    setErr('lock-err', '');
    showApp();
  } catch (e) {
    recordFail();
    const msg = String(e).includes('wrong-password')
      ? t('wrong_password')
      : t('unlock_error') + e;
    setErr('lock-err', msg);
    document.getElementById('master-input').value = '';
    document.getElementById('master-input').focus();
  }
}

// ==================== APP SHOW / LOCK ====================

async function showApp() {
  document.getElementById('lock-screen').style.display = 'none';
  document.getElementById('app').classList.add('visible');
  _currentSvc = null;
  await refreshEntries();
  resetActivity();
  startAutoLockCountdown();
  checkForUpdate();
}

async function checkForUpdate() {
  try {
    const info = await invoke('check_update');
    if (info) {
      const bar = document.getElementById('update-bar');
      document.getElementById('update-bar-msg').textContent =
        `${t('update_available')} v${info.version}`;
      document.getElementById('update-install-btn').textContent = t('update_install_now');
      document.getElementById('update-later-btn').textContent = t('update_later');
      bar.classList.add('show');
      document.getElementById('app').classList.add('has-update');
    }
  } catch (_) {}
}

async function doUpdate() {
  const btn = document.getElementById('update-install-btn');
  btn.textContent = t('update_installing');
  btn.disabled = true;
  try {
    await invoke('do_update');
  } catch (e) {
    showToast(t('error') + ': ' + e);
    btn.textContent = t('update_install_now');
    btn.disabled = false;
  }
}

function dismissUpdate() {
  const bar = document.getElementById('update-bar');
  bar.classList.remove('show');
  document.getElementById('app').classList.remove('has-update');
}

async function lockApp() {
  stopAutoLockCountdown();
  try { await invoke('lock'); } catch (_) {}
  _entries = [];
  _currentSvc = null;
  document.getElementById('screen-blur').style.display = 'none';
  const created = await invoke('is_vault_created');
  showLockScreen(!created);
}

// ==================== ENTRIES ====================

async function refreshEntries(searchQuery) {
  try {
    if (searchQuery && searchQuery.trim()) {
      _entries = await invoke('search_entries', { query: searchQuery.trim() });
    } else {
      _entries = await invoke('list_entries');
    }
  } catch (_) {
    _entries = [];
  }
  renderSidebar();
  renderMain();
}

// ==================== SIDEBAR ====================

function renderSidebar() {
  const list = document.getElementById('sidebar-list');
  list.innerHTML = '';

  const allItem = makeEl('div', 'svc-item' + (_currentSvc === null ? ' active' : ''));
  const allDot = makeEl('div', 'svc-dot');
  allDot.style.background = '#52525b';
  allDot.innerHTML = `<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/></svg>`;
  const allLabel = makeEl('span', 'svc-label'); allLabel.textContent = t('all_services');
  allItem.append(allDot, allLabel);
  allItem.onclick = () => { _currentSvc = null; renderSidebar(); renderMain(); };
  list.appendChild(allItem);

  const groups = groupByService(_entries);
  const sorted = Object.keys(groups).sort((a, b) => a.localeCompare(b, 'ja'));
  sorted.forEach(svc => {
    const count = groups[svc].length;
    const item = makeEl('div', 'svc-item' + (_currentSvc === svc ? ' active' : ''));
    const dot = makeServiceDot(svc);
    const label = makeEl('span', 'svc-label'); label.textContent = svc;
    const cnt = makeEl('span', 'svc-count'); cnt.textContent = count;
    item.append(dot, label, cnt);
    item.onclick = () => { _currentSvc = svc; renderSidebar(); renderMain(); };
    list.appendChild(item);
  });
}

function groupByService(entries) {
  const g = {};
  entries.forEach(e => {
    if (!g[e.service]) g[e.service] = [];
    g[e.service].push(e);
  });
  return g;
}

// ==================== MAIN AREA ====================

function renderMain() {
  const main = document.getElementById('main-area');
  main.innerHTML = '';
  if (_currentSvc === null) renderAllView(main);
  else renderServiceView(main, _currentSvc);
}

function renderAllView(main) {
  const header = makeEl('div', 'main-header');
  const title = makeEl('h2', 'main-title'); title.textContent = t('all_services');
  const addBtn = makeBtn('+ ' + t('add_entry'), 'btn-icon accent', () => openEditModal(null));
  header.append(title, addBtn);
  main.appendChild(header);

  const body = makeEl('div', 'main-body');
  const groups = groupByService(_entries);
  const sorted = Object.keys(groups).sort((a, b) => a.localeCompare(b, 'ja'));

  if (sorted.length === 0) {
    const empty = makeEl('div', 'empty-state');
    empty.innerHTML = `<svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg><p>${t('no_entries').replace(/\n/g,'<br>')}</p>`;
    body.appendChild(empty);
  } else {
    const grid = makeEl('div', 'all-grid');
    sorted.forEach(svc => {
      const entries = groups[svc];
      const card = makeEl('div', 'grid-card');
      card.onclick = () => { _currentSvc = svc; renderSidebar(); renderMain(); };
      const head = makeEl('div', 'grid-card-head');
      const dot = makeServiceDot(svc);
      const name = makeEl('span', 'grid-card-name'); name.textContent = svc;
      const cnt = makeEl('span', 'grid-card-count'); cnt.textContent = entries.length + ' 件';
      head.append(dot, name, cnt);
      const previews = makeEl('div', 'grid-card-emails');
      entries.slice(0, 3).forEach(e => {
        const p = makeEl('div', 'grid-card-email');
        p.textContent = e.username || e.url || '(空)';
        previews.appendChild(p);
      });
      if (entries.length > 3) {
        const more = makeEl('div', 'grid-card-email');
        more.textContent = `+${entries.length - 3} 件`;
        previews.appendChild(more);
      }
      card.append(head, previews);
      grid.appendChild(card);
    });
    body.appendChild(grid);
  }
  main.appendChild(body);
}

function renderServiceView(main, svc) {
  const entries = _entries.filter(e => e.service === svc);

  const header = makeEl('div', 'main-header');
  const dot = makeServiceDot(svc);
  dot.style.width = '32px'; dot.style.height = '32px';
  dot.style.borderRadius = '8px'; dot.style.fontSize = '0.9rem';

  const titleWrap = makeEl('div', ''); titleWrap.style.flex = '1';
  const title = makeEl('h2', 'main-title'); title.textContent = svc;
  titleWrap.appendChild(title);

  const addBtn = makeBtn('+ ' + t('add_entry'), 'btn-icon accent', () => openEditModal(null, svc));
  header.append(dot, titleWrap, addBtn);
  main.appendChild(header);

  const body = makeEl('div', 'main-body');
  const cards = makeEl('div', 'account-cards');

  if (entries.length === 0) {
    const empty = makeEl('div', 'empty-state');
    empty.innerHTML = `<svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg><p>${t('no_svc_entries').replace(/\n/g,'<br>')}</p>`;
    cards.appendChild(empty);
  } else {
    const sortedEntries = [...entries].sort((a, b) =>
      (a.username || '').localeCompare(b.username || '', 'ja')
    );
    sortedEntries.forEach((e, i) => cards.appendChild(makeEntryCard(e, i + 1)));
  }

  body.appendChild(cards);
  main.appendChild(body);
}

function makeEntryCard(entry, num) {
  const card = makeEl('div', 'ac-card');

  const head = makeEl('div', 'ac-head');
  const numEl = makeEl('span', 'ac-number'); numEl.textContent = `#${num}`;
  const label = makeEl('span', 'ac-head-label');
  label.textContent = entry.username || entry.url || entry.service;
  const actions = makeEl('div', 'ac-head-actions');

  const editBtn = makeEl('button', 'ac-btn'); editBtn.title = '編集';
  editBtn.innerHTML = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>`;
  editBtn.onclick = () => openEditModal(entry.id);

  const delBtn = makeEl('button', 'ac-btn delete'); delBtn.title = '削除';
  delBtn.innerHTML = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>`;
  delBtn.onclick = () => confirmDeleteEntry(entry.id);

  actions.append(editBtn, delBtn);
  head.append(numEl, label, actions);
  card.appendChild(head);

  const fields = makeEl('div', 'ac-fields');
  if (entry.username) fields.appendChild(makeFieldRow(t('username'), entry.username));
  if (entry.url)      fields.appendChild(makeFieldRowUrl('URL', entry.url));
  fields.appendChild(makePasswordRow(entry.id));
  card.appendChild(fields);

  if (entry.memo && entry.memo.trim()) {
    const memoToggle = makeEl('button', 'notes-toggle-btn');
    memoToggle.textContent = t('memo_show');
    const memoEl = makeEl('div', 'ac-notes');
    memoEl.textContent = entry.memo;
    memoEl.style.display = 'none';
    memoToggle.onclick = () => {
      const show = memoEl.style.display === 'none';
      memoEl.style.display = show ? 'block' : 'none';
      memoToggle.textContent = show ? t('memo_hide') : t('memo_show');
    };
    card.append(memoToggle, memoEl);
  }

  return card;
}

function makeFieldRow(labelText, value) {
  const row = makeEl('div', 'ac-row');
  const key = makeEl('span', 'ac-key'); key.textContent = labelText;
  const val = makeEl('span', 'ac-val'); val.textContent = value;
  const ac = makeEl('div', 'ac-actions');
  const copyIcon = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>`;
  const copyBtn = makeEl('button', 'tiny-btn'); copyBtn.title = 'コピー';
  copyBtn.innerHTML = copyIcon;
  copyBtn.onclick = () => copyValue(value, copyBtn, copyIcon);
  ac.appendChild(copyBtn);
  row.append(key, val, ac);
  return row;
}

function makeFieldRowUrl(labelText, url) {
  const row = makeEl('div', 'ac-row');
  const key = makeEl('span', 'ac-key'); key.textContent = labelText;
  const val = makeEl('span', 'ac-val');
  const a = makeEl('a');
  a.href = url; a.textContent = url; a.target = '_blank'; a.rel = 'noopener noreferrer';
  val.appendChild(a);
  const ac = makeEl('div', 'ac-actions');
  const copyIcon = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>`;
  const copyBtn = makeEl('button', 'tiny-btn'); copyBtn.title = 'コピー';
  copyBtn.innerHTML = copyIcon;
  copyBtn.onclick = () => copyValue(url, copyBtn, copyIcon);
  ac.appendChild(copyBtn);
  row.append(key, val, ac);
  return row;
}

function makePasswordRow(entryId) {
  const row = makeEl('div', 'ac-row');
  const key = makeEl('span', 'ac-key'); key.textContent = t('password');
  const val = makeEl('span', 'ac-val');
  val.textContent = '••••••••••••';
  val.style.letterSpacing = '0.1em';
  const ac = makeEl('div', 'ac-actions');

  let revealed = false;
  let pwValue = null;
  let showing = false;

  const eyeIconShow = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>`;
  const eyeIconHide = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>`;
  const copyIcon = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>`;

  const eyeBtn = makeEl('button', 'tiny-btn'); eyeBtn.title = '表示/非表示';
  eyeBtn.innerHTML = eyeIconShow;
  eyeBtn.onclick = async () => {
    if (!revealed) {
      try {
        pwValue = await invoke('reveal_password', { id: entryId });
        revealed = true;
      } catch (_) { showToast(t('pw_fetch_fail')); return; }
    }
    showing = !showing;
    if (showing) {
      val.textContent = pwValue;
      val.style.letterSpacing = '';
      val.style.fontFamily = 'Menlo, Consolas, monospace';
      eyeBtn.innerHTML = eyeIconHide;
    } else {
      val.textContent = '••••••••••••';
      val.style.letterSpacing = '0.1em';
      val.style.fontFamily = '';
      eyeBtn.innerHTML = eyeIconShow;
    }
  };

  const copyBtn = makeEl('button', 'tiny-btn'); copyBtn.title = 'コピー';
  copyBtn.innerHTML = copyIcon;
  copyBtn.onclick = async () => {
    if (!revealed) {
      try { pwValue = await invoke('reveal_password', { id: entryId }); revealed = true; }
      catch (_) { showToast(t('pw_fetch_fail')); return; }
    }
    copyValue(pwValue, copyBtn, copyIcon);
  };

  ac.append(eyeBtn, copyBtn);
  row.append(key, val, ac);
  return row;
}

// ==================== COPY ====================

async function copyValue(value, btn, originalHTML) {
  try {
    await invoke('copy_to_clipboard', { text: value });
    btn.innerHTML = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"/></svg>`;
    btn.classList.add('copied');
    showToast(t('copied'));
    setTimeout(() => {
      btn.innerHTML = originalHTML;
      btn.classList.remove('copied');
    }, 2000);
  } catch (_) {
    showToast(t('copy_fail'));
  }
}

// ==================== ENTRY EDIT MODAL ====================

async function openEditModal(id, prefillService) {
  _editingId = id || null;
  document.getElementById('edit-title').textContent = id ? t('edit_entry') : t('add_entry');
  document.getElementById('edit-delete-btn').style.display = id ? 'inline-flex' : 'none';
  document.getElementById('edit-err').textContent = '';

  if (id) {
    const entry = _entries.find(e => e.id === id);
    document.getElementById('edit-service').value  = entry ? entry.service  : '';
    document.getElementById('edit-username').value = entry ? entry.username : '';
    document.getElementById('edit-url').value      = entry ? entry.url      : '';
    document.getElementById('edit-memo').value     = entry ? (entry.memo || '') : '';
    document.getElementById('edit-password').value = '';
    _editingIcon = (entry && entry.icon) ? entry.icon : null;
    try {
      const pw = await invoke('reveal_password', { id });
      document.getElementById('edit-password').value = pw;
    } catch (_) {}
  } else {
    document.getElementById('edit-service').value  = prefillService || '';
    document.getElementById('edit-username').value = '';
    document.getElementById('edit-password').value = '';
    document.getElementById('edit-url').value      = '';
    document.getElementById('edit-memo').value     = '';
    _editingIcon = null;
  }
  applyIconPreview(_editingIcon);

  openModal('edit-overlay');
  setTimeout(() => document.getElementById('edit-service').focus(), 50);
}

async function saveEntry() {
  const saveBtn = document.getElementById('edit-save-btn');
  const errEl   = document.getElementById('edit-err');
  errEl.textContent = '';
  const svc = document.getElementById('edit-service').value.trim();
  if (!svc) { errEl.textContent = t('service_required'); return; }

  saveBtn.disabled = true; saveBtn.textContent = '保存中…';
  setSaveStatus('saving');
  try {
    const entry = {
      service:  svc,
      icon:     _editingIcon || null,
      username: document.getElementById('edit-username').value.trim(),
      password: document.getElementById('edit-password').value,
      url:      document.getElementById('edit-url').value.trim(),
      memo:     document.getElementById('edit-memo').value.trim(),
    };

    if (_editingId) {
      await invoke('update_entry', { entry: { id: _editingId, ...entry } });
      if (_currentSvc && entry.service !== _currentSvc) _currentSvc = entry.service;
    } else {
      await invoke('add_entry', { entry });
      if (_currentSvc !== null && entry.service !== _currentSvc) {
        // new entry in different service – switch to it
      }
    }

    closeModal('edit-overlay');
    await refreshEntries(document.getElementById('sidebar-search').value);
    setSaveStatus('saved');
  } catch (e) {
    errEl.textContent = t('save_fail') + e;
    setSaveStatus('error', String(e));
  } finally {
    saveBtn.disabled = false; saveBtn.textContent = '保存';
  }
}

function deleteEntryFromModal() {
  closeModal('edit-overlay');
  confirmDeleteEntry(_editingId);
}

function confirmDeleteEntry(id) {
  document.getElementById('confirm-title').textContent = t('confirm_delete_title');
  document.getElementById('confirm-msg').textContent   = t('confirm_delete_msg');
  const btn = document.getElementById('confirm-ok');
  btn.onclick = async () => {
    closeModal('confirm-overlay');
    try {
      const entry = _entries.find(e => e.id === id);
      await invoke('delete_entry', { id });
      if (entry && _currentSvc === entry.service) {
        const remaining = _entries.filter(e => e.service === entry.service && e.id !== id);
        if (remaining.length === 0) _currentSvc = null;
      }
      await refreshEntries(document.getElementById('sidebar-search').value);
    } catch (e) { showToast(t('del_fail') + e); }
  };
  openModal('confirm-overlay');
}

// ==================== SEARCH ====================

let _searchTimer = null;
function onSearch(q) {
  clearTimeout(_searchTimer);
  _searchTimer = setTimeout(() => refreshEntries(q), 200);
}

// ==================== GENERATOR ====================

function openGenerator(forEdit) {
  _genForEdit = Boolean(forEdit);
  document.getElementById('gen-results').innerHTML = '';
  document.getElementById('gen-err').textContent = '';
  openModal('gen-overlay');
}

async function runGenerator() {
  const length     = parseInt(document.getElementById('gen-len').value, 10) || 20;
  const count      = Math.min(parseInt(document.getElementById('gen-count').value, 10) || 5, 20);
  const useUpper   = document.getElementById('gen-upper').checked;
  const useLower   = document.getElementById('gen-lower').checked;
  const useDigits  = document.getElementById('gen-digits').checked;
  const useSymbols = document.getElementById('gen-symbols').checked;
  const excl       = document.getElementById('gen-excl').checked;
  const firstRaw   = document.querySelector('input[name="gen-first"]:checked')?.value || 'None';

  if (!useUpper && !useLower && !useDigits && !useSymbols) {
    document.getElementById('gen-err').textContent = t('char_type_required');
    return;
  }
  document.getElementById('gen-err').textContent = '';

  const checkedSymbols = useSymbols
    ? [...document.querySelectorAll('.gen-sym-cb:checked')].map(cb => cb.value).join('')
    : '';

  const opt = {
    length,
    count,
    use_upper:       useUpper,
    use_lower:       useLower,
    use_digits:      useDigits,
    use_symbols:     useSymbols,
    allowed_symbols: checkedSymbols,
    exclude_similar: excl,
    first_char:      firstRaw,
  };

  try {
    const results = await invoke('generate', { opt });
    renderGenResults(results);
  } catch (e) {
    document.getElementById('gen-err').textContent = String(e);
  }
}

function renderGenResults(passwords) {
  const container = document.getElementById('gen-results');
  container.innerHTML = '';
  const exportBtn = document.getElementById('gen-export-btn');
  if (exportBtn) exportBtn.style.display = passwords.length ? 'inline-flex' : 'none';

  // Auto-insert the first password when opened from edit modal
  if (_genForEdit && passwords.length > 0) {
    document.getElementById('edit-password').value = passwords[0];
  }
  const copyIcon = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>`;
  passwords.forEach(pw => {
    const row = makeEl('div', 'gen-result-row');
    const pwEl = makeEl('span', 'gen-result-pw'); pwEl.textContent = pw;
    const copyBtn = makeEl('button', 'tiny-btn'); copyBtn.title = 'コピー';
    copyBtn.innerHTML = copyIcon;
    copyBtn.onclick = () => copyValue(pw, copyBtn, copyIcon);

    if (_genForEdit) {
      const useBtn = makeEl('button', 'gen-use-btn'); useBtn.textContent = t('pw_use');
      useBtn.onclick = () => {
        document.getElementById('edit-password').value = pw;
        closeModal('gen-overlay');
      };
      row.append(pwEl, copyBtn, useBtn);
    } else {
      row.append(pwEl, copyBtn);
    }
    container.appendChild(row);
  });
}

// ==================== SETTINGS ====================

async function openSettingsModal() {
  try {
    const cfg = await invoke('get_config');
    document.getElementById('cfg-auto-lock').value  = cfg.auto_lock_minutes;
    document.getElementById('cfg-clip-clear').value = cfg.clipboard_clear_secs;
    document.getElementById('cfg-lang').value       = cfg.language;
  } catch (_) {}
  document.getElementById('cfg-err').textContent = '';
  openModal('settings-overlay');
}

async function saveConfig() {
  const autoLock  = parseInt(document.getElementById('cfg-auto-lock').value, 10);
  const clipClear = parseInt(document.getElementById('cfg-clip-clear').value, 10);
  const lang      = document.getElementById('cfg-lang').value;
  if (!autoLock  || autoLock  < 1) { setErr('cfg-err', t('auto_lock_min_error')); return; }
  if (!clipClear || clipClear < 5) { setErr('cfg-err', t('clip_clear_min_error')); return; }
  try {
    await invoke('set_config', { cfg: { language: lang, auto_lock_minutes: autoLock, clipboard_clear_secs: clipClear } });
    _autoLockMinutes = autoLock;
    _lang = lang;
    applyLangToPage();
    closeModal('settings-overlay');
    showToast(t('cfg_saved'));
  } catch (e) {
    setErr('cfg-err', t('cfg_save_fail') + e);
  }
}

// ==================== EXPORT ====================

async function doExportTxt() {
  try {
    const path = await window.__TAURI__.dialog.save({
      defaultPath: 'kagibox_export.txt',
      filters: [{ name: 'テキスト', extensions: ['txt'] }],
    });
    if (!path) return;

    const lines = [];
    for (const e of _entries) {
      let pw = '';
      try { pw = await invoke('reveal_password', { id: e.id }); } catch (_) {}
      lines.push(`サービス: ${e.service}`);
      if (e.username) lines.push(`ユーザー名: ${e.username}`);
      lines.push(`パスワード: ${pw}`);
      if (e.url) lines.push(`URL: ${e.url}`);
      lines.push('---');
    }
    await invoke('export_txt', { passwords: lines, path });
    closeModal('export-overlay');
    showToast(t('exported') + path);
  } catch (e) {
    document.getElementById('export-err').textContent = t('export_fail') + e;
  }
}

// ==================== AUTO-LOCK COUNTDOWN ====================

function resetActivity() { _lastActivity = Date.now(); }

function startAutoLockCountdown() {
  stopAutoLockCountdown();
  _autoLockTimer = setInterval(() => {
    const remaining = _autoLockMinutes * 60000 - (Date.now() - _lastActivity);
    const bar  = document.getElementById('autolock-bar');
    const text = document.getElementById('autolock-text');
    if (!bar) return;
    if (remaining <= 0) { stopAutoLockCountdown(); return; }
    const mins = Math.floor(remaining / 60000);
    const secs = Math.floor((remaining % 60000) / 1000);
    text.textContent = mins > 0
      ? `${mins}${t('min_label')}${t('autolock_label')}`
      : `${secs}${t('sec_label')}${t('autolock_label')}`;
    bar.className = 'autolock-bar' + (remaining < 60000 ? ' warn' : ' show');
  }, 1000);

  invoke('get_config').then(cfg => { _autoLockMinutes = cfg.auto_lock_minutes; }).catch(() => {});
}

function stopAutoLockCountdown() {
  if (_autoLockTimer) { clearInterval(_autoLockTimer); _autoLockTimer = null; }
  const bar = document.getElementById('autolock-bar');
  if (bar) bar.className = 'autolock-bar';
}

// ==================== LOCKOUT (brute-force) ====================

function isLockedOut() { return Date.now() < _lockUntil; }

function recordFail() {
  _failCount++;
  const delay = _failCount >= 10 ? 5 * 60000
              : _failCount >= 5  ? 30000
              : _failCount >= 3  ? 5000
              : 0;
  if (delay > 0) { _lockUntil = Date.now() + delay; startLockoutCountdown(); }
}

function recordSuccess() { _failCount = 0; _lockUntil = 0; }

function startLockoutCountdown() {
  if (_lockoutTimer) clearInterval(_lockoutTimer);
  const msgEl = document.getElementById('lockout-msg');
  _lockoutTimer = setInterval(() => {
    const left = Math.ceil((_lockUntil - Date.now()) / 1000);
    if (left <= 0) {
      clearInterval(_lockoutTimer); _lockoutTimer = null;
      if (msgEl) msgEl.textContent = '';
    } else {
      const m = Math.floor(left / 60), s = left % 60;
      if (msgEl) msgEl.textContent = `⚠️ ${_failCount}${t('lockout_msg')}${m > 0 ? m + t('min_label') + s + t('sec_label') : s + t('sec_label')}${t('lockout_after')}`;
    }
  }, 500);
}

// ==================== SAVE STATUS ====================

let _saveTimer = null;
function setSaveStatus(state, msg) {
  const el   = document.getElementById('save-status');
  const text = document.getElementById('save-status-text');
  if (!el) return;
  el.className = 'save-status' + (state !== 'idle' ? ' ' + state : '');
  text.textContent = state === 'saving' ? t('save_saving')
                   : state === 'saved'  ? t('save_saved')
                   : state === 'error'  ? t('save_error') + (msg || '')
                   : '';
  clearTimeout(_saveTimer);
  if (state === 'saved') _saveTimer = setTimeout(() => setSaveStatus('idle'), 2000);
}

// ==================== TOAST ====================

let _toastTimer = null;
function showToast(msg) {
  const el = document.getElementById('toast');
  if (!el) return;
  el.textContent = msg;
  el.classList.add('show');
  clearTimeout(_toastTimer);
  _toastTimer = setTimeout(() => el.classList.remove('show'), 2500);
}

// ==================== HELPERS ====================

function openModal(id)  { document.getElementById(id).classList.add('show'); }
function closeModal(id) { document.getElementById(id).classList.remove('show'); }
function setErr(id, msg) { const el = document.getElementById(id); if (el) el.textContent = msg; }

function makeEl(tag, cls) {
  const el = document.createElement(tag);
  if (cls) el.className = cls;
  return el;
}

function makeBtn(text, cls, onClick) {
  const btn = makeEl('button', cls);
  btn.textContent = text;
  btn.onclick = onClick;
  return btn;
}

function toggleInput(id, btn) {
  const inp = document.getElementById(id);
  const isHidden = inp.type === 'password';
  inp.type = isHidden ? 'text' : 'password';
  btn.innerHTML = isHidden
    ? `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>`
    : `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>`;
  inp.focus();
}

const COLORS = ['#6366f1','#0ea5e9','#10b981','#f59e0b','#ef4444','#8b5cf6','#ec4899','#14b8a6','#f97316','#84cc16'];
function colorFor(name) {
  let h = 0;
  for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) | 0;
  return COLORS[Math.abs(h) % COLORS.length];
}

function makeServiceDot(svc) {
  const dot = makeEl('div', 'svc-dot');
  const icon = getServiceIcon(svc);
  if (icon) {
    dot.classList.add('has-icon');
    dot.style.backgroundImage = `url('${icon}')`;
  } else {
    dot.style.background = colorFor(svc);
    dot.textContent = svc[0].toUpperCase();
  }
  return dot;
}

function getServiceIcon(svc) {
  const entry = _entries.find(e => e.service === svc && e.icon);
  return entry ? entry.icon : null;
}

function applyIconPreview(dataUrl) {
  const box = document.getElementById('edit-icon-preview');
  const clearBtn = document.getElementById('edit-icon-clear');
  if (!box) return;
  if (dataUrl) {
    box.style.backgroundImage = `url('${dataUrl}')`;
    if (clearBtn) clearBtn.style.display = 'inline-flex';
  } else {
    box.style.backgroundImage = '';
    if (clearBtn) clearBtn.style.display = 'none';
  }
}

function pickIcon() { document.getElementById('edit-icon-input').click(); }

function clearIcon() {
  _editingIcon = null;
  applyIconPreview(null);
  document.getElementById('edit-icon-input').value = '';
}

// ==================== LANGUAGE SELECTION ====================

async function selectLang(lang) {
  _lang = lang;
  try {
    await invoke('set_config', { cfg: { language: lang, auto_lock_minutes: 5, clipboard_clear_secs: 20 } });
  } catch (_) {}
  applyLangToPage();
  closeModal('lang-overlay');
  showLockScreen(true);  // true = show setup screen (no vault yet)
}

// ==================== SYMBOL PICKER ====================

function toggleSymbolPicker() {
  const show = document.getElementById('gen-symbols').checked;
  const picker = document.getElementById('gen-symbol-picker');
  if (picker) picker.style.display = show ? 'flex' : 'none';
}

// ==================== GENERATOR EXPORT ====================

async function exportGeneratorResults() {
  const rows = [...document.querySelectorAll('.gen-result-pw')].map(el => el.textContent);
  if (!rows.length) return;
  try {
    const path = await window.__TAURI__.dialog.save({
      defaultPath: 'passwords.txt',
      filters: [{ name: 'Text', extensions: ['txt'] }],
    });
    if (!path) return;
    await invoke('export_txt', { passwords: rows, path });
    showToast(t('exported') + path);
  } catch (e) {
    showToast(t('export_fail') + e);
  }
}
