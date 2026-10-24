import { describe, expect, it, vi } from "vitest";
import { createMockApi } from "@/mocks/create-mock-api";
import { installRecommendation, type RecommendationSession } from "./install-recommendation";

const path = "/Preview/ADanceOfFireAndIce.app";
const session = (): RecommendationSession => ({
  installed: new Set(),
  configured: new Set(),
});
async function prepare() {
  const api = createMockApi();
  await api.installation.execute(path, "install");
  return api;
}
describe("recommended installation plans", () => {
  it("updates an old dependency but never downgrades a newer one", async () => {
    for (const [version, shouldUpdate] of [
      ["1.0.0.1", true],
      ["2.0.0.0", false],
    ] as const) {
      const api = await prepare();
      await installRecommendation(
        api,
        path,
        "jrp",
        session(),
        new AbortController().signal,
        () => {},
      );
      const original = await api.installation.load(path);
      vi.spyOn(api.installation, "load").mockResolvedValueOnce({
        ...original,
        mods: original.mods.map((m) => (m.id === "JALib" ? { ...m, version } : m)),
      });
      const install = vi.spyOn(api.mods, "install");
      await installRecommendation(
        api,
        path,
        "calibration",
        session(),
        new AbortController().signal,
        () => {},
      );
      expect(install).toHaveBeenCalledTimes(shouldUpdate ? 2 : 1);
    }
  });
  it("inspects all archives first, installs JALib before JRP, and reuses existing dependencies", async () => {
    const api = await prepare();
    const order: string[] = [];
    const original = api.mods.install;
    vi.spyOn(api.mods, "install").mockImplementation(async (game, source) => {
      order.push((await api.mods.inspect(game, source)).id);
      return original(game, source);
    });
    const discard = vi.spyOn(api.catalog, "discard");
    await installRecommendation(
      api,
      path,
      "jrp",
      session(),
      new AbortController().signal,
      () => {},
    );
    expect(order).toEqual(["JALib", "JipperResourcePack"]);
    expect(discard).toHaveBeenCalledTimes(2);
    await installRecommendation(
      api,
      path,
      "calibration",
      session(),
      new AbortController().signal,
      () => {},
    );
    expect(order).toEqual(["JALib", "JipperResourcePack", "BetterCalibration"]);
  });
  it("rejects mismatched archives before installing any dependency and discards downloads", async () => {
    const api = await prepare();
    const inspect = api.mods.inspect;
    vi.spyOn(api.mods, "inspect").mockImplementation(async (game, source) => {
      const info = await inspect(game, source);
      return info.id === "JipperResourcePack" ? { ...info, id: "WrongMod" } : info;
    });
    const install = vi.spyOn(api.mods, "install");
    const discard = vi.spyOn(api.catalog, "discard");
    await expect(
      installRecommendation(api, path, "jrp", session(), new AbortController().signal, () => {}),
    ).rejects.toThrow("archiveMismatch");
    expect(install).not.toHaveBeenCalled();
    expect(discard).toHaveBeenCalledTimes(2);
  });
  it("applies defaults to new recommendations and preserves previously installed mods", async () => {
    const api = await prepare();
    const setup = vi.spyOn(api.mods, "configureOnboarding");
    await installRecommendation(
      api,
      path,
      "jrp",
      session(),
      new AbortController().signal,
      () => {},
    );
    const flow = session();
    await installRecommendation(api, path, "chatter", flow, new AbortController().signal, () => {});
    expect(setup).toHaveBeenCalledWith(path, "jrp-defaults");
    expect(setup).toHaveBeenCalledWith(path, "chatter-defaults");
    await installRecommendation(api, path, "tuf", flow, new AbortController().signal, () => {});
    expect(setup).toHaveBeenCalledTimes(3);
    await installRecommendation(
      api,
      path,
      "tuf",
      session(),
      new AbortController().signal,
      () => {},
    );
    expect(setup).toHaveBeenCalledTimes(3);
  });
  it("retries configuration failures without reinstalling a mod", async () => {
    const api = await prepare();
    const install = vi.spyOn(api.mods, "install");
    const setup = vi
      .spyOn(api.mods, "configureOnboarding")
      .mockRejectedValueOnce(new Error("write failed"));
    const flow = session();
    await expect(
      installRecommendation(api, path, "tuf", flow, new AbortController().signal, () => {}),
    ).rejects.toThrow("write failed");
    await installRecommendation(api, path, "tuf", flow, new AbortController().signal, () => {});
    expect(install).toHaveBeenCalledTimes(1);
    expect(setup).toHaveBeenCalledTimes(2);
  });
  it("cancels after download without writing mods and releases the temporary archive", async () => {
    const api = await prepare();
    const abort = new AbortController();
    const download = api.catalog.download;
    vi.spyOn(api.catalog, "download").mockImplementation(async (url, id) => {
      const token = await download(url, id);
      abort.abort();
      return token;
    });
    const install = vi.spyOn(api.mods, "install");
    const discard = vi.spyOn(api.catalog, "discard");
    await expect(
      installRecommendation(api, path, "jrp", session(), abort.signal, () => {}),
    ).rejects.toThrow("cancelled");
    expect(install).not.toHaveBeenCalled();
    expect(discard).toHaveBeenCalledTimes(1);
  });
  it("stops before downloading or installing when TUF is unavailable", async () => {
    const api = await prepare();
    const catalog = vi.spyOn(api.catalog, "list").mockRejectedValue(new Error("TUF is down"));
    const download = vi.spyOn(api.catalog, "download");
    const install = vi.spyOn(api.mods, "install");
    await expect(
      installRecommendation(api, path, "jrp", session(), new AbortController().signal, () => {}),
    ).rejects.toThrow("TUF is down");
    expect(catalog).toHaveBeenCalledOnce();
    expect(download).not.toHaveBeenCalled();
    expect(install).not.toHaveBeenCalled();
  });
  it("downloads recommendations and dependencies through TUF", async () => {
    const api = await prepare();
    const download = vi.spyOn(api.catalog, "download");
    await installRecommendation(
      api,
      path,
      "tuf",
      session(),
      new AbortController().signal,
      () => {},
    );
    expect(download).toHaveBeenCalledWith(
      "https://api.tuforums.com/v2/mods/tufhelper/3.1.1/download",
      expect.any(String),
    );
    await installRecommendation(
      api,
      path,
      "jrp",
      session(),
      new AbortController().signal,
      () => {},
    );
    expect(download).toHaveBeenCalledWith(
      "https://api.tuforums.com/v2/mods/jalib/1.0.0.45%20beta45/download",
      expect.any(String),
    );
    expect(download).toHaveBeenCalledWith(
      "https://api.tuforums.com/v2/mods/jrp/1.5.2.0/download",
      expect.any(String),
    );
  });
  it("resolves the complete TUF plan before downloading when a recommendation is missing", async () => {
    const api = await prepare();
    const catalog = await api.catalog.list();
    vi.spyOn(api.catalog, "list").mockResolvedValue(catalog.filter((mod) => mod.id !== "jrp"));
    const download = vi.spyOn(api.catalog, "download");
    const install = vi.spyOn(api.mods, "install");
    await expect(
      installRecommendation(api, path, "jrp", session(), new AbortController().signal, () => {}),
    ).rejects.toThrow("download_unavailable");
    expect(download).not.toHaveBeenCalled();
    expect(install).not.toHaveBeenCalled();
  });
  it("installs Lite without IPC and enables beta updates for Countdown", async () => {
    const api = await prepare();
    const catalog = await api.catalog.list();
    vi.spyOn(api.catalog, "list").mockResolvedValue(
      catalog.filter((mod) => mod.id !== "adofaiipc"),
    );
    const setup = vi.spyOn(api.mods, "configureOnboarding");
    const game = await installRecommendation(
      api,
      path,
      "tufLite",
      session(),
      new AbortController().signal,
      () => {},
    );
    expect(game.mods.map((m) => m.id)).toEqual(["TUFHelperLite"]);
    expect(setup).not.toHaveBeenCalled();
    await installRecommendation(
      api,
      path,
      "countdown",
      session(),
      new AbortController().signal,
      () => {},
    );
    expect(setup).toHaveBeenCalledWith(path, "countdown-defaults");
  });
});
