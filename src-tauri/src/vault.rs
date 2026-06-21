// vault.rs – 保管庫のデータ型定義
use serde::{Deserialize, Serialize};

/// Vault に保存するエントリ（平文は Rust メモリのみ）
#[derive(Serialize, Deserialize, Clone)]
pub struct Entry {
    pub id: String,
    pub service: String,
    pub username: String,
    pub password: String,
    pub url: String,
    pub memo: String,
    #[serde(default)]
    pub icon: Option<String>,
}

/// フロントに渡す要約（パスワードを含まない）
#[derive(Serialize, Clone)]
pub struct EntrySummary {
    pub id: String,
    pub service: String,
    pub username: String,
    pub url: String,
    pub memo: String,
    pub icon: Option<String>,
}

/// vault.kagi ファイルの JSON 構造（SPEC §2.3）
#[derive(Serialize, Deserialize)]
pub struct VaultFile {
    pub magic: String,
    pub format_version: u32,
    pub kdf: KdfMeta,
    pub cipher: String,
    pub nonce_b64: String,
    pub ciphertext_b64: String,
}

#[derive(Serialize, Deserialize)]
pub struct KdfMeta {
    pub algo: String,
    pub m_kib: u32,
    pub t: u32,
    pub p: u32,
    pub salt_b64: String,
}

/// アンロック時に記憶しておく KDF パラメータ（再保存時に使う）
#[derive(Clone)]
pub struct StoredKdfParams {
    pub m_kib: u32,
    pub t: u32,
    pub p: u32,
    pub salt: [u8; 16],
}
