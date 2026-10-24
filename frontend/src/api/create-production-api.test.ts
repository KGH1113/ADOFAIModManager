import { describe, expect, it, vi } from "vitest";
import { checkModUpdate } from "@/models/mods/mod-updates";
import {
  copyKeyLayouts,
  defaultKeyLayouts,
  keyViewerStyles,
} from "@/models/onboarding/key-bindings";
import type { DesktopClient } from "@/shared/clients/desktop-client";
import { createProductionApi } from "./create-production-api";

function client(invoke: DesktopClient["invoke"]): DesktopClient {
  return {
    invoke,
    chooseGame: async () => null,
    chooseArchive: async () => null,
    onFiles: async () => () => {},
  };
}
describe("native API adapters", () => {
  it("round trips the UMM shortcut through native settings", async () => {
    const ummShortcut = { keyCode: "K", modifiers: 3 };
    const invoke = vi.fn(async () => ({ ok: true, detected: true, ummShortcut }));
    const api = createProductionApi(client(invoke));
    expect(await api.mods.loadUmmShortcut("/game")).toEqual(ummShortcut);
    expect(await api.mods.saveUmmShortcut("/game", ummShortcut)).toEqual(ummShortcut);
    expect(invoke).toHaveBeenLastCalledWith("engine_request", {
      request: {
        action: "saveummshortcut",
        gamePath: "/game",
        shortcutKey: "K",
        shortcutModifiers: 3,
      },
    });
  });
  it.each(keyViewerStyles)(
    "round trips Key%i bindings with the explicit selected style",
    async (style) => {
      const bindings = { style, layouts: copyKeyLayouts(defaultKeyLayouts) };
      const invoke = vi.fn(async () => ({ ok: true, detected: true, keyBindings: bindings }));
      const api = createProductionApi(client(invoke));
      expect(await api.mods.loadJrpKeys("/game")).toEqual(bindings);
      expect(await api.mods.saveJrpKeys("/game", bindings.layouts[style], style)).toEqual(bindings);
      expect(invoke).toHaveBeenLastCalledWith("engine_request", {
        request: {
          action: "savejrpkeys",
          gamePath: "/game",
          keyBindings: bindings.layouts[style],
          keyViewerStyle: style,
        },
      });
    },
  );
  it("rejects unsupported styles and incomplete native layout responses", async () => {
    for (const keyBindings of [
      { style: 14, layouts: copyKeyLayouts(defaultKeyLayouts) },
      {
        style: 8,
        layouts: { ...copyKeyLayouts(defaultKeyLayouts), 8: [...defaultKeyLayouts[12]] },
      },
    ]) {
      const api = createProductionApi(
        client(async () => ({ ok: true, detected: true, keyBindings })),
      );
      await expect(api.mods.loadJrpKeys("/game")).rejects.toThrow();
    }
  });
  it("validates TUF catalog data and downloads through the native command", async () => {
    const invoke = vi.fn(async (command: string) =>
      command === "runtime_info"
        ? { platform: "macos" }
        : command === "catalog_fetch"
          ? [
              {
                id: 1,
                slug: "pacl2",
                name: "PACL2",
                version: "2.5.301",
                downloadUrl: "https://example.com/page",
              },
            ]
          : "download-token",
    );
    const api = createProductionApi(client(invoke));
    const mod = (await api.catalog.list()).find((mod) => mod.id === "pacl2");
    expect(mod).toBeDefined();
    if (!mod) throw new Error("Missing catalog entry");
    expect(mod.url).toBe("https://api.tuforums.com/v2/mods/pacl2/2.5.301/download");
    expect(await api.catalog.download(mod.url, "request")).toBe("download-token");
    expect(invoke).toHaveBeenLastCalledWith("download_mod", { url: mod.url, requestId: "request" });
  });
  it("propagates TUF outages instead of supplying a fallback catalog", async () => {
    const invoke = vi.fn(async () => {
      throw new Error("TUF unavailable");
    });
    const api = createProductionApi(client(invoke));
    await expect(api.catalog.list()).rejects.toThrow("TUF unavailable");
    expect(invoke).toHaveBeenCalledExactlyOnceWith("catalog_fetch");
  });
  it("loads version history and downloads the selected historical version through TUF", async () => {
    const invoke = vi.fn(async (command: string) =>
      command === "catalog_details"
        ? {
            mod: {
              id: 1,
              slug: "chatter",
              name: "Chatter",
              version: "0.1.3",
              versions: [
                { id: 1, version: "0.1.3", releasedAt: "2026-10-22" },
                { id: 2, version: "0.1.2", releasedAt: "2026-09-01", notes: "Earlier release" },
              ],
            },
          }
        : "historical-token",
    );
    const api = createProductionApi(client(invoke));
    const detail = await api.catalog.details("chatter");
    expect(invoke).toHaveBeenCalledExactlyOnceWith("catalog_details", { slug: "chatter" });
    const historical = detail.versions[1];
    expect(historical.version).toBe("0.1.2");
    expect(await api.catalog.download(historical.url, "history-request")).toBe("historical-token");
    expect(invoke).toHaveBeenLastCalledWith("download_mod", {
      url: "https://api.tuforums.com/v2/mods/chatter/0.1.2/download",
      requestId: "history-request",
    });
  });
  it("rejects mismatched or malformed detail responses and preserves detail errors", async () => {
    const mismatched = createProductionApi(
      client(async () => ({ mod: { id: 1, slug: "wrong", name: "Wrong" } })),
    );
    await expect(mismatched.catalog.details("requested")).rejects.toThrow("different mod");
    const malformed = createProductionApi(
      client(async () => ({
        mod: { id: 1, slug: "mod", name: "Mod", versions: [{ version: "1.0" }] },
      })),
    );
    await expect(malformed.catalog.details("mod")).rejects.toThrow();
    const unavailable = createProductionApi(
      client(async () => {
        throw new Error("Detail unavailable");
      }),
    );
    await expect(unavailable.catalog.details("mod")).rejects.toThrow("Detail unavailable");
  });
  it("uses TUF versions for recommendations and their updates without adding pinned mods", async () => {
    const invoke = vi.fn(async (command: string) =>
      command === "catalog_fetch"
        ? [{ id: 1, slug: "jrp", name: "JRP", version: "99.0", description: "Resource pack" }]
        : "update-token",
    );
    const api = createProductionApi(client(invoke));
    const mods = await api.catalog.list();
    const jrp = mods.filter((mod) => mod.id === "jrp");
    expect(jrp).toHaveLength(1);
    expect(mods).toHaveLength(1);
    expect(jrp[0]).toMatchObject({
      version: "99.0",
      description: "Resource pack",
      url: "https://api.tuforums.com/v2/mods/jrp/99.0/download",
    });
    const update = checkModUpdate(
      {
        id: "JipperResourcePack",
        name: "JRP",
        version: "1.5.2.0",
        path: "/game/Mods/JRP",
        installed: true,
        enabled: true,
        status: "installed",
        requirements: [],
      },
      mods,
    );
    expect(update.state).toBe("available");
    if (!update.latest) throw new Error("Missing TUF update");
    expect(await api.catalog.download(update.latest.url, "update-request")).toBe("update-token");
    expect(invoke).toHaveBeenLastCalledWith("download_mod", {
      url: "https://api.tuforums.com/v2/mods/jrp/99.0/download",
      requestId: "update-request",
    });
  });
  it("validates untrusted engine responses and treats missing games as a setup state", async () => {
    const api = createProductionApi(
      client(async () => ({ ok: false, detected: false, error: "not found" })),
    );
    expect((await api.installation.load(null)).detected).toBe(false);
    const invalid = createProductionApi(client(async () => ({ ok: "yes", detected: true })));
    await expect(invalid.installation.load(null)).rejects.toThrow();
    await expect(api.installation.execute("/game", "install")).rejects.toThrow("not found");
  });
  it("passes captured archive identity and uses forcePayload for repair", async () => {
    const invoke = vi.fn(async () => ({ ok: true, detected: true, status: {}, mods: [] }));
    const api = createProductionApi(client(invoke));
    await api.mods.install("/captured/game", { token: "owned-archive" });
    expect(invoke).toHaveBeenLastCalledWith("engine_request", {
      request: { action: "installmod", gamePath: "/captured/game", downloadToken: "owned-archive" },
    });
    await api.installation.execute("/game", "repair");
    expect(invoke).toHaveBeenLastCalledWith("engine_request", {
      request: { action: "install", gamePath: "/game", forcePayload: true },
    });
  });
  it("does not report success when inspection metadata is missing", async () => {
    const api = createProductionApi(client(async () => ({ ok: true, detected: true })));
    await expect(api.mods.inspect("/game", { path: "/tmp/mod.zip" })).rejects.toThrow("metadata");
  });
});
