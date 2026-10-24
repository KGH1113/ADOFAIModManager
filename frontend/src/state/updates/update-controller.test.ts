import { afterEach, describe, expect, it, vi } from "vitest";
import type { UpdateApi } from "@/api/updates/update-api";
import { createUpdateController, UPDATE_INTERVAL } from "./update-controller";

const release = { version: "0.3.0", notes: "A new release" };
function setup(check = vi.fn<UpdateApi["check"]>().mockResolvedValue(release)) {
  const api = {
    check,
    download: vi.fn<UpdateApi["download"]>().mockResolvedValue(),
    install: vi.fn<UpdateApi["install"]>().mockResolvedValue(),
  };
  const lock = { acquire: vi.fn(() => true), release: vi.fn() };
  return { api, lock, controller: createUpdateController(api, lock) };
}
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}
afterEach(() => vi.useRealTimers());

describe("application updates", () => {
  it("downloads and installs on startup, retaining the lock for restart", async () => {
    const { api, lock, controller } = setup();
    await controller.check();
    expect(api.download).toHaveBeenCalledOnce();
    expect(api.install).toHaveBeenCalledOnce();
    expect(controller.store.getState().phase).toBe("installing");
    expect(lock.release).not.toHaveBeenCalled();
    await controller.restart();
    expect(api.install).toHaveBeenCalledOnce();
  });
  it("continues startup when there is no update or the network fails", async () => {
    for (const check of [
      vi.fn<UpdateApi["check"]>().mockResolvedValue(null),
      vi.fn<UpdateApi["check"]>().mockRejectedValue(new Error("offline")),
    ]) {
      const { controller, api } = setup(check);
      await controller.check();
      expect(controller.store.getState().startup).toBe(false);
      expect(api.download).not.toHaveBeenCalled();
      expect(api.install).not.toHaveBeenCalled();
    }
  });
  it("deferring an in-flight startup download prevents automatic installation", async () => {
    const { controller, api } = setup();
    const download = deferred<void>();
    api.download.mockReturnValue(download.promise);
    const checking = controller.check();
    await Promise.resolve();
    expect(controller.store.getState().phase).toBe("downloading");
    controller.defer();
    download.resolve();
    await checking;
    expect(controller.store.getState().phase).toBe("ready");
    expect(api.install).not.toHaveBeenCalled();
    await controller.restart();
    expect(api.install).toHaveBeenCalledOnce();
  });
  it("deduplicates startup effects and concurrent manual checks", async () => {
    const result = deferred<null>();
    const { controller, api } = setup(vi.fn<UpdateApi["check"]>().mockReturnValue(result.promise));
    controller.start();
    controller.start();
    await controller.check();
    expect(api.check).toHaveBeenCalledOnce();
    result.resolve(null);
    await vi.waitFor(() => expect(controller.store.getState().startup).toBe(false));
  });
  it("throttles wake events and downloads background updates without installing", async () => {
    vi.useFakeTimers();
    const { controller, api } = setup(
      vi.fn<UpdateApi["check"]>().mockResolvedValueOnce(null).mockResolvedValue(release),
    );
    await controller.check();
    await controller.check(true);
    expect(api.check).toHaveBeenCalledOnce();
    vi.advanceTimersByTime(UPDATE_INTERVAL);
    await controller.check(true);
    expect(controller.store.getState().phase).toBe("ready");
    expect(api.install).not.toHaveBeenCalled();
    await controller.check(true);
    expect(api.check).toHaveBeenCalledTimes(2);
  });
  it("never installs over a mod operation, and allows restarting once it finishes", async () => {
    const { controller, api, lock } = setup();
    lock.acquire.mockReturnValue(false);
    await controller.check();
    expect(controller.store.getState()).toMatchObject({ startup: false, phase: "ready" });
    await controller.restart();
    expect(api.install).not.toHaveBeenCalled();
    lock.acquire.mockReturnValue(true);
    await controller.restart();
    expect(api.install).toHaveBeenCalledOnce();
  });
  it("does not install a failed download/signature and can retry", async () => {
    const { controller, api } = setup();
    api.download.mockRejectedValueOnce(new Error("invalid signature"));
    await controller.check();
    expect(api.install).not.toHaveBeenCalled();
    expect(controller.store.getState()).toMatchObject({ startup: false, phase: "error" });
    await controller.check();
    expect(controller.store.getState().phase).toBe("ready");
    expect(api.install).not.toHaveBeenCalled();
  });
  it("releases the operation lock on install failure and retries without redownloading", async () => {
    const { controller, api, lock } = setup();
    api.install.mockRejectedValueOnce(new Error("permission denied"));
    await controller.check();
    expect(lock.release).toHaveBeenCalledOnce();
    expect(controller.store.getState()).toMatchObject({
      phase: "ready",
      startup: false,
      error: true,
    });
    await controller.restart();
    expect(api.download).toHaveBeenCalledOnce();
    expect(api.install).toHaveBeenCalledTimes(2);
  });
});
