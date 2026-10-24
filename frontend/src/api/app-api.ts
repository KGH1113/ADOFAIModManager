import type { CatalogApi } from "./catalog/catalog-api";
import type { InstallationApi } from "./installation/installation-api";
import type { LogsApi } from "./logs/logs-api";
import type { ModsApi } from "./mods/mods-api";
import type { UpdateApi } from "./updates/update-api";
export interface DesktopApi {
  runtime(): Promise<{ platform: string; version: string }>;
  chooseGame(platform: string): Promise<string | null>;
  chooseArchive(): Promise<string | null>;
  openUrl(url: string): Promise<void>;
  reveal(path: string): Promise<void>;
  onFiles(handler: (paths: string[]) => void): Promise<() => void>;
}
export interface AppApi {
  updates: UpdateApi;
  installation: InstallationApi;
  mods: ModsApi;
  catalog: CatalogApi;
  logs: LogsApi;
  desktop: DesktopApi;
}
