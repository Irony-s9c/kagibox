#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod commands;
mod crypto;
mod generator;
mod vault;

fn main() {
    use std::sync::Mutex;
    use commands::AppState;

    tauri::Builder::default()
        .manage(Mutex::new(AppState::default()))
        .plugin(tauri_plugin_clipboard_manager::init())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_fs::init())
        .plugin(tauri_plugin_process::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .invoke_handler(tauri::generate_handler![
            commands::is_vault_created,
            commands::unlock,
            commands::lock,
            commands::is_locked,
            commands::list_entries,
            commands::reveal_password,
            commands::add_entry,
            commands::update_entry,
            commands::delete_entry,
            commands::save_vault,
            commands::generate,
            commands::export_txt,
            commands::copy_to_clipboard,
            commands::get_config,
            commands::set_config,
            commands::search_entries,
            commands::check_update,
            commands::do_update,
        ])
        .setup(|app| {
            commands::spawn_auto_lock_task(app.handle().clone());
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
