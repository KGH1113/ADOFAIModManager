import { spawnSync } from "node:child_process";
import { chmodSync, cpSync, mkdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
function run(command, args) {
  const result = spawnSync(command, args, { cwd: root, stdio: "inherit" });
  if (result.status !== 0) throw new Error(`${command} failed (${result.status})`);
}
const target =
  process.env.TAURI_ENV_TARGET_TRIPLE ||
  process.env.AMM_TARGET ||
  spawnSync("rustc", ["--print", "host-tuple"], { encoding: "utf8" }).stdout.trim();
const targets = {
  "aarch64-apple-darwin": "osx-arm64",
  "x86_64-apple-darwin": "osx-x64",
  "x86_64-pc-windows-msvc": "win-x64",
  "aarch64-pc-windows-msvc": "win-arm64",
  "x86_64-unknown-linux-gnu": "linux-x64",
  "aarch64-unknown-linux-gnu": "linux-arm64",
};
const rid = targets[target];
if (!rid) throw new Error(`Unsupported desktop target: ${target}`);
const extension = rid.startsWith("win") ? ".exe" : "";
// Link the engine for the same minimum macOS as the app bundle. The Intel build
// supports older Macs (.NET 10 cannot target anything below macOS 12).
const macConfig =
  target === "x86_64-apple-darwin" ? "tauri.intel-mac.conf.json" : "tauri.conf.json";
const macMinimum = rid.startsWith("osx")
  ? JSON.parse(readFileSync(join(root, "src-tauri", macConfig), "utf8")).bundle.macOS
      .minimumSystemVersion
  : undefined;
// Minimum macOS a thin 64-bit Mach-O was linked for, as [major, minor, patch].
function machOMinimumMacOS(path) {
  const data = readFileSync(path);
  if (data.readUInt32LE(0) !== 0xfeedfacf) throw new Error(`Not a 64-bit Mach-O: ${path}`);
  let offset = 32;
  for (let i = 0; i < data.readUInt32LE(16); i++) {
    const command = data.readUInt32LE(offset);
    // LC_BUILD_VERSION stores minos at +12; the older LC_VERSION_MIN_MACOSX at +8.
    if (command === 0x32 || command === 0x24) {
      const version = data.readUInt32LE(offset + (command === 0x32 ? 12 : 8));
      return [version >>> 16, (version >>> 8) & 0xff, version & 0xff];
    }
    offset += data.readUInt32LE(offset + 4);
  }
  throw new Error(`No minimum macOS version in ${path}`);
}
const output = join(root, "build", `tauri-engine-${rid}`);
run("dotnet", [
  "publish",
  "Engine/NativeUmm.Cli/NativeUmm.Cli.csproj",
  "-c",
  "Release",
  "-r",
  rid,
  "-o",
  output,
  ...(macMinimum
    ? [`-p:AppleMinOSVersion=${macMinimum}`, `-p:SupportedOSPlatformVersion=${macMinimum}`]
    : []),
]);
if (macMinimum) {
  // .NET silently raises versions below its own floor. An engine that needs a
  // newer macOS than the app would launch fine yet fail every install, and the
  // updater would still deliver it, so stop the build instead.
  const engine = machOMinimumMacOS(join(output, "UMMInstallerEngine"));
  const app = [...macMinimum.split(".").map(Number), 0, 0].slice(0, 3);
  const index = engine.findIndex((part, i) => part !== app[i]);
  if (index >= 0 && engine[index] > app[index]) {
    throw new Error(
      `Engine requires macOS ${engine.join(".")} but src-tauri/${macConfig} allows ${macMinimum}. ` +
        "Raise minimumSystemVersion there to match the .NET runtime.",
    );
  }
}
mkdirSync(join(root, "src-tauri/binaries"), { recursive: true });
const binary = join(root, `src-tauri/binaries/UMMInstallerEngine-${target}${extension}`);
cpSync(join(output, `UMMInstallerEngine${extension}`), binary);
if (!extension) chmodSync(binary, 0o755);
const payload = join(root, "src-tauri/resources/UMMPayload");
mkdirSync(payload, { recursive: true });
cpSync(join(root, "Resources/UMMPayload"), payload, { recursive: true });
if (rid.startsWith("osx")) {
  const harmony = readFileSync(join(payload, "0Harmony.dll"));
  for (const marker of [
    "MonoMod.Core, Version=1.3.3.0",
    "MethodPatcher",
    "Arm64Arch",
    "exhelper_macos_arm64.dylib",
    "exhelper_macos_x86_64.dylib",
  ]) {
    if (!harmony.includes(Buffer.from(marker)))
      throw new Error(`Incompatible Harmony payload: ${marker}`);
  }
}
mkdirSync(join(root, "frontend/public/assets"), { recursive: true });
cpSync(
  join(root, "Resources/AppIconMaster.png"),
  join(root, "frontend/public/assets/app-icon.png"),
);
run(process.execPath, [
  "run",
  "tauri",
  "icon",
  "Resources/AppIconMaster.png",
  "--output",
  "src-tauri/icons",
]);
console.log(`Desktop resources ready: ${target}`);
