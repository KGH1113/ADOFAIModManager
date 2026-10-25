import type { CatalogMod } from "@/models/catalog/catalog-model";
import type { InstalledMod } from "@/models/installation/installation-model";
import { recommendations } from "@/models/onboarding/onboarding-model";

function parseVersion(value: string) {
  const match = value
    .trim()
    .match(/^v?(\d+(?:\.\d+)*)(?:[-\s]?(alpha|beta|rc)[.\s]?(\d*))?(?:\+[\w.-]+)?$/i);
  if (!match) return null;
  return {
    parts: match[1].split(".").map(Number),
    stage: match[2] ? ({ alpha: 0, beta: 1, rc: 2 }[match[2].toLowerCase()] ?? 3) : 3,
    revision: Number(match[3] || 0),
  };
}
// null means the publisher's version labels cannot be ordered reliably.
export function compareVersions(left: string, right: string): number | null {
  const a = parseVersion(left),
    b = parseVersion(right);
  if (!a || !b) return null;
  for (let i = 0; i < Math.max(a.parts.length, b.parts.length); i++) {
    const difference = (a.parts[i] ?? 0) - (b.parts[i] ?? 0);
    if (difference) return Math.sign(difference);
  }
  return Math.sign(a.stage - b.stage || a.revision - b.revision);
}
const identity = (value: string) => value.toLowerCase().replace(/[^\p{L}\p{N}]/gu, "");
function projectIdentity(value?: string | null): string | null {
  try {
    const url = new URL(value ?? "");
    if (!["https:", "http:"].includes(url.protocol)) return null;
    const path = url.pathname.replace(/\/+$/, "");
    if (!path) return null;
    if (url.hostname.toLowerCase() === "github.com") {
      const parts = path.split("/").filter(Boolean);
      return parts.length >= 2
        ? `github.com/${parts[0]}/${parts[1].replace(/\.git$/i, "")}`.toLowerCase()
        : null;
    }
    return `${url.hostname.toLowerCase()}${path}`;
  } catch {
    return null;
  }
}
export function findCatalogMod(mod: InstalledMod, catalog: CatalogMod[]): CatalogMod | undefined {
  const alias = Object.values(recommendations).find(
    (item) => identity(item.installedId) === identity(mod.id),
  );
  if (alias) return catalog.find((item) => item.id === alias.slug);
  const home = projectIdentity(mod.homePage);
  if (home) {
    const matches = catalog.filter((item) => projectIdentity(item.projectUrl) === home);
    if (matches.length === 1) return matches[0];
  }
  for (const name of [mod.id, mod.name]) {
    if (!identity(name)) continue;
    const matches = catalog.filter((item) =>
      [item.id, item.name].some((value) => identity(value) === identity(name)),
    );
    if (matches.length === 1) return matches[0];
    if (matches.length > 1) return undefined;
  }
  return undefined;
}
export function installedCatalogMods(mods: InstalledMod[], catalog: CatalogMod[]) {
  const matches = new Map<string, InstalledMod>();
  for (const mod of mods) {
    if (!mod.installed) continue;
    const entry = findCatalogMod(mod, catalog);
    if (entry) matches.set(entry.id, mod);
  }
  return matches;
}
export function sameModVersion(left: string, right: string) {
  return (
    !!left.trim() &&
    !!right.trim() &&
    (compareVersions(left, right) === 0 || left.trim().toLowerCase() === right.trim().toLowerCase())
  );
}
export interface ModUpdate {
  state: "available" | "current" | "unknown" | "unlisted";
  latest?: CatalogMod;
}
export function checkModUpdate(mod: InstalledMod, catalog: CatalogMod[]): ModUpdate {
  const latest = findCatalogMod(mod, catalog);
  if (!latest) return { state: "unlisted" };
  const comparison = compareVersions(latest.version, mod.version);
  return {
    state: comparison === null ? "unknown" : comparison > 0 ? "available" : "current",
    latest,
  };
}
