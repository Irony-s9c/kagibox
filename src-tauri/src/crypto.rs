// src-tauri/src/crypto.rs
use aes_gcm::{aead::{Aead, KeyInit}, Aes256Gcm, Key, Nonce};
use argon2::{Argon2, Algorithm, Params, Version};
use rand::{rngs::OsRng, RngCore};
use zeroize::Zeroize;

pub struct KdfParams {
    pub m_kib: u32,   // 65536
    pub t: u32,       // 3
    pub p: u32,       // 1
    pub salt: [u8; 16],
}

pub fn random_salt() -> [u8; 16] {
    let mut s = [0u8; 16];
    OsRng.fill_bytes(&mut s);
    s
}

pub fn random_nonce() -> [u8; 12] {
    let mut n = [0u8; 12];
    OsRng.fill_bytes(&mut n);
    n
}

/// マスターパスワード -> 32 バイト鍵（Argon2id）
pub fn derive_key(master: &str, p: &KdfParams) -> Result<[u8; 32], String> {
    let params = Params::new(p.m_kib, p.t, p.p, Some(32))
        .map_err(|e| e.to_string())?;
    let argon = Argon2::new(Algorithm::Argon2id, Version::V0x13, params);
    let mut key = [0u8; 32];
    argon
        .hash_password_into(master.as_bytes(), &p.salt, &mut key)
        .map_err(|e| e.to_string())?;
    Ok(key)
}

/// 平文 -> (nonce, ciphertext)
pub fn encrypt(key: &[u8; 32], plaintext: &[u8]) -> Result<([u8; 12], Vec<u8>), String> {
    let cipher = Aes256Gcm::new(Key::<Aes256Gcm>::from_slice(key));
    let nonce_bytes = random_nonce();
    let nonce = Nonce::from_slice(&nonce_bytes);
    let ct = cipher.encrypt(nonce, plaintext).map_err(|_| "encrypt failed".to_string())?;
    Ok((nonce_bytes, ct))
}

/// (nonce, ciphertext) -> 平文。失敗 = パスワード違い or 改ざん
pub fn decrypt(key: &[u8; 32], nonce_bytes: &[u8; 12], ct: &[u8]) -> Result<Vec<u8>, String> {
    let cipher = Aes256Gcm::new(Key::<Aes256Gcm>::from_slice(key));
    let nonce = Nonce::from_slice(nonce_bytes);
    cipher.decrypt(nonce, ct).map_err(|_| "decrypt failed".to_string())
}

/// 使い終わった鍵を消す
pub fn wipe(mut key: [u8; 32]) {
    key.zeroize();
}

#[cfg(test)]
mod tests {
    use super::*;

    /// テスト用の高速パラメータ（m=64KiB, t=1）
    fn fast_params(salt: [u8; 16]) -> KdfParams {
        KdfParams { m_kib: 64, t: 1, p: 1, salt }
    }

    // ---- derive_key ----

    #[test]
    fn derive_deterministic_same_password_same_salt() {
        let p = fast_params([1u8; 16]);
        let k1 = derive_key("correct-horse-battery-staple", &p).unwrap();
        let k2 = derive_key("correct-horse-battery-staple", &p).unwrap();
        assert_eq!(k1, k2, "同じパスワード+ソルトなら鍵は同一");
    }

    #[test]
    fn derive_different_for_different_passwords() {
        let p = fast_params([0u8; 16]);
        let k1 = derive_key("password1", &p).unwrap();
        let k2 = derive_key("password2", &p).unwrap();
        assert_ne!(k1, k2, "パスワードが違えば鍵は異なる");
    }

    #[test]
    fn derive_different_for_different_salts() {
        let k1 = derive_key("same", &fast_params([0u8; 16])).unwrap();
        let k2 = derive_key("same", &fast_params([1u8; 16])).unwrap();
        assert_ne!(k1, k2, "ソルトが違えば鍵は異なる");
    }

    #[test]
    fn derived_key_is_32_bytes() {
        let k = derive_key("pw", &fast_params([0u8; 16])).unwrap();
        assert_eq!(k.len(), 32);
    }

    // ---- encrypt / decrypt ----

    #[test]
    fn encrypt_decrypt_roundtrip() {
        let key = derive_key("master", &fast_params([42u8; 16])).unwrap();
        let plaintext = b"Hello, KagiBox!";
        let (nonce, ct) = encrypt(&key, plaintext).unwrap();
        let decrypted = decrypt(&key, &nonce, &ct).unwrap();
        assert_eq!(plaintext.as_slice(), decrypted.as_slice());
    }

    #[test]
    fn encrypt_decrypt_empty_plaintext() {
        let key = derive_key("pw", &fast_params([0u8; 16])).unwrap();
        let (nonce, ct) = encrypt(&key, b"").unwrap();
        let dec = decrypt(&key, &nonce, &ct).unwrap();
        assert!(dec.is_empty());
    }

    #[test]
    fn encrypt_decrypt_long_plaintext() {
        let key = derive_key("pw", &fast_params([0u8; 16])).unwrap();
        let pt = vec![0xABu8; 100_000];
        let (nonce, ct) = encrypt(&key, &pt).unwrap();
        let dec = decrypt(&key, &nonce, &ct).unwrap();
        assert_eq!(pt, dec);
    }

    #[test]
    fn decrypt_fails_with_wrong_key() {
        let k1 = derive_key("correct", &fast_params([0u8; 16])).unwrap();
        let k2 = derive_key("wrong",   &fast_params([0u8; 16])).unwrap();
        let (nonce, ct) = encrypt(&k1, b"secret").unwrap();
        assert!(decrypt(&k2, &nonce, &ct).is_err(), "誤パスワードで復号は失敗すること");
    }

    #[test]
    fn decrypt_fails_with_tampered_ciphertext() {
        let key = derive_key("pw", &fast_params([0u8; 16])).unwrap();
        let (nonce, mut ct) = encrypt(&key, b"data").unwrap();
        ct[0] ^= 0xFF; // 1 ビット改ざん
        assert!(decrypt(&key, &nonce, &ct).is_err(), "改ざんで復号は失敗すること");
    }

    #[test]
    fn encrypt_produces_different_nonces() {
        let key = derive_key("pw", &fast_params([0u8; 16])).unwrap();
        let (n1, _) = encrypt(&key, b"x").unwrap();
        let (n2, _) = encrypt(&key, b"x").unwrap();
        // nonce は毎回ランダム生成されるため異なるはず（確率的に同一は 2^96 分の 1）
        assert_ne!(n1, n2, "nonce は保存のたびに新規生成");
    }

    // ---- random helpers ----

    #[test]
    fn random_salt_is_16_bytes() {
        assert_eq!(random_salt().len(), 16);
    }

    #[test]
    fn random_nonce_is_12_bytes() {
        assert_eq!(random_nonce().len(), 12);
    }

    #[test]
    fn wipe_does_not_panic() {
        let key = derive_key("pw", &fast_params([0u8; 16])).unwrap();
        wipe(key); // パニックしないことを確認
    }
}
