import { z } from "zod";
import type { DesktopClient } from "@/shared/clients/desktop-client";
import type { AppApi } from "./app-api";
import { createCatalogApi } from "./catalog/create-catalog-api";
import { createEngineGateway } from "./engine/engine-gateway";
import { createInstallationApi } from "./installation/create-installation-api";
import { createModsApi } from "./mods/create-mods-api";
export function createProductionApi(client: DesktopClient): AppApi {
  const engine = createEngineGateway(client);
  return {
    updates: {
      check: async () =>
        z
          .object({ version: z.string(), notes: z.string() })
          .nullable()
          .parse(await client.invoke("check_app_update")),
      download: async () => {
        await client.invoke("download_app_update");
      },
      install: async () => {
        await client.invoke("install_app_update");
      },
    },
    installation: createInstallationApi(engine),
    mods: createModsApi(engine),
    catalog: createCatalogApi(client),
    logs: {
      async load(gamePath, source) {
        const result = await engine(source === "game" ? "gamelog" : "logtail", gamePath);
        return {
          exists: result.logExists ?? false,
          text: result.logText ?? "",
          path: result.logPath ?? null,
        };
      },
    },
    desktop: {
      runtime: async () =>
        z
          .object({ platform: z.string(), version: z.string() })
          .parse(await client.invoke("runtime_info")),
      chooseGame: client.chooseGame,
      chooseArchive: client.chooseArchive,
      onFiles: client.onFiles,
      openUrl: async (url) => {
        await client.invoke("open_external", { url });
      },
      reveal: async (path) => {
        await client.invoke("reveal_path", { path });
      },
    },
  };
}
