import type { CatalogDetailDto, CatalogModDto } from "@/schemas/catalog/catalog-schema";
export type CatalogMod = ReturnType<typeof mapCatalogMod>;
export type CatalogModDetail = ReturnType<typeof mapCatalogDetail>;
export type CatalogVersion = CatalogModDetail["versions"][number];
export type CatalogTag = CatalogMod["tags"][number];
export type CatalogSort = "newest" | "oldest" | "downloads" | "likes" | "name";
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
    author:
      dto.assignees
        ?.map((person) => person.name)
        .filter(Boolean)
        .join(", ") ||
      dto.creatorUsername ||
      "",
    uploadedAt: Date.parse(dto.sourceUploadedAt ?? "") || 0,
    hidden: false,
    url,
    action: "download" as const,
    imageUrl: secureUrl(dto.imageUrl),
    projectUrl: secureUrl(dto.projectUrl),
    deprecatedAfter: dto.deprecatedAfter ?? null,
    pinned: dto.isPinned ?? false,
    likes: dto.likes ?? 0,
    downloadCount: dto.downloadCount ?? 0,
    tags: (dto.tags ?? []).map((tag) => ({
      id: tag.id,
      name: tag.name,
      color: /^#[\da-f]{6}$/i.test(tag.color ?? "") ? (tag.color ?? null) : null,
    })),
  };
}
export function mapCatalogDetail(dto: CatalogDetailDto) {
  const mod = mapCatalogMod(dto);
  const releases = [...(dto.versions ?? [])];
  if (
    dto.latestVersion &&
    !releases.some((release) => release.version === dto.latestVersion?.version)
  ) {
    releases.push(dto.latestVersion);
  }
  if (!releases.length && dto.version) {
    releases.push({
      id: dto.id,
      version: dto.version,
      releasedAt: dto.sourceUploadedAt,
      downloadUrl: dto.downloadUrl,
    });
  }
  const versions = releases
    .map((release) => ({
      id: String(release.id),
      version: release.version,
      notes: release.notes ?? "",
      releasedAt: Date.parse(release.releasedAt ?? "") || 0,
      url: tufDownloadUrl(dto.slug, release.version),
      source: release.source ?? "",
      projectUrl: secureUrl(release.githubUrl) ?? secureUrl(release.downloadUrl) ?? mod.projectUrl,
    }))
    .sort((a, b) => b.releasedAt - a.releasedAt);
  return {
    ...mod,
    latestVersion: dto.latestVersion?.version ?? dto.version ?? versions[0]?.version ?? "",
    versions,
  };
}
export function filterCatalog(
  mods: CatalogMod[],
  query: string,
  sort: CatalogSort = "newest",
  tagId: number | null = null,
) {
  const term = query.trim().toLocaleLowerCase();
  return mods
    .filter(
      (mod) =>
        !mod.hidden &&
        (tagId === null || mod.tags.some((tag) => tag.id === tagId)) &&
        (!term ||
          [mod.name, mod.author, mod.description].some((text) =>
            text.toLocaleLowerCase().includes(term),
          )),
    )
    .sort((a, b) => {
      if (a.pinned !== b.pinned) return Number(b.pinned) - Number(a.pinned);
      if (sort === "name") return a.name.localeCompare(b.name);
      if (sort === "oldest") return a.uploadedAt - b.uploadedAt;
      if (sort === "downloads")
        return b.downloadCount - a.downloadCount || b.uploadedAt - a.uploadedAt;
      if (sort === "likes") return b.likes - a.likes || b.uploadedAt - a.uploadedAt;
      return b.uploadedAt - a.uploadedAt;
    });
}
