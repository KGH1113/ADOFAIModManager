use serde::Serialize;
use std::time::Duration;
use tauri::{AppHandle, State};
use tauri_plugin_updater::{Update, UpdaterExt};
use tokio::sync::Mutex;

#[derive(Default)]
pub struct Updates(Mutex<Option<PendingUpdate>>);

struct PendingUpdate {
    update: Update,
    bytes: Option<Vec<u8>>,
}

#[derive(Serialize)]
pub struct UpdateInfo {
    version: String,
    notes: String,
}

#[tauri::command]
pub async fn check_app_update(
    app: AppHandle,
    state: State<'_, Updates>,
) -> Result<Option<UpdateInfo>, String> {
    // Never replace a running development build with a published application.
    if cfg!(debug_assertions) {
        return Ok(None);
    }
    let mut pending = state
        .0
        .try_lock()
        .map_err(|_| "Update already in progress")?;
    if pending.is_none() {
        let updater = app
            .updater_builder()
            .timeout(Duration::from_secs(10))
            .build()
            .map_err(|e| e.to_string())?;
        if let Some(mut update) = updater.check().await.map_err(|e| e.to_string())? {
            update.timeout = Some(Duration::from_secs(300));
            *pending = Some(PendingUpdate {
                update,
                bytes: None,
            });
        }
    }
    Ok(pending.as_ref().map(|p| UpdateInfo {
        version: p.update.version.clone(),
        notes: p.update.body.clone().unwrap_or_default(),
    }))
}

#[tauri::command]
pub async fn download_app_update(state: State<'_, Updates>) -> Result<(), String> {
    let mut pending = state
        .0
        .try_lock()
        .map_err(|_| "Update already in progress")?;
    let update = pending.as_mut().ok_or("No update available")?;
    if update.bytes.is_none() {
        // The plugin verifies the signature before returning these bytes.
        match update.update.download(|_, _| {}, || {}).await {
            Ok(bytes) => update.bytes = Some(bytes),
            Err(error) => {
                // A withdrawn or repaired release must be discoverable on retry.
                *pending = None;
                return Err(error.to_string());
            }
        }
    }
    Ok(())
}

#[tauri::command]
pub async fn install_app_update(
    app: AppHandle,
    state: State<'_, Updates>,
    runtime: State<'_, crate::Runtime>,
) -> Result<(), String> {
    // The native engine lock is also held through replacement/restart so an IPC
    // request cannot race installation even if the UI has not rendered its lock.
    let _engine = runtime
        .engine
        .try_lock()
        .map_err(|_| "A mod operation is running")?;
    let downloads = runtime.downloads.pending.lock().await;
    if !downloads.is_empty() {
        return Err("A mod download is running".into());
    }
    let pending = state
        .0
        .try_lock()
        .map_err(|_| "Update already in progress")?;
    let pending = pending.as_ref().ok_or("No update available")?;
    let bytes = pending.bytes.as_ref().ok_or("Update is not downloaded")?;
    pending.update.install(bytes).map_err(|e| e.to_string())?;
    app.restart();
}
