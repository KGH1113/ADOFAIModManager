import type { EngineGateway } from "@/api/engine/engine-gateway";
import { mapInstallation } from "@/models/installation/installation-model";
import type { InstallationApi } from "./installation-api";
export function createInstallationApi(engine: EngineGateway): InstallationApi {
  return {
    load: async (gamePath) => mapInstallation(await engine("status", gamePath)),
    execute: async (gamePath, action) =>
      mapInstallation(
        await engine(action === "repair" ? "install" : action, gamePath, {
          forcePayload: action === "repair",
        }),
      ),
  };
}
