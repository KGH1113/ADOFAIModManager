export type PresetKind = "tweaks" | "keyViewer";
export type PresetValues = [boolean, boolean, boolean];
export type ArchiveSource = { path: string; token?: never } | { token: string; path?: never };
export interface ModInspection {
  id: string;
  name: string;
  version: string;
  alreadyInstalled: boolean;
}
export interface PendingImport {
  source: ArchiveSource;
  inspection: ModInspection;
  gamePath: string;
}
export type LogSource = "game" | "umm";
export interface GameLog {
  exists: boolean;
  text: string;
  path: string | null;
}
