// src-tauri/src/generator.rs
use rand::{rngs::OsRng, RngCore};
use serde::Deserialize;

const DIGITS: &str = "0123456789";
const UPPER:  &str = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
const LOWER:  &str = "abcdefghijklmnopqrstuvwxyz";
pub const SYMBOLS: &str = "/*-+.,!#$%&()~|_"; // Akiさん指定の記号セット
const SIMILAR: &[char] = &['I', 'l', '1', 'O', 'o', '0'];

#[derive(Deserialize, Clone, Copy, PartialEq)]
pub enum FirstCharKind { None, Upper, Lower, Digit }

#[derive(Deserialize)]
pub struct GenOptions {
    pub length: u32,            // 1..=40
    pub count: u32,             // 1..=1000
    pub use_digits: bool,
    pub use_upper: bool,
    pub use_lower: bool,
    pub use_symbols: bool,
    pub allowed_symbols: String,// 任意選択された記号（use_symbols=true のとき）
    pub exclude_similar: bool,
    pub first_char: FirstCharKind,
}

/// 0..max の一様乱数（rejection sampling で偏りゼロ）
fn uniform(max: u32) -> u32 {
    debug_assert!(max > 0);
    let zone = u32::MAX - (u32::MAX % max);
    loop {
        let mut b = [0u8; 4];
        OsRng.fill_bytes(&mut b);
        let v = u32::from_le_bytes(b);
        if v < zone { return v % max; }
    }
}

fn pick(pool: &[char]) -> char { pool[uniform(pool.len() as u32) as usize] }

fn filtered(set: &str, exclude_similar: bool) -> Vec<char> {
    set.chars()
        .filter(|c| !(exclude_similar && SIMILAR.contains(c)))
        .collect()
}

pub fn generate_one(opt: &GenOptions) -> Result<String, String> {
    let len = opt.length.clamp(1, 40) as usize;

    // 各文字種プール（似た文字除外を適用）
    let mut classes: Vec<Vec<char>> = Vec::new();
    if opt.use_digits { classes.push(filtered(DIGITS, opt.exclude_similar)); }
    if opt.use_upper  { classes.push(filtered(UPPER,  opt.exclude_similar)); }
    if opt.use_lower  { classes.push(filtered(LOWER,  opt.exclude_similar)); }
    if opt.use_symbols {
        let base = if opt.allowed_symbols.is_empty() { SYMBOLS } else { opt.allowed_symbols.as_str() };
        // 指定記号のうち SYMBOLS に含まれるものだけ許可
        let syms: Vec<char> = base.chars().filter(|c| SYMBOLS.contains(*c)).collect();
        if !syms.is_empty() { classes.push(syms); }
    }
    if classes.is_empty() { return Err("文字種が1つも選択されていません".into()); }

    let all: Vec<char> = classes.iter().flatten().copied().collect();
    if all.is_empty() { return Err("使用可能な文字がありません".into()); }

    let mut out: Vec<char> = Vec::with_capacity(len);

    // 頭文字の指定
    let first_pool: Option<Vec<char>> = match opt.first_char {
        FirstCharKind::None  => None,
        FirstCharKind::Upper => Some(filtered(UPPER,  opt.exclude_similar)),
        FirstCharKind::Lower => Some(filtered(LOWER,  opt.exclude_similar)),
        FirstCharKind::Digit => Some(filtered(DIGITS, opt.exclude_similar)),
    };
    if let Some(ref fp) = first_pool {
        if fp.is_empty() { return Err("頭文字に使える文字がありません".into()); }
        out.push(pick(fp));
    }

    // 各選択文字種を最低1文字（長さに余裕がある範囲で）
    for cls in &classes {
        if out.len() >= len { break; }
        out.push(pick(cls));
    }
    // 残りを全プールから
    while out.len() < len { out.push(pick(&all)); }

    // 頭文字を固定したまま、それ以降を Fisher–Yates でシャッフル
    let start = if first_pool.is_some() { 1 } else { 0 };
    for i in (start + 1..out.len()).rev() {
        let j = start + uniform((i - start + 1) as u32) as usize;
        out.swap(i, j);
    }

    Ok(out.into_iter().collect())
}

