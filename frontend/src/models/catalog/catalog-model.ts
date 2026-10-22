import type { CatalogModDto } from "@/schemas/catalog/catalog-schema";
export type CatalogMod = ReturnType<typeof mapCatalogMod>;
export function tufDownloadUrl(slug: string, version?: string): string {
  const path = [slug, ...(version ? [version] : []), "download"].map(encodeURIComponent).join("/");
  return `https://api.tuforums.com/v2/mods/${path}`;
}
export function secureUrl(value?: string | null): string | null {
  try {
    const url = new URL(value ?? "");
    return url.protocol === "https:" && !url.username && !url.password ? url.href : null;
  } catch {
    return null;
  }
}
export function mapCatalogMod(dto: CatalogModDto) {
  const url = tufDownloadUrl(dto.slug, dto.version ?? undefined);
  return {
    id: dto.slug,
    name: dto.name,
    version: dto.version ?? "",
    description: dto.description ?? "",
    author: dto.creatorUsername ?? "",
    uploadedAt: Date.parse(dto.sourceUploadedAt ?? "") || 0,
    hidden: false,
    url,
    action: "download" as const,
  };
}
export function filterCatalog(mods: CatalogMod[], query: string) {
  const term = query.trim().toLocaleLowerCase();
  return mods
    .filter(
      (mod) =>
        !mod.hidden &&
        (!term ||
          [mod.name, mod.author, mod.description].some((text) =>
            text.toLocaleLowerCase().includes(term),
          )),
    )
    .sort((a, b) => b.uploadedAt - a.uploadedAt);
}
