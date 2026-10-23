import { describe, expect, it } from "vitest";
import { catalogDetailSchema, catalogSchema } from "@/schemas/catalog/catalog-schema";
import { filterCatalog, mapCatalogDetail, mapCatalogMod, tufDownloadUrl } from "./catalog-model";

describe("TUF catalog boundary", () => {
  it("maps TUF fields and searches author and description", () => {
    const items = catalogSchema
      .parse([
        { id: 1, slug: "one", name: "First" },
        {
          id: 2,
          slug: "new",
          name: "New",
          sourceUploadedAt: "2026-09-30T00:00:00Z",
          creatorUsername: "Author",
          description: "Keyboard",
        },
      ])
      .map(mapCatalogMod);
    expect(filterCatalog(items, "").map((mod) => mod.id)).toEqual(["new", "one"]);
    expect(filterCatalog(items, " author ").map((mod) => mod.id)).toEqual(["new"]);
    expect(filterCatalog(items, "keyboard").map((mod) => mod.id)).toEqual(["new"]);
  });
  it("uses the TUF endpoint even when the registered URL is a website", () => {
    const mod = mapCatalogMod({
      id: 1,
      slug: "jrp",
      name: "JRP",
      version: "1.5.2",
      downloadUrl: "https://github.com/example/mod/releases",
    });
    expect(mod.action).toBe("download");
    expect(mod.url).toBe("https://api.tuforums.com/v2/mods/jrp/1.5.2/download");
    expect(tufDownloadUrl("my/mod", "1.0 Beta45")).toBe(
      "https://api.tuforums.com/v2/mods/my%2Fmod/1.0%20Beta45/download",
    );
    expect(tufDownloadUrl("pacl2")).toBe("https://api.tuforums.com/v2/mods/pacl2/download");
  });
  it("rejects missing TUF identity and malformed metadata", () => {
    expect(() => catalogSchema.parse([{ id: 1, name: "Missing slug" }])).toThrow();
    expect(() => catalogSchema.parse([{ id: "1", slug: "test", name: "Test" }])).toThrow();
  });
  it("maps public card metadata and removes unsafe links and tag colors", () => {
    const mod = mapCatalogMod({
      id: 1,
      slug: "test",
      name: "Test",
      creatorUsername: "username",
      assignees: [{ name: "Display name" }],
      isPinned: true,
      likes: 8,
      downloadCount: 578,
      imageUrl: "javascript:alert(1)",
      projectUrl: "https://user:password@example.com",
      tags: [
        { id: 1, name: "Gameplay", color: "#ff7070" },
        { id: 2, name: "Overlay", color: "url(https://example.com)" },
      ],
    });
    expect(mod).toMatchObject({
      author: "Display name",
      pinned: true,
      likes: 8,
      downloadCount: 578,
      imageUrl: null,
      projectUrl: null,
    });
    expect(mod.tags.map((tag) => tag.color)).toEqual(["#ff7070", null]);
  });
  it("combines text and tag filtering while keeping pinned entries first in every sort", () => {
    const items = [
      {
        id: 1,
        slug: "pinned",
        name: "Pinned",
        isPinned: true,
        likes: 1,
        downloadCount: 1,
        sourceUploadedAt: "2026-01-01",
        tags: [{ id: 2, name: "Overlay" }],
      },
      {
        id: 2,
        slug: "alpha",
        name: "Alpha",
        likes: 10,
        downloadCount: 8,
        sourceUploadedAt: "2026-01-03",
        tags: [{ id: 2, name: "Overlay" }],
      },
      {
        id: 3,
        slug: "beta",
        name: "Beta",
        likes: 8,
        downloadCount: 10,
        sourceUploadedAt: "2026-01-02",
        tags: [{ id: 3, name: "Editor" }],
      },
    ].map(mapCatalogMod);
    expect(filterCatalog(items, "", "downloads").map((mod) => mod.id)).toEqual([
      "pinned",
      "beta",
      "alpha",
    ]);
    expect(filterCatalog(items, "", "likes").map((mod) => mod.id)).toEqual([
      "pinned",
      "alpha",
      "beta",
    ]);
    expect(filterCatalog(items, "", "oldest").map((mod) => mod.id)).toEqual([
      "pinned",
      "beta",
      "alpha",
    ]);
    expect(filterCatalog(items, "", "name").map((mod) => mod.id)).toEqual([
      "pinned",
      "alpha",
      "beta",
    ]);
    expect(filterCatalog(items, "alpha", "newest", 2).map((mod) => mod.id)).toEqual(["alpha"]);
    expect(filterCatalog(items, "alpha", "newest", 3)).toEqual([]);
    expect(items.map((mod) => mod.id)).toEqual(["pinned", "alpha", "beta"]);
  });
  it("keeps each historical release identity, notes and pinned TUF download URL", () => {
    const detail = mapCatalogDetail(
      catalogDetailSchema.parse({
        mod: {
          id: 1,
          slug: "mod",
          name: "Mod",
          version: "2.0",
          versions: [
            {
              id: 10,
              version: "1.0 Beta45",
              releasedAt: "2025-01-01",
              notes: "Old release",
              downloadUrl: "https://github.com/org/repo/releases/tag/v1",
            },
            { id: 20, version: "2.0", releasedAt: "2026-01-01", notes: "New release" },
          ],
          latestVersion: { id: 20, version: "2.0" },
        },
      }).mod,
    );
    expect(detail.latestVersion).toBe("2.0");
    expect(detail.versions).toHaveLength(2);
    expect(detail.versions[1]).toMatchObject({
      id: "10",
      version: "1.0 Beta45",
      notes: "Old release",
      url: "https://api.tuforums.com/v2/mods/mod/1.0%20Beta45/download",
      projectUrl: "https://github.com/org/repo/releases/tag/v1",
    });
  });
  it("retains the current release when TUF has no historical version records", () => {
    const detail = mapCatalogDetail({ id: 1, slug: "one", name: "One", version: "1.0" });
    expect(detail.versions.map((version) => version.version)).toEqual(["1.0"]);
    expect(mapCatalogDetail({ id: 1, slug: "one", name: "One" }).versions).toEqual([]);
  });
});
