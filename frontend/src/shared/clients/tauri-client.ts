import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { getCurrentWebview } from "@tauri-apps/api/webview";
import { open } from "@tauri-apps/plugin-dialog";
import type { DesktopClient } from "./desktop-client";

export function createTauriClient(): DesktopClient {
  return {
    invoke,
    async chooseGame(platform) {
      return open(
        platform === "macos"
          ? {
              multiple: false,
              directory: false,
              filters: [{ name: "ADOFAI", extensions: ["app"] }],
            }
          : { multiple: false, directory: true },
      );
    },
    chooseArchive: () =>
      open({ multiple: false, filters: [{ name: "UMM Mod Archive", extensions: ["zip"] }] }),
    async onFiles(handler) {
      let stopped = false;
      const drain = async () => {
        const files = await invoke<string[]>("take_open_files");
        if (!stopped && files.length) handler(files);
      };
      const unlisten = await listen("open-mod-files", () => {
        void drain();
      });
      const unlistenDrop = await getCurrentWebview().onDragDropEvent((event) => {
        if (!stopped && event.payload.type === "drop")
          handler(event.payload.paths.filter((path) => /\.zip$/i.test(path)));
      });
      await drain();
      return () => {
        stopped = true;
        unlisten();
        unlistenDrop();
      };
    },
  };
}
