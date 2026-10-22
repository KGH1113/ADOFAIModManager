use std::{collections::HashMap, time::Duration};
use tempfile::{Builder, TempPath};
use tokio::{io::AsyncWriteExt, sync::Mutex};
use tokio_util::sync::CancellationToken;

pub const CATALOG_URL: &str = "https://api.tuforums.com/v2/mods";
const MAX_ZIP: u64 = 512 * 1024 * 1024;

#[derive(Default)]
pub struct Downloads {
    pub files: Mutex<HashMap<String, TempPath>>,
    pub pending: Mutex<HashMap<String, CancellationToken>>,
}

pub fn https_url(raw: &str) -> Result<reqwest::Url, String> {
    let url = reqwest::Url::parse(raw).map_err(|_| "Invalid download link")?;
    if url.scheme() != "https"
        || url.host_str().is_none()
        || !url.username().is_empty()
        || url.password().is_some()
    {
        return Err("Only HTTPS links without credentials are supported.".into());
    }
    Ok(url)
}

fn platform_download_url(raw: &str, platform: &str) -> Result<reqwest::Url, String> {
    let mut url = https_url(raw)?;
    let parts: Vec<_> = url
        .path()
        .split('/')
        .filter(|part| !part.is_empty())
        .collect();
    let is_tuf_download = url.host_str() == Some("api.tuforums.com")
        && url.port().is_none()
        && matches!(parts.len(), 4 | 5)
        && parts[0] == "v2"
        && parts[1] == "mods"
        && parts.last() == Some(&"download");
    if is_tuf_download {
        if !matches!(platform, "windows" | "macos" | "linux") {
            return Err("TUF mod downloads require Windows, macOS, or Linux.".into());
        }
        // Always target this machine, including links with a stale platform query.
        let pairs: Vec<(String, String)> = url
            .query_pairs()
            .filter(|(key, _)| key != "platform")
            .map(|(key, value)| (key.into_owned(), value.into_owned()))
            .collect();
        url.set_query(None);
        url.query_pairs_mut()
            .extend_pairs(pairs)
            .append_pair("platform", platform);
    }
    Ok(url)
}

fn client() -> Result<reqwest::Client, String> {
    reqwest::Client::builder()
        .https_only(true)
        .timeout(Duration::from_secs(120))
        .user_agent("ADOFAIModManager/0.2")
        .redirect(reqwest::redirect::Policy::limited(5))
        .build()
        .map_err(|e| e.to_string())
}

#[derive(serde::Deserialize)]
struct CatalogPage {
    mods: Vec<serde_json::Value>,
    offset: usize,
    #[serde(rename = "hasMore")]
    has_more: bool,
}

fn parse_catalog_page(data: &[u8], expected_offset: usize) -> Result<CatalogPage, String> {
    let page: CatalogPage =
        serde_json::from_slice(data).map_err(|_| "Catalog response is invalid.")?;
    if page.offset != expected_offset || (page.has_more && page.mods.is_empty()) {
        return Err("Catalog pagination did not advance.".into());
    }
    Ok(page)
}

pub async fn catalog() -> Result<serde_json::Value, String> {
    let client = client()?;
    let mut mods = Vec::new();
    let mut offset = 0usize;
    let mut total_bytes = 0usize;
    loop {
        let mut response = client
            .get(CATALOG_URL)
            .timeout(Duration::from_secs(15))
            .query(&[("limit", 100), ("offset", offset)])
            .send()
            .await
            .map_err(|e| e.to_string())?
            .error_for_status()
            .map_err(|e| e.to_string())?;
        let mut data = Vec::new();
        while let Some(chunk) = response.chunk().await.map_err(|e| e.to_string())? {
            total_bytes += chunk.len();
            if total_bytes > 8 * 1024 * 1024 {
                return Err("Catalog response is too large.".into());
            }
            data.extend_from_slice(&chunk);
        }
        let page = parse_catalog_page(&data, offset)?;
        offset += page.mods.len();
        mods.extend(page.mods);
        if !page.has_more {
            break;
        }
    }
    Ok(serde_json::Value::Array(mods))
}

