// commands.rs – Tauri コマンド / AppState / 自動ロック / クリップボード自動クリア
use std::sync::Mutex;
use std::path::PathBuf;
use base64::{engine::general_purpose::STANDARD as B64, Engine};
use rand::RngCore;
use serde::{Deserialize, Serialize};
use tauri::{Emitter, Manager};
use zeroize::Zeroize;

use crate::crypto;
use crate::generator::{GenOptions, generate_many};
use crate::vault::{Entry, EntrySummary, KdfMeta, StoredKdfParams, VaultFile};

// ==================== AppState ====================

pub struct AppState {
    /// 導出鍵（None = ロック中）
    pub key: Option<[u8; 32]>,
    /// アンロック時に記憶した KDF パラメータ（再保存のため）
    pub kdf_params: Option<StoredKdfParams>,
    /// 復号済みエントリ（Rust メモリ内だけ）
    pub entries: Vec<Entry>,
    /// 最終操作時刻（自動ロック判定）
    pub last_activity: std::time::Instant,
    /// 自動ロックまでの時間（分）
    pub auto_lock_minutes: u64,
    /// クリップボード自動クリアまでの時間（秒）
    pub clipboard_clear_secs: u64,
}

impl Default for AppState {
    fn default() -> Self {
        Self {
            key: None,
            kdf_params: None,
            entries: Vec::new(),
            last_activity: std::time::Instant::now(),
            auto_lock_minutes: 5,
            clipboard_clear_secs: 20,
        }
    }
}

impl AppState {
    pub fn do_lock(&mut self) {
        if let Some(mut k) = self.key.take() {
            k.zeroize();
        }
        self.kdf_params = None;
        self.entries.clear();
    }

    pub fn touch(&mut self) {
        self.last_activity = std::time::Instant::now();
    }

    pub fn is_unlocked(&self) -> bool {
        self.key.is_some()
    }
}

// ==================== 設定 ====================

#[derive(Serialize, Deserialize, Clone)]
pub struct Config {
    pub language: String,
    pub auto_lock_minutes: u64,
    pub clipboard_clear_secs: u64,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            language: "ja".into(),
            auto_lock_minutes: 5,
            clipboard_clear_secs: 20,
        }
    }
}

// ==================== ユーティリティ ====================

fn get_vault_path(app: &tauri::AppHandle) -> Result<PathBuf, String> {
    let mut p = app.path().app_data_dir().map_err(|e| e.to_string())?;
    p.push("vault.kagi");
    Ok(p)
}

fn get_config_path(app: &tauri::AppHandle) -> Result<PathBuf, String> {
    let mut p = app.path().app_data_dir().map_err(|e| e.to_string())?;
    p.push("config.json");
    Ok(p)
}

fn ensure_app_dir(app: &tauri::AppHandle) -> Result<(), String> {
    let dir = app.path().app_data_dir().map_err(|e| e.to_string())?;
    std::fs::create_dir_all(dir).map_err(|e| e.to_string())
}

pub fn load_config(app: &tauri::AppHandle) -> Config {
    let path = match get_config_path(app) {
        Ok(p) => p,
        Err(_) => return Config::default(),
    };
    std::fs::read_to_string(&path)
        .ok()
        .and_then(|s| serde_json::from_str(&s).ok())
        .unwrap_or_default()
}

fn save_config_file(app: &tauri::AppHandle, cfg: &Config) -> Result<(), String> {
    ensure_app_dir(app)?;
    let path = get_config_path(app)?;
    let json = serde_json::to_string_pretty(cfg).map_err(|e| e.to_string())?;
    std::fs::write(path, json).map_err(|e| e.to_string())
}

