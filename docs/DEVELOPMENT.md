# KagiBox 開発メモ

利用者向けの説明は `README.md`。ここは開発・ビルドの手順と、設計の要点。

## 構成

| 部分 | 中身 |
|---|---|
| `ui\` | 画面。WinUI 3（Windows App SDK 2.5.1 / .NET 8 / C#）。`KagiBox.exe` |
| `core\` | 処理。Python 3.13 の `backend\`（保管庫・暗号・生成・診断・自動ロック・オートタイプ・更新）を、画面のない `KagiBox-core.exe`（PyInstaller）にしたもの |

- UI は起動時に core を子プロセスとして立ち上げ、標準入出力で 1 行 1 JSON をやり取りする（`core\kagibox_core.py`）。
  - 要求 `{"id","method","args"}` → 応答 `{"id","ok"}` / `{"id","error"}`、通知 `{"event","payload"}`（`ready` / `auto-locked` / `autotype-requested` / `fatal`）。
  - 呼べるメソッドは `ALLOWED_METHODS` の一覧だけ。標準入力が閉じると、core は鍵とクリップボードを消して終了する。
- 開発中、UI は core を `.venv` の Python で `core\kagibox_core.py` から起動する（インストール版は exe の横の `KagiBox-core.exe`）。

| 用途 | 技術 |
|---|---|
| 暗号 | AES-256-GCM（cryptography 50.0.1）、Argon2id（argon2-cffi 25.1.0） |
| その他 | keyboard 0.13.5、pywin32 312、pyperclip 1.11.0、requests 2.34.2 |
| ビルド | PyInstaller 6.22.3（`core\KagiBox-core.spec`）、`dotnet publish`（`ui\KagiBox.csproj`）、Inno Setup 6.7.3（`installer.iss`） |
| 更新確認 | GitHub Releases API。比較は SemVer 2.0.0 準拠の `core\backend\semver.py`。ダウンロードは GitHub が公開する SHA-256 で検証 |

Python の依存は `requirements.txt` に `==` で固定（間接依存を含む）。

## 開発環境

Python 3.13.15、.NET SDK 8.0.425、Inno Setup 6.7.3。

```
py -3.13 -m venv .venv
.venv\Scripts\python.exe -m pip install -r requirements.txt
dotnet restore ui\KagiBox.csproj
```

## ビルド

```
.venv\Scripts\python.exe -m PyInstaller --noconfirm core\KagiBox-core.spec --distpath build\core-dist --workpath build\core-work
dotnet publish ui\KagiBox.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:DebugType=None -o build\ui-publish
```

`build\ui-publish\` と `build\core-dist\KagiBox-core\` の中身を `dist\KagiBox\` にまとめ、`ISCC.exe installer.iss` で `installer_output\KagiBox-Setup-<version>.exe` を作る。

## テスト

| コマンド | 内容 |
|---|---|
| `.venv\Scripts\python.exe tests\test_core_ipc.py [KagiBox-core.exe]` | core の通信（一時フォルダの保管庫を使う） |
| `scripts\smoke-test.ps1` | 副モニターにフォーカスを奪わず表示してスクリーンショット。`-Seed -Unlock -Page generator` などで各画面（`-Unlock` / `-Page` は Debug ビルドのみ） |
| `scripts\install-test.ps1` | インストーラーを一時フォルダに現在のユーザーだけで無音インストール → 起動確認 → アンインストール |

`tests\seed_test_vault.py` はサンプル入りの保管庫（マスターパスワード `test1234`）を作る。

## データの保存場所

- 保管庫: `%LOCALAPPDATA%\KagiBox\vault.kagibox`（ファイル形式は互換を最優先。変えるときは旧形式の読み込みを残す）
- 設定（秘密情報なし）: `%LOCALAPPDATA%\KagiBox\settings.json`
- エラーログ: `%LOCALAPPDATA%\KagiBox\logs\error.log`（UI と core の共通。5MB でローテート）
- 環境変数 `KAGIBOX_DATA_DIR` で別のフォルダにできる

## 環境変数（.env）

`.env` は exe の横、プロジェクト直下、`%LOCALAPPDATA%\KagiBox\` の順に読む。既存の環境変数が優先。空の値は「未設定」扱い。

| 変数 | 内容 | 既定値 |
|---|---|---|
| `GITHUB_REPO` | 更新を確認するリポジトリ（`owner/repo`） | `Irony-s9c/kagibox` |
| `GITHUB_TOKEN` | 非公開リポジトリの場合のみ（読み取り専用） | なし |
| `UPDATE_CHECK_ENABLED` | `false` で起動時の確認を無効化 | `true` |

## セキュリティ設計

- 各アカウントの全項目を、アカウントごとに 1 つの AES-256-GCM 暗号文として保存する。マスターキーは Argon2id（メモリ 64 MiB、時間コスト 3、並列 4）で導出し、ディスクには書かない。
- 画面が一覧で受け取るのはサービス名・ID などだけ。パスワードはコピー・編集画面の表示・自動入力のときに、その 1 件だけを復号する。コピーは core がクリップボードに入れ、30 秒後に消す。
- 無操作・Win+L・スリープで鍵をメモリから消す。UI が終了すると core も鍵とクリップボードを消して終わる。
- 外部通信はアップデート確認とダウンロードだけ。「ログインページを開く」は http / https だけを開く。
- データフォルダ単位の名前付き Mutex で、core の二重起動を防ぐ（2 つ同時に動くと保管庫を上書きし合うため）。UI も 1 つだけ起動する。
- テスト用の自動アンロック（`KAGIBOX_TEST_MASTER_PASSWORD`）と画面指定は `#if DEBUG` の中だけにあり、配布する Release ビルドには含まれない。

## インストーラーと SmartScreen

コード署名をしていないため、初回実行時に「Windows によって PC が保護されました」と表示される。「詳細情報」→「実行」で進める。
