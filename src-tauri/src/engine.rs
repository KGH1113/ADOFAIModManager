use serde::Deserialize;
use serde_json::Value;
use std::{path::PathBuf, process::Stdio, time::Duration};
use tauri::Manager;
use tokio::{io::AsyncWriteExt, process::Command};

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct EngineRequest {
    pub action: Action,
    pub game_path: Option<String>,
    pub zip_path: Option<String>,
    pub download_token: Option<String>,
    pub path: Option<String>,
    pub mod_id: Option<String>,
    pub enabled: Option<bool>,
    #[serde(default)]
    pub force_payload: bool,
    pub preset_kind: Option<String>,
    pub preset_values: Option<[bool; 3]>,
    pub key_bindings: Option<Vec<i32>>,
    pub key_viewer_style: Option<u8>,
}

#[derive(Debug, Deserialize, serde::Serialize)]
#[serde(rename_all = "lowercase")]
pub enum Action {
    Status,
    Mods,
    Install,
    Remove,
    Restore,
    Inspectmod,
    Installmod,
    Setmodenabled,
    Removemod,
    Logtail,
    Gamelog,
    Preset,
    Savepreset,
    Onboardingsetup,
    Jrpkeys,
    Savejrpkeys,
}

pub async fn run(
    app: &tauri::AppHandle,
    request: EngineRequest,
    downloaded: Option<PathBuf>,
) -> Result<Value, String> {
    let executable = std::env::current_exe()
        .map_err(|e| e.to_string())?
        .parent()
        .ok_or("Application directory is unavailable")?
        .join(if cfg!(windows) {
            "UMMInstallerEngine.exe"
        } else {
            "UMMInstallerEngine"
        });
    if !executable.is_file() {
        return Err(
            "The installation engine is missing. Rebuild with bun run prepare:desktop.".into(),
        );
    }
    let resources = app.path().resource_dir().map_err(|e| e.to_string())?;
    let mut payload = resources.join("UMMPayload");
    if cfg!(debug_assertions) && !payload.is_dir() {
        payload = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("resources/UMMPayload");
    }
    let body = serde_json::json!({
        "protocolVersion": 1, "action": request.action,
        "gamePath": request.game_path, "payloadDir": payload,
        "zipPath": downloaded.map(|p| p.to_string_lossy().into_owned()).or(request.zip_path),
        "path": request.path, "modId": request.mod_id, "enabled": request.enabled,
        "forcePayload": request.force_payload, "presetKind": request.preset_kind,
        "presetValues": request.preset_values,
        "keyBindings": request.key_bindings, "keyViewerStyle": request.key_viewer_style,
    });
    let mut command = Command::new(executable);
    command
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .kill_on_drop(true);
    #[cfg(windows)]
    command.creation_flags(0x08000000);
    let mut child = command
        .spawn()
        .map_err(|e| format!("Cannot start installation engine: {e}"))?;
    let mut input = child.stdin.take().ok_or("Engine input is unavailable")?;
    input
        .write_all(body.to_string().as_bytes())
        .await
        .map_err(|e| e.to_string())?;
    drop(input);
    let output = tokio::time::timeout(Duration::from_secs(180), child.wait_with_output())
        .await
        .map_err(|_| {
            "The installation engine timed out. Refresh the game status before trying again."
        })?
        .map_err(|e| e.to_string())?;
    serde_json::from_slice(&output.stdout).map_err(|_| {
        format!(
            "The installation engine returned an invalid response. {}",
            String::from_utf8_lossy(&output.stderr)
        )
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn accepts_each_key_layout_with_its_style() {
        for style in [10, 12, 16, 20] {
            let request: EngineRequest = serde_json::from_value(serde_json::json!({
                "action": "savejrpkeys", "keyViewerStyle": style,
                "keyBindings": vec![97; style as usize],
            }))
            .unwrap();
            assert_eq!(request.key_viewer_style, Some(style));
            assert_eq!(request.key_bindings.unwrap().len(), style as usize);
        }
    }

    #[test]
    fn denies_unknown_actions_and_payload_overrides() {
        assert!(serde_json::from_str::<EngineRequest>(r#"{"action":"shell"}"#).is_err());
        assert!(serde_json::from_str::<EngineRequest>(
            r#"{"action":"install","payloadDir":"/tmp/evil"}"#
        )
        .is_err());
        assert!(serde_json::from_str::<EngineRequest>(r#"{"action":"status"}"#).is_ok());
    }
}
