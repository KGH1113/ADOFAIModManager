fn main() {
    // Re-stage the sidecar when prepare:desktop publishes a new engine.
    // Otherwise Cargo can reuse the old copy next to the desktop executable.
    println!("cargo:rerun-if-changed=binaries");
    tauri_build::build()
}