/// CSPRNG で UUID v4 相当のランダム ID を生成
fn new_id() -> String {
    let mut b = [0u8; 16];
    rand::rngs::OsRng.fill_bytes(&mut b);
    b[6] = (b[6] & 0x0f) | 0x40; // version 4
    b[8] = (b[8] & 0x3f) | 0x80; // variant
    format!(
        "{:08x}-{:04x}-{:04x}-{:04x}-{:012x}",
        u32::from_be_bytes([b[0], b[1], b[2], b[3]]),
        u16::from_be_bytes([b[4], b[5]]),
        u16::from_be_bytes([b[6], b[7]]),
        u16::from_be_bytes([b[8], b[9]]),
        {
            let mut arr = [0u8; 8];
            arr[2..].copy_from_slice(&b[10..16]);
            u64::from_be_bytes(arr)
        }
    )
}

/// Vault ファイルへの書き込み（毎回 nonce を新規生成）
fn write_vault_file(
    app: &tauri::AppHandle,
    key: &[u8; 32],
    kdf: &StoredKdfParams,
    entries: &[Entry],
) -> Result<(), String> {
    let vault_path = get_vault_path(app)?;
    let plaintext = serde_json::to_vec(entries).map_err(|e| e.to_string())?;
    let (nonce, ciphertext) = crypto::encrypt(key, &plaintext)?;

    let vault_file = VaultFile {
        magic: "KAGIBOX".into(),
        format_version: 1,
        kdf: KdfMeta {
            algo: "argon2id".into(),
            m_kib: kdf.m_kib,
            t: kdf.t,
            p: kdf.p,
            salt_b64: B64.encode(kdf.salt),
        },
        cipher: "aes-256-gcm".into(),
        nonce_b64: B64.encode(nonce),
        ciphertext_b64: B64.encode(&ciphertext),
    };

    ensure_app_dir(app)?;
    let json = serde_json::to_string_pretty(&vault_file).map_err(|e| e.to_string())?;
    std::fs::write(&vault_path, json).map_err(|e| e.to_string())
}

// ==================== TAURI COMMANDS ====================

/// Vault ファイルが既に存在するか（初回起動判定）
#[tauri::command]
pub fn is_vault_created(app: tauri::AppHandle) -> bool {
    get_vault_path(&app).map(|p| p.exists()).unwrap_or(false)
}

