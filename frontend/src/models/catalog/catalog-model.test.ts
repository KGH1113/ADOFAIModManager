import { describe, expect, it } from "vitest";
import { catalogSchema } from "@/schemas/catalog/catalog-schema";
import { filterCatalog, mapCatalogMod, tufDownloadUrl } from "./catalog-model";

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
});
