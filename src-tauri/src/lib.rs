mod downloads;
mod engine;
mod window_sizing;

use downloads::Downloads;
use engine::EngineRequest;
use std::sync::Mutex;
use tauri::{Emitter, Manager, State};
use tauri_plugin_opener::OpenerExt;
use tokio_util::sync::CancellationToken;

#[derive(Default)]
struct Runtime {
    engine: tokio::sync::Mutex<()>,
    downloads: Downloads,
    imports: Mutex<Vec<String>>,
}

#[tauri::command]
async fn engine_request(
    app: tauri::AppHandle,
    state: State<'_, Runtime>,
    request: EngineRequest,
) -> Result<serde_json::Value, String> {
    let _guard = state.engine.lock().await;
    // Hold the owned archive until the engine finishes, even if the UI asks to discard it.
    let files = state.downloads.files.lock().await;
    let downloaded = match &request.download_token {
        Some(token) => Some(
            files
                .get(token)
                .ok_or("This download has expired. Download the mod again.")?
                .to_path_buf(),
        ),
        None => None,
    };
    engine::run(&app, request, downloaded).await
}

#[tauri::command]
async fn catalog_fetch() -> Result<serde_json::Value, String> {
    downloads::catalog().await
}

#[tauri::command]
async fn catalog_details(slug: String) -> Result<serde_json::Value, String> {
    downloads::catalog_details(&slug).await
}

#[tauri::command]
async fn download_mod(
    state: State<'_, Runtime>,
    url: String,
    request_id: String,
) -> Result<String, String> {
    let cancellation = CancellationToken::new();
    {
        let mut pending = state.downloads.pending.lock().await;
        if pending.contains_key(&request_id) {
            return Err("A download with this identifier is already running.".into());
        }
        pending.insert(request_id.clone(), cancellation.clone());
    }
    let result = tokio::select! {
        _ = cancellation.cancelled() => Err("cancelled".to_string()),
        result = downloads::download(&url) => result,
    };
    state.downloads.pending.lock().await.remove(&request_id);
    let path = result?;
    let token = uuid::Uuid::new_v4().to_string();
    state
        .downloads
        .files
        .lock()
        .await
        .insert(token.clone(), path);
    Ok(token)
}

#[tauri::command]
async fn cancel_download(state: State<'_, Runtime>, request_id: String) -> Result<(), String> {
    if let Some(cancellation) = state.downloads.pending.lock().await.get(&request_id) {
        cancellation.cancel();
    }
    Ok(())
}

#[tauri::command]
async fn discard_download(state: State<'_, Runtime>, token: String) -> Result<(), String> {
    state.downloads.files.lock().await.remove(&token);
    Ok(())
}

#[tauri::command]
fn open_external(app: tauri::AppHandle, url: String) -> Result<(), String> {
    let url = downloads::https_url(&url)?;
    app.opener()
        .open_url(url.as_str(), None::<&str>)
        .map_err(|e| e.to_string())
}

#[tauri::command]
fn reveal_path(app: tauri::AppHandle, path: String) -> Result<(), String> {
    if !std::path::PathBuf::from(&path).exists() {
        return Err("The requested file no longer exists.".into());
    }
    app.opener()
        .reveal_item_in_dir(path)
        .map_err(|e| e.to_string())
}

#[tauri::command]
fn runtime_info() -> serde_json::Value {
    serde_json::json!({ "platform": std::env::consts::OS, "version": env!("CARGO_PKG_VERSION") })
}

#[tauri::command]
fn take_open_files(state: State<'_, Runtime>) -> Vec<String> {
    std::mem::take(&mut *state.imports.lock().unwrap())
}

fn queue_imports(app: &tauri::AppHandle, paths: impl IntoIterator<Item = String>) {
    let files: Vec<_> = paths
        .into_iter()
        .filter(|p| std::path::PathBuf::from(p).is_file() && p.to_lowercase().ends_with(".zip"))
        .collect();
    if files.is_empty() {
        return;
    }
    app.state::<Runtime>().imports.lock().unwrap().extend(files);
    let _ = app.emit("open-mod-files", ());
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.unminimize();
        let _ = window.set_focus();
    }
}

pub fn run() {
    let app = tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, args, _cwd| {
            queue_imports(app, args.into_iter().skip(1));
            if let Some(window) = app.get_webview_window("main") {
                let _ = window.unminimize();
                let _ = window.set_focus();
            }
        }))
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_opener::init())
        .manage(Runtime::default())
        .invoke_handler(tauri::generate_handler![
            engine_request,
            catalog_fetch,
            catalog_details,
            download_mod,
            cancel_download,
            discard_download,
            open_external,
            reveal_path,
            runtime_info,
            take_open_files
        ])
        .setup(|app| {
            if let Some(window) = app.get_webview_window("main") {
                window_sizing::initialize(&window)?;
            }
            queue_imports(app.handle(), std::env::args().skip(1));
            Ok(())
        })
        .build(tauri::generate_context!())
        .expect("Unable to start ADOFAI Mod Manager");
    app.run(|_app, _event| {
        #[cfg(target_os = "macos")]
        if let tauri::RunEvent::Opened { urls } = _event {
            queue_imports(
                _app,
                urls.iter()
                    .filter_map(|url| url.to_file_path().ok())
                    .map(|path| path.to_string_lossy().into_owned()),
            );
        }
    });
}