pub fn generate_many(opt: &GenOptions) -> Result<Vec<String>, String> {
    let count = opt.count.clamp(1, 1000);
    (0..count).map(|_| generate_one(opt)).collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn base_opt() -> GenOptions {
        GenOptions {
            length: 16,
            count: 1,
            use_digits: true,
            use_upper: true,
            use_lower: true,
            use_symbols: false,
            allowed_symbols: String::new(),
            exclude_similar: false,
            first_char: FirstCharKind::None,
        }
    }

    // ---- 長さ・個数の境界 ----

    #[test]
    fn length_boundary_1() {
        let mut opt = base_opt();
        opt.length = 1;
        let r = generate_one(&opt).unwrap();
        assert_eq!(r.len(), 1, "length=1 で1文字");
    }

    #[test]
    fn length_boundary_40() {
        let mut opt = base_opt();
        opt.length = 40;
        let r = generate_one(&opt).unwrap();
        assert_eq!(r.len(), 40, "length=40 で40文字");
    }

    #[test]
    fn length_clamped_above_40() {
        // clamp により 41 → 40
        let mut opt = base_opt();
        opt.length = 41;
        let r = generate_one(&opt).unwrap();
        assert_eq!(r.len(), 40);
    }

    #[test]
    fn count_boundary_1() {
        let mut opt = base_opt();
        opt.count = 1;
        let rs = generate_many(&opt).unwrap();
        assert_eq!(rs.len(), 1);
    }

    #[test]
    fn count_boundary_1000() {
        let mut opt = base_opt();
        opt.count = 1000;
        let rs = generate_many(&opt).unwrap();
        assert_eq!(rs.len(), 1000);
        for r in &rs { assert_eq!(r.len(), 16); }
    }

    #[test]
    fn count_clamped_above_1000() {
        let mut opt = base_opt();
        opt.count = 1001;
        let rs = generate_many(&opt).unwrap();
        assert_eq!(rs.len(), 1000);
    }

    // ---- 文字種の包含確認 ----

    #[test]
    fn digits_only_chars_are_digits() {
        let opt = GenOptions {
            use_digits: true, use_upper: false, use_lower: false, use_symbols: false,
            ..base_opt()
        };
        let r = generate_one(&opt).unwrap();
        assert!(r.chars().all(|c| c.is_ascii_digit()), "数字のみ設定: {r}");
    }

    #[test]
    fn upper_only_chars_are_uppercase() {
        let opt = GenOptions {
            use_digits: false, use_upper: true, use_lower: false, use_symbols: false,
            ..base_opt()
        };
        let r = generate_one(&opt).unwrap();
        assert!(r.chars().all(|c| c.is_ascii_uppercase()), "英大文字のみ設定: {r}");
    }

    #[test]
    fn lower_only_chars_are_lowercase() {
        let opt = GenOptions {
            use_digits: false, use_upper: false, use_lower: true, use_symbols: false,
            ..base_opt()
        };
        let r = generate_one(&opt).unwrap();
        assert!(r.chars().all(|c| c.is_ascii_lowercase()), "英小文字のみ設定: {r}");
    }

    #[test]
    fn symbols_only_chars_are_in_symbol_set() {
        let opt = GenOptions {
            use_digits: false, use_upper: false, use_lower: false,
            use_symbols: true, allowed_symbols: SYMBOLS.to_string(),
            ..base_opt()
        };
        let r = generate_one(&opt).unwrap();
        assert!(r.chars().all(|c| SYMBOLS.contains(c)), "記号のみ設定: {r}");
    }

    #[test]
    fn allowed_symbols_subset_respected() {
        // "!#$" だけ許可
        let opt = GenOptions {
            use_digits: false, use_upper: false, use_lower: false,
            use_symbols: true, allowed_symbols: "!#$".to_string(),
            length: 20, count: 50,
            ..base_opt()
        };
        let rs = generate_many(&opt).unwrap();
        for r in rs {
            assert!(r.chars().all(|c| "!#$".contains(c)), "許可外の記号を含む: {r}");
        }
    }

    #[test]
    fn symbols_outside_spec_set_are_rejected() {
        // '@' は SYMBOLS に入っていないので除外される → 空になりエラー
        let opt = GenOptions {
            use_digits: false, use_upper: false, use_lower: false,
            use_symbols: true, allowed_symbols: "@".to_string(),
            ..base_opt()
        };
        assert!(generate_one(&opt).is_err());
    }

    // ---- 各文字種を最低1文字含む（長さが余裕ある場合） ----

    #[test]
    fn all_classes_represented_when_length_equals_class_count() {
        // 3文字種 × length=3 → 各1文字必ず含む
        let opt = GenOptions {
            length: 3, count: 100,
            use_digits: true, use_upper: true, use_lower: true, use_symbols: false,
            ..base_opt()
        };
        let rs = generate_many(&opt).unwrap();
        for r in rs {
            assert_eq!(r.len(), 3);
            assert!(r.chars().any(|c| c.is_ascii_digit()),    "数字なし: {r}");
            assert!(r.chars().any(|c| c.is_ascii_uppercase()), "英大文字なし: {r}");
            assert!(r.chars().any(|c| c.is_ascii_lowercase()), "英小文字なし: {r}");
        }
    }

    #[test]
    fn all_classes_represented_with_length_16() {
        let opt = GenOptions {
            length: 16, count: 100,
            use_digits: true, use_upper: true, use_lower: true, use_symbols: false,
            ..base_opt()
        };
        let rs = generate_many(&opt).unwrap();
        for r in rs {
            assert!(r.chars().any(|c| c.is_ascii_digit()),    "数字なし: {r}");
            assert!(r.chars().any(|c| c.is_ascii_uppercase()), "英大文字なし: {r}");
            assert!(r.chars().any(|c| c.is_ascii_lowercase()), "英小文字なし: {r}");
        }
    }

    // ---- 似た文字除外 ----

    #[test]
    fn exclude_similar_removes_lookalike_chars() {
        let opt = GenOptions {
            exclude_similar: true, length: 40, count: 200,
            ..base_opt()
        };
        let rs = generate_many(&opt).unwrap();
        for r in rs {
            assert!(!r.contains('I'), "I を含む: {r}");
            assert!(!r.contains('l'), "l を含む: {r}");
            assert!(!r.contains('1'), "1 を含む: {r}");
            assert!(!r.contains('O'), "O を含む: {r}");
            assert!(!r.contains('o'), "o を含む: {r}");
            assert!(!r.contains('0'), "0 を含む: {r}");
        }
    }

    #[test]
    fn exclude_similar_with_upper_first_char() {
        let opt = GenOptions {
            exclude_similar: true, first_char: FirstCharKind::Upper,
            length: 10, count: 200,
            ..base_opt()
        };
        let rs = generate_many(&opt).unwrap();
        for r in rs {
            let first = r.chars().next().unwrap();
            assert!(first.is_ascii_uppercase(), "頭文字が英大文字でない: {r}");
            assert!(first != 'I' && first != 'O', "頭文字が除外対象: {r}");
        }
    }

    // ---- 頭文字指定 ----

    #[test]
    fn first_char_upper_always_uppercase() {
        let opt = GenOptions {
            first_char: FirstCharKind::Upper, length: 16, count: 200,
            ..base_opt()
        };
        let rs = generate_many(&opt).unwrap();
        for r in rs {
            assert!(r.chars().next().unwrap().is_ascii_uppercase(), "頭文字が英大文字でない: {r}");
        }
    }

    #[test]
    fn first_char_lower_always_lowercase() {
        let opt = GenOptions {
            first_char: FirstCharKind::Lower, length: 16, count: 200,
            ..base_opt()
        };
        let rs = generate_many(&opt).unwrap();
        for r in rs {
            assert!(r.chars().next().unwrap().is_ascii_lowercase(), "頭文字が英小文字でない: {r}");
        }
    }

    #[test]
    fn first_char_digit_always_digit() {
        let opt = GenOptions {
            first_char: FirstCharKind::Digit, length: 16, count: 200,
            ..base_opt()
        };
        let rs = generate_many(&opt).unwrap();
        for r in rs {
            assert!(r.chars().next().unwrap().is_ascii_digit(), "頭文字が数字でない: {r}");
        }
    }

    #[test]
    fn first_char_preserves_length() {
        let opt = GenOptions {
            first_char: FirstCharKind::Upper, length: 10, count: 50,
            ..base_opt()
        };
        let rs = generate_many(&opt).unwrap();
        for r in rs { assert_eq!(r.len(), 10, "文字数が 10 でない: {r}"); }
    }

    // ---- エラーケース ----

    #[test]
    fn no_char_types_selected_returns_error() {
        let opt = GenOptions {
            use_digits: false, use_upper: false, use_lower: false, use_symbols: false,
            ..base_opt()
        };
        assert!(generate_one(&opt).is_err(), "文字種なしはエラー");
    }

    #[test]
    fn empty_symbol_set_after_filter_falls_back_to_error() {
        // use_symbols=true だが SYMBOLS に含まれない文字だけ → 空 → クラスが0になりエラー
        let opt = GenOptions {
            use_digits: false, use_upper: false, use_lower: false,
            use_symbols: true, allowed_symbols: "@^".to_string(),
            ..base_opt()
        };
        assert!(generate_one(&opt).is_err());
    }

    // ---- 偏りの無さ（統計的に分布が均等か簡易確認） ----

    #[test]
    fn distribution_bias_check_digits() {
        // generate_many は count を 1000 にクランプするため generate_one を直接 10000 回呼ぶ
        // 数字 0-9 で length=1 → 各数字が 700〜1300 回程度出るはず（±10σ 以上のはずれは起こらない）
        let opt = GenOptions {
            use_digits: true, use_upper: false, use_lower: false, use_symbols: false,
            length: 1, count: 1,
            ..base_opt()
        };
        let mut freq = [0u32; 10];
        for _ in 0..10_000 {
            let r = generate_one(&opt).unwrap();
            let d = r.chars().next().unwrap() as u8 - b'0';
            freq[d as usize] += 1;
        }
        for (i, &f) in freq.iter().enumerate() {
            assert!(f > 700 && f < 1300,
                "数字 {i} の出現回数 {f}/10000 が期待範囲 700-1300 を外れた（偏りの可能性）");
        }
    }

    // ---- 結果の多様性 ----

    #[test]
    fn generated_passwords_are_diverse() {
        let opt = GenOptions { length: 16, count: 20, ..base_opt() };
        let rs = generate_many(&opt).unwrap();
        let first = &rs[0];
        // 20 件中すべてが同一の確率は天文学的に低い
        assert!(rs.iter().any(|r| r != first), "全件が同一パスワード（乱数が固定されている可能性）");
    }
}
