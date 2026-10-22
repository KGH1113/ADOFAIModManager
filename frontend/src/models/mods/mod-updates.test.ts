import { describe, expect, it } from "vitest";
import { mapCatalogMod } from "@/models/catalog/catalog-model";
import type { InstalledMod } from "@/models/installation/installation-model";
import { checkModUpdate, compareVersions } from "./mod-updates";

const installed = (id: string, version: string, name = id): InstalledMod => ({
  id,
  name,
  version,
  installed: true,
  enabled: true,
  status: "installed",
  path: `/Mods/${id}`,
  requirements: [],
});
const remote = (slug: string, name: string, version: string) =>
  mapCatalogMod({ id: 1, slug, name, version });
describe("mod updates", () => {
  it("orders numeric versions, optional v prefixes, trailing zeros and prereleases", () => {
    expect(compareVersions("v1.10.0", "1.9.9")).toBe(1);
    expect(compareVersions("1.5.2", "1.5.2.0")).toBe(0);
    expect(compareVersions("1.0", "1.0 beta45")).toBe(1);
    expect(compareVersions("1.0 Beta46", "1.0 beta45")).toBe(1);
    expect(compareVersions("nightly", "1.0")).toBeNull();
  });
  it("matches aliases, detects updates and never offers downgrades", () => {
    const jrp = remote("jrp", "JipperResourcePack", "1.5.2");
    expect(checkModUpdate(installed("JipperResourcePack", "1.4"), [jrp]).state).toBe("available");
    expect(checkModUpdate(installed("JipperResourcePack", "1.5.2.0"), [jrp]).state).toBe("current");
    expect(checkModUpdate(installed("JipperResourcePack", "2.0"), [jrp]).state).toBe("current");
    expect(checkModUpdate(installed("JipperResourcePack", "nightly"), [jrp]).state).toBe("unknown");
  });
  it("matches normalized names but leaves absent or ambiguous mods unmatched", () => {
    const countdown = remote("enhanced-countdown", "Enhanced Countdown", "0.2.3");
    expect(checkModUpdate(installed("EnhancedCountdown", "0.2.0"), [countdown]).state).toBe(
      "available",
    );
    expect(checkModUpdate(installed("Other", "1.0"), [countdown]).state).toBe("unlisted");
    expect(
      checkModUpdate(installed("Ambiguous", "1.0"), [
        remote("a", "Ambiguous", "2.0"),
        remote("b", "Ambiguous", "3.0"),
      ]).state,
    ).toBe("unlisted");
  });
  it("does not collapse distinct non-Latin mod names", () => {
    expect(
      checkModUpdate(installed("LocalMod", "1.0", "내 모드"), [remote("other", "다른 모드", "2.0")])
        .state,
    ).toBe("unlisted");
  });
});
