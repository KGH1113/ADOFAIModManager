import type { EngineResponseDto, InstalledModDto } from "@/schemas/engine/engine-schema";
export type InstalledMod = InstalledModDto;
export type Installation = ReturnType<typeof mapInstallation>;
export type InstallationAction = "install" | "repair" | "remove" | "restore";
const injectionMethods = ["none", "assembly", "doorstop", "both"] as const;
export type InjectionMethod = (typeof injectionMethods)[number];
function readInjection(value: string | null | undefined, hookInstalled: boolean | undefined) {
  return value && (injectionMethods as readonly string[]).includes(value)
    ? (value as InjectionMethod)
    : hookInstalled
      ? "assembly"
      : "none";
}
export function mapInstallation(dto: EngineResponseDto) {
  const injection = readInjection(dto.status?.injectionMethod, dto.status?.hookInstalled);
  return {
    detected: dto.detected,
    gamePath: dto.status?.appPath ?? null,
    gameRoot: dto.status?.gameRoot ?? null,
    modsPath: dto.status?.modsPath ?? null,
    injection,
    installed: injection !== "none" && !!dto.status?.managerInstalled,
    version: dto.status?.managerVersion ?? null,
    bundledVersion: dto.bundledVersion ?? null,
    hasBackup: dto.status?.hasBackup ?? false,
    warning: dto.status?.warning ?? null,
    steamBranch: dto.status?.steamBranch ?? null,
    alphaInstalled: dto.status?.alphaInstalled ?? false,
    gameRunning: dto.gameRunning,
    mods: dto.mods.filter((mod) => mod.installed),
    log: dto.log,
  };
}
