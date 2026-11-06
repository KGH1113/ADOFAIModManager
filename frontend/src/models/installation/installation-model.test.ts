import { describe, expect, it } from "vitest";
import { engineResponseSchema } from "@/schemas/engine/engine-schema";
import { type InjectionMethod, mapInstallation } from "./installation-model";

function response(status: {
  injectionMethod?: InjectionMethod;
  hookInstalled?: boolean;
  managerInstalled?: boolean;
  managerVersion?: string;
}) {
  return engineResponseSchema.parse({
    ok: true,
    detected: true,
    gameRunning: false,
    status,
  });
}

describe("installation status", () => {
  it("treats an active Doorstop deployment as installed", () => {
    const installation = mapInstallation(
      response({
        injectionMethod: "doorstop",
        hookInstalled: false,
        managerInstalled: true,
        managerVersion: "0.33.3.0",
      }),
    );
    expect(installation.injection).toBe("doorstop");
    expect(installation.installed).toBe(true);
    expect(installation.version).toBe("0.33.3.0");
  });

  it("keeps a disabled Doorstop or a BepInEx Doorstop uninstalled", () => {
    const installation = mapInstallation(
      response({ injectionMethod: "none", hookInstalled: false, managerInstalled: true }),
    );
    expect(installation.injection).toBe("none");
    expect(installation.installed).toBe(false);
  });

  it("reports both injection methods and still requires the manager files", () => {
    const both = mapInstallation(
      response({ injectionMethod: "both", hookInstalled: true, managerInstalled: true }),
    );
    expect(both.injection).toBe("both");
    expect(both.installed).toBe(true);
    const missingManager = mapInstallation(
      response({ injectionMethod: "doorstop", hookInstalled: false, managerInstalled: false }),
    );
    expect(missingManager.injection).toBe("doorstop");
    expect(missingManager.installed).toBe(false);
  });

  it("falls back to the assembly hook when an older engine omits the injection method", () => {
    const hooked = mapInstallation(response({ hookInstalled: true, managerInstalled: true }));
    expect(hooked.injection).toBe("assembly");
    expect(hooked.installed).toBe(true);
    const managerOnly = mapInstallation(response({ hookInstalled: false, managerInstalled: true }));
    expect(managerOnly.installed).toBe(false);
  });
});