pub async fn download(url: &str) -> Result<TempPath, String> {
    let mut response = client()?
        .get(platform_download_url(url, std::env::consts::OS)?)
        .send()
        .await
        .map_err(|e| e.to_string())?
        .error_for_status()
        .map_err(|e| e.to_string())?;
    if response.content_length().unwrap_or(0) > MAX_ZIP {
        return Err("The mod archive is larger than 512 MB.".into());
    }
    // The engine's archive boundary requires .zip, including for owned downloads.
    let temp = Builder::new()
        .prefix("adofai-mod-")
        .suffix(".zip")
        .tempfile()
        .map_err(|e| e.to_string())?;
    let (file, path) = temp.into_parts();
    let mut file = tokio::fs::File::from_std(file);
    let mut total = 0;
    let mut signature = Vec::with_capacity(4);
    while let Some(chunk) = response.chunk().await.map_err(|e| e.to_string())? {
        total += chunk.len() as u64;
        if total > MAX_ZIP {
            return Err("The mod archive is larger than 512 MB.".into());
        }
        if signature.len() < 4 {
            signature.extend(chunk.iter().take(4 - signature.len()));
        }
        if signature.len() == 4 && !is_zip(&signature) {
            return Err("This link did not return a ZIP archive. Open the website instead.".into());
        }
        file.write_all(&chunk).await.map_err(|e| e.to_string())?;
    }
    if !is_zip(&signature) {
        return Err("The downloaded file is not a ZIP archive.".into());
    }
    file.flush().await.map_err(|e| e.to_string())?;
    drop(file);
    Ok(path)
}

