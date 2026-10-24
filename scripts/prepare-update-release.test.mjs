import { describe, expect, it } from "bun:test";
import { readFileSync } from "node:fs";
import { createManifest, validateVersions } from "./prepare-update-release.mjs";

const files = [
  "mac/ADOFAI Mod Manager.app.tar.gz",
  "win/manager.exe",
  "linux/manager.AppImage",
].flatMap((path) => [path, `${path}.sig`]);
describe("update release manifest", () => {
  it("assembles all supported targets using GitHub-safe names and signature content", () => {
    const manifest = createManifest(
      "0.3.0",
      "v0.3.0",
      "KGH1113/ADOFAIModManager",
      files,
      () => "signature\n",
    );
    expect(Object.keys(manifest.platforms)).toHaveLength(3);
    expect(manifest.platforms["darwin-aarch64"]).toEqual({
      signature: "signature",
      url: "https://github.com/KGH1113/ADOFAIModManager/releases/download/v0.3.0/ADOFAI.Mod.Manager.app.tar.gz",
    });
  });
  it("rejects partial releases, duplicate artifacts, empty signatures and mismatched tags", () => {
    expect(() => createManifest("0.3.0", "v0.3.0", "a/b", files.slice(2), () => "sig")).toThrow();
    expect(() =>
      createManifest(
        "0.3.0",
        "v0.3.0",
        "a/b",
        [...files, "extra.exe", "extra.exe.sig"],
        () => "sig",
      ),
    ).toThrow();
    expect(() => createManifest("0.3.0", "v0.3.0", "a/b", files, () => " ")).toThrow();
    expect(() => createManifest("0.3.0", "v0.4.0", "a/b", files, () => "sig")).toThrow();
    expect(() =>
      createManifest("0.3.0-beta.1", "v0.3.0-beta.1", "a/b", files, () => "sig"),
    ).toThrow();
  });
  it("checks the repository version against the release tag", () => {
    const { version } = JSON.parse(readFileSync("package.json", "utf8"));
    expect(validateVersions(process.cwd(), `v${version}`)).toBe(version);
    expect(() => validateVersions(process.cwd(), "v999.0.0")).toThrow();
  });
});