/// マスターパスワードでアンロック / 初回は新規 Vault を作成
#[tauri::command]
pub async fn unlock(
    master: String,
    app: tauri::AppHandle,
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<(), String> {
    let vault_path = get_vault_path(&app)?;

    if !vault_path.exists() {
        // ===== 初回: 新規 Vault 作成 =====
        let salt = crypto::random_salt();
        let kdf_params_for_derive = crypto::KdfParams {
            m_kib: 65536,
            t: 3,
            p: 1,
            salt,
        };
        // KDF は CPU 集約 → blocking スレッドで実行
        let key = tokio::task::spawn_blocking(move || {
            crypto::derive_key(&master, &kdf_params_for_derive)
        })
        .await
        .map_err(|e| e.to_string())??;

        let entries: Vec<Entry> = Vec::new();
        write_vault_file(
            &app,
            &key,
            &StoredKdfParams { m_kib: 65536, t: 3, p: 1, salt },
            &entries,
        )?;

        let cfg = load_config(&app);
        let mut guard = state.lock().unwrap();
        guard.key = Some(key);
        guard.kdf_params = Some(StoredKdfParams { m_kib: 65536, t: 3, p: 1, salt });
        guard.entries = entries;
        guard.auto_lock_minutes = cfg.auto_lock_minutes;
        guard.clipboard_clear_secs = cfg.clipboard_clear_secs;
        guard.touch();
    } else {
        // ===== 既存 Vault 読み込み =====
        let content =
            std::fs::read_to_string(&vault_path).map_err(|e| e.to_string())?;
        let vault_file: VaultFile =
            serde_json::from_str(&content).map_err(|e| e.to_string())?;

        if vault_file.magic != "KAGIBOX" {
            return Err("invalid-vault".into());
        }

        let salt_bytes = B64
            .decode(&vault_file.kdf.salt_b64)
            .map_err(|e| e.to_string())?;
        let salt: [u8; 16] = salt_bytes
            .try_into()
            .map_err(|_| "invalid-salt-length".to_string())?;

        let m_kib = vault_file.kdf.m_kib;
        let t = vault_file.kdf.t;
        let p = vault_file.kdf.p;

        // KDF → blocking
        let key = tokio::task::spawn_blocking(move || {
            crypto::derive_key(&master, &crypto::KdfParams { m_kib, t, p, salt })
        })
        .await
        .map_err(|e| e.to_string())??;

        let nonce_bytes = B64
            .decode(&vault_file.nonce_b64)
            .map_err(|e| e.to_string())?;
        let nonce: [u8; 12] = nonce_bytes
            .try_into()
            .map_err(|_| "invalid-nonce-length".to_string())?;
        let ciphertext = B64
            .decode(&vault_file.ciphertext_b64)
            .map_err(|e| e.to_string())?;

        let plaintext = crypto::decrypt(&key, &nonce, &ciphertext)
            .map_err(|_| "wrong-password".to_string())?;

        let entries: Vec<Entry> =
            serde_json::from_slice(&plaintext).map_err(|e| e.to_string())?;

        let cfg = load_config(&app);
        let mut guard = state.lock().unwrap();
        guard.key = Some(key);
        guard.kdf_params = Some(StoredKdfParams { m_kib, t, p, salt });
        guard.entries = entries;
        guard.auto_lock_minutes = cfg.auto_lock_minutes;
        guard.clipboard_clear_secs = cfg.clipboard_clear_secs;
        guard.touch();
    }

    Ok(())
}

/// 手動ロック（鍵・エントリをメモリから消去）
#[tauri::command]
pub fn lock(state: tauri::State<'_, Mutex<AppState>>) {
    state.lock().unwrap().do_lock();
}

/// ロック状態確認
#[tauri::command]
pub fn is_locked(state: tauri::State<'_, Mutex<AppState>>) -> bool {
    !state.lock().unwrap().is_unlocked()
}

/// エントリ一覧（パスワードなし）
#[tauri::command]
pub fn list_entries(
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<Vec<EntrySummary>, String> {
    let mut guard = state.lock().unwrap();
    if !guard.is_unlocked() {
        return Err("locked".into());
    }
    guard.touch();
    Ok(guard
        .entries
        .iter()
        .map(|e| EntrySummary {
            id: e.id.clone(),
            service: e.service.clone(),
            username: e.username.clone(),
            url: e.url.clone(),
            memo: e.memo.clone(),
            icon: e.icon.clone(),
        })
        .collect())
}

/// パスワードを単体で返す（必要時だけ）
#[tauri::command]
pub fn reveal_password(
    id: String,
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<String, String> {
    let mut guard = state.lock().unwrap();
    if !guard.is_unlocked() {
        return Err("locked".into());
    }
    guard.touch();
    guard
        .entries
        .iter()
        .find(|e| e.id == id)
        .map(|e| e.password.clone())
        .ok_or_else(|| "not-found".into())
}

#[derive(Deserialize)]
pub struct EntryInput {
    pub service: String,
    pub username: String,
    pub password: String,
    pub url: String,
    pub memo: String,
}

/// エントリ追加 → 即 Vault 保存
#[tauri::command]
pub async fn add_entry(
    entry: EntryInput,
    app: tauri::AppHandle,
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<String, String> {
    let id = new_id();
    let (key, kdf, entries) = {
        let mut guard = state.lock().unwrap();
        if !guard.is_unlocked() {
            return Err("locked".into());
        }
        guard.touch();
        guard.entries.push(Entry {
            id: id.clone(),
            service: entry.service,
            username: entry.username,
            password: entry.password,
            url: entry.url,
            memo: entry.memo,
            icon: entry.icon,
        });
        let key = *guard.key.as_ref().unwrap();
        let kdf = guard.kdf_params.as_ref().unwrap().clone();
        let entries = guard.entries.clone();
        (key, kdf, entries)
    };
    write_vault_file(&app, &key, &kdf, &entries)?;
    Ok(id)
}

#[derive(Deserialize)]
pub struct UpdateEntryInput {
    pub id: String,
    pub service: String,
    pub username: String,
    pub password: String,
    pub url: String,
    pub memo: String,
}

/// エントリ編集 → 即 Vault 保存
#[tauri::command]
pub async fn update_entry(
    entry: UpdateEntryInput,
    app: tauri::AppHandle,
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<(), String> {
    let (key, kdf, entries) = {
        let mut guard = state.lock().unwrap();
        if !guard.is_unlocked() {
            return Err("locked".into());
        }
        guard.touch();
        let e = guard
            .entries
            .iter_mut()
            .find(|e| e.id == entry.id)
            .ok_or_else(|| "not-found".to_string())?;
        e.service = entry.service;
        e.username = entry.username;
        e.password = entry.password;
        e.url = entry.url;
        e.memo = entry.memo;
        e.icon = entry.icon;
        let key = *guard.key.as_ref().unwrap();
        let kdf = guard.kdf_params.as_ref().unwrap().clone();
        let entries = guard.entries.clone();
        (key, kdf, entries)
    };
    write_vault_file(&app, &key, &kdf, &entries)?;
    Ok(())
}

/// エントリ削除 → 即 Vault 保存
#[tauri::command]
pub async fn delete_entry(
    id: String,
    app: tauri::AppHandle,
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<(), String> {
    let (key, kdf, entries) = {
        let mut guard = state.lock().unwrap();
        if !guard.is_unlocked() {
            return Err("locked".into());
        }
        guard.touch();
        guard.entries.retain(|e| e.id != id);
        let key = *guard.key.as_ref().unwrap();
        let kdf = guard.kdf_params.as_ref().unwrap().clone();
        let entries = guard.entries.clone();
        (key, kdf, entries)
    };
    write_vault_file(&app, &key, &kdf, &entries)?;
    Ok(())
}

/// 明示的な Vault 保存（SPEC §6 準拠・通常は add/update/delete で自動保存）
#[tauri::command]
pub async fn save_vault(
    app: tauri::AppHandle,
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<(), String> {
    let (key, kdf, entries) = {
        let mut guard = state.lock().unwrap();
        if !guard.is_unlocked() {
            return Err("locked".into());
        }
        guard.touch();
        let key = *guard.key.as_ref().unwrap();
        let kdf = guard.kdf_params.as_ref().unwrap().clone();
        let entries = guard.entries.clone();
        (key, kdf, entries)
    };
    write_vault_file(&app, &key, &kdf, &entries)?;
    Ok(())
}

/// パスワード生成（Rust 側 CSPRNG）
#[tauri::command]
pub fn generate(opt: GenOptions) -> Result<Vec<String>, String> {
    generate_many(&opt)
}

/// 生成パスワードのテキストファイル保存（警告は UI 側で出してから呼ぶ）
#[tauri::command]
pub async fn export_txt(
    passwords: Vec<String>,
    path: String,
) -> Result<(), String> {
    let content = passwords.join("\n") + "\n";
    tokio::fs::write(&path, content)
        .await
        .map_err(|e| e.to_string())
}

/// クリップボードにコピーし N 秒後に自動クリア
#[tauri::command]
pub async fn copy_to_clipboard(
    text: String,
    app: tauri::AppHandle,
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<(), String> {
    let secs = {
        let mut guard = state.lock().unwrap();
        guard.touch();
        guard.clipboard_clear_secs
    };

    use tauri_plugin_clipboard_manager::ClipboardExt;
    app.clipboard().write_text(text).map_err(|e| e.to_string())?;

    // N 秒後にクリップボードをクリア
    let handle = app.clone();
    tauri::async_runtime::spawn(async move {
        tokio::time::sleep(std::time::Duration::from_secs(secs)).await;
        use tauri_plugin_clipboard_manager::ClipboardExt;
        let _ = handle.clipboard().write_text(String::new());
        let _ = handle.emit("clipboard-cleared", true);
    });

    Ok(())
}

/// 設定を返す
#[tauri::command]
pub fn get_config(app: tauri::AppHandle) -> Config {
    load_config(&app)
}

/// 設定を保存（言語・自動ロック・クリップボードクリア）
#[tauri::command]
pub fn set_config(
    cfg: Config,
    app: tauri::AppHandle,
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<(), String> {
    {
        let mut guard = state.lock().unwrap();
        guard.auto_lock_minutes = cfg.auto_lock_minutes;
        guard.clipboard_clear_secs = cfg.clipboard_clear_secs;
        guard.touch();
    }
    save_config_file(&app, &cfg)
}

/// エントリの全フィールドを検索キーワードで絞り込んだ一覧を返す
#[tauri::command]
pub fn search_entries(
    query: String,
    state: tauri::State<'_, Mutex<AppState>>,
) -> Result<Vec<EntrySummary>, String> {
    let mut guard = state.lock().unwrap();
    if !guard.is_unlocked() {
        return Err("locked".into());
    }
    guard.touch();
    let q = query.to_lowercase();
    Ok(guard
        .entries
        .iter()
        .filter(|e| {
            e.service.to_lowercase().contains(&q)
                || e.username.to_lowercase().contains(&q)
                || e.url.to_lowercase().contains(&q)
                || e.memo.to_lowercase().contains(&q)
        })
        .map(|e| EntrySummary {
            id: e.id.clone(),
            service: e.service.clone(),
            username: e.username.clone(),
            url: e.url.clone(),
            memo: e.memo.clone(),
            icon: e.icon.clone(),
        })
        .collect())
}

// ==================== アップデーター ====================

#[tauri::command]
pub async fn check_update(app: tauri::AppHandle) -> Result<Option<serde_json::Value>, String> {
    use tauri_plugin_updater::UpdaterExt;
    let updater = app.updater_builder().build().map_err(|e| e.to_string())?;
    match updater.check().await {
        Ok(Some(update)) => Ok(Some(serde_json::json!({
            "version": update.version,
            "body": update.body.clone().unwrap_or_default(),
        }))),
        Ok(None) => Ok(None),
        Err(e) => Err(e.to_string()),
    }
}

#[tauri::command]
pub async fn do_update(app: tauri::AppHandle) -> Result<(), String> {
    use tauri_plugin_updater::UpdaterExt;
    let updater = app.updater_builder().build().map_err(|e| e.to_string())?;
    if let Ok(Some(update)) = updater.check().await {
        update.download_and_install(|_, _| {}, || {}).await
            .map_err(|e| e.to_string())?;
        app.restart();
    }
    Ok(())
}

// ==================== 自動ロック バックグラウンドタスク ====================

/// setup() から呼ぶ: 10 秒ごとに無操作時間を確認し、閾値超過でロック
pub fn spawn_auto_lock_task(app: tauri::AppHandle) {
    tauri::async_runtime::spawn(async move {
        loop {
            tokio::time::sleep(std::time::Duration::from_secs(10)).await;

            let should_lock = {
                let state = app.state::<Mutex<AppState>>();
                let guard = state.lock().unwrap();
                guard.is_unlocked()
                    && guard.last_activity.elapsed().as_secs()
                        >= guard.auto_lock_minutes * 60
            };

            if should_lock {
                let state = app.state::<Mutex<AppState>>();
                state.lock().unwrap().do_lock();
                let _ = app.emit("auto-locked", true);
            }
        }
    });
}