fn is_zip(bytes: &[u8]) -> bool {
    matches!(
        bytes,
        [0x50, 0x4b, 3, 4] | [0x50, 0x4b, 5, 6] | [0x50, 0x4b, 7, 8]
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn tuf_downloads_target_the_current_platform_and_preserve_other_query_values() {
        for platform in ["windows", "macos", "linux"] {
            for path in ["mod/download", "mod/1.0%20beta/download"] {
                let url = platform_download_url(
                    &format!("{CATALOG_URL}/{path}?platform=windows&other=a%20b&platform=linux"),
                    platform,
                )
                .unwrap();
                let pairs: Vec<_> = url.query_pairs().collect();
                assert_eq!(pairs.iter().filter(|(key, _)| key == "platform").count(), 1);
                assert!(pairs
                    .iter()
                    .any(|(key, value)| key == "platform" && value == platform));
                assert!(pairs
                    .iter()
                    .any(|(key, value)| key == "other" && value == "a b"));
                assert!(url.path().ends_with(path));
            }
        }
        assert!(platform_download_url(&format!("{CATALOG_URL}/mod/download"), "android").is_err());
        assert!(
            platform_download_url("http://api.tuforums.com/v2/mods/mod/download", "macos").is_err()
        );
    }

    #[test]
    fn platform_queries_do_not_change_direct_assets_or_unrelated_endpoints() {
        for raw in [
            "https://github.com/org/repo/releases/download/v1/mod.zip?token=x",
            "https://example.com/v2/mods/mod/download",
            "https://api.tuforums.com/v2/mods",
            "https://api.tuforums.com/v2/mods/mod",
            "https://api.tuforums.com:8443/v2/mods/mod/download",
        ] {
            assert_eq!(
                platform_download_url(raw, "macos").unwrap(),
                https_url(raw).unwrap()
            );
        }
    }
    #[test]
    fn parses_tuf_pages_and_rejects_stalled_or_malformed_pagination() {
        let first = parse_catalog_page(
            br#"{"mods":[{"id":1,"slug":"a"}],"offset":0,"hasMore":true}"#,
            0,
        )
        .unwrap();
        let last = parse_catalog_page(
            br#"{"mods":[{"id":2,"slug":"b"}],"offset":1,"hasMore":false}"#,
            first.mods.len(),
        )
        .unwrap();
        assert!(first.has_more);
        assert!(!last.has_more);
        let mods: Vec<_> = first.mods.into_iter().chain(last.mods).collect();
        assert_eq!(mods.len(), 2);
        assert_eq!(mods[1]["slug"], "b");
        assert!(parse_catalog_page(br#"{"mods":[],"offset":0,"hasMore":true}"#, 0).is_err());
        assert!(parse_catalog_page(br#"{"mods":[],"offset":0,"hasMore":false}"#, 100).is_err());
        assert!(parse_catalog_page(br#"{"mods":[],"offset":0}"#, 0).is_err());
    }

    #[tokio::test]
    #[ignore = "requires the live public TUF API"]
    async fn live_tuf_catalog_contains_recommendations() {
        let data = catalog().await.unwrap();
        let mods = data.as_array().unwrap();
        for slug in ["jalib", "jrp", "pacl2", "youtubestream"] {
            assert!(mods.iter().any(|m| m["slug"] == slug));
        }
        let slugs: std::collections::HashSet<_> =
            mods.iter().map(|m| m["slug"].as_str().unwrap()).collect();
        assert_eq!(mods.len(), slugs.len());
        println!("Fetched {} unique TUF mods across all pages", mods.len());
    }

    #[tokio::test]
    #[ignore = "downloads a mod from the live public TUF API"]
    async fn live_tuf_download_returns_zip() {
        let archive = download("https://api.tuforums.com/v2/mods/youtubestream/download")
            .await
            .unwrap();
        let data = tokio::fs::read(&archive).await.unwrap();
        assert!(data.len() > 4 && is_zip(&data[..4]));
        println!("Downloaded {} bytes through the TUF endpoint", data.len());
    }

    #[cfg(target_os = "macos")]
    #[tokio::test]
    #[ignore = "downloads official ZIPs and requires the published macOS engine"]
    async fn official_downloads_pass_native_engine_inspection() {
        use std::process::Stdio;
        use tokio::process::Command;

        let repository = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .parent()
            .unwrap();
        let engine = repository.join("build/tauri-engine-osx-arm64/UMMInstallerEngine");
        let fixture = tempfile::tempdir().unwrap();
        let game = fixture.path().join("ADanceOfFireAndIce.app");
        let managed = game.join("Contents/Resources/Data/Managed");
        std::fs::create_dir_all(&managed).unwrap();
        for name in ["UnityEngine.CoreModule.dll", "Assembly-CSharp.dll"] {
            std::fs::copy(
                repository.join("Resources/UMMPayload/UnityModManager.dll"),
                managed.join(name),
            )
            .unwrap();
        }
        for (url, identifier) in [
            (
                "https://github.com/Jongye0l/JALib/releases/download/v1.0.0.45/JALib.zip",
                "JALib",
            ),
            (
                "https://github.com/Jongye0l/JipperResourcePack/releases/download/v1.5.2.0/JipperResourcePack.zip",
                "JipperResourcePack",
            ),
        ] {
            let archive = download(url).await.unwrap();
            let request = serde_json::json!({
                "action": "inspectmod", "gamePath": game, "zipPath": archive.to_path_buf(),
            });
            let mut child = Command::new(&engine)
                .env("ADOFAI_MOD_MANAGER_DATA", fixture.path().join("app-data"))
                .stdin(Stdio::piped())
                .stdout(Stdio::piped())
                .stderr(Stdio::piped())
                .spawn()
                .unwrap();
            let mut input = child.stdin.take().unwrap();
            input.write_all(request.to_string().as_bytes()).await.unwrap();
            drop(input);
            let output = child.wait_with_output().await.unwrap();
            let response: serde_json::Value = serde_json::from_slice(&output.stdout).unwrap();
            assert!(output.status.success(), "{response}");
            assert_eq!(response["modInspection"]["id"], identifier, "{response}");
            assert!(response["ok"].as_bool().unwrap());
            println!("Official {identifier} download passed native ZIP inspection");
        }
        assert!(!fixture.path().join("Mods").exists());
    }

    #[test]
    fn downloads_require_https_and_real_zip_signatures() {
        assert!(https_url("http://example.com/mod.zip").is_err());
        assert!(https_url("https://user:pass@example.com/mod.zip").is_err());
        assert!(https_url("https://example.com/mod.zip").is_ok());
        assert!(is_zip(&[0x50, 0x4b, 3, 4]));
        assert!(!is_zip(b"<htm"));
    }
}
