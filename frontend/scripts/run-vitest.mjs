import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { delimiter, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export function supportsNode(version) {
  const [major, minor] = version.trim().split(".").map(Number);
  return major > 22 || (major === 22 && minor >= 12);
}

function usableNode(binary) {
  const result = spawnSync(binary, ["-p", "process.versions.bun ? '' : process.versions.node"], {
    encoding: "utf8",
  });
  return result.status === 0 && supportsNode(result.stdout);
}

export function windowsArchive(manifest, arch) {
  if (!["x64", "arm64"].includes(arch)) throw new Error(`Unsupported Node architecture: ${arch}`);
  const pattern = new RegExp(
    `^([a-f0-9]{64})\\s+(node-(v22\\.\\d+\\.\\d+)-win-${arch}\\.zip)$`,
    "m",
  );
  const match = manifest.replace(/\r/g, "").match(pattern);
  if (!match)
    throw new Error("The official Node.js checksum manifest has no matching Windows ZIP.");
  return { checksum: match[1], filename: match[2], version: match[3] };
}

async function download(url) {
  const response = await fetch(url, { signal: AbortSignal.timeout(60_000) });
  if (!response.ok) throw new Error(`Node.js download failed: ${response.status} ${url}`);
  return response;
}

async function nodeBinary() {
  if (process.env.AMM_NODE_BINARY) {
    if (!usableNode(process.env.AMM_NODE_BINARY))
      throw new Error("AMM_NODE_BINARY must point to Node.js 22.12 or newer, not Bun.");
    return process.env.AMM_NODE_BINARY;
  }
  if (!process.versions.bun && usableNode(process.execPath)) return process.execPath;
  if (usableNode("node")) {
    const result = spawnSync("node", ["-p", "process.execPath"], { encoding: "utf8" });
    return result.stdout.trim();
  }
  if (process.platform !== "win32")
    throw new Error("Vitest requires Node.js 22.12 or newer on PATH.");

  // Keep downloaded tools outside the source checkout so Git remains clean.
  const cache = join(
    process.env.LOCALAPPDATA || tmpdir(),
    "ADOFAIModManager",
    "tools",
    `node22-${process.arch}`,
  );
  const binary = join(cache, "node.exe");
  if (existsSync(binary) && usableNode(binary)) return binary;
  const base = "https://nodejs.org/dist";
  const manifest = await (await download(`${base}/latest-v22.x/SHASUMS256.txt`)).text();
  const archive = windowsArchive(manifest, process.arch);
  console.log(`Installing official Node.js ${archive.version} for Vitest (${process.arch})...`);
  const data = Buffer.from(
    await (await download(`${base}/${archive.version}/${archive.filename}`)).arrayBuffer(),
  );
  if (createHash("sha256").update(data).digest("hex") !== archive.checksum) {
    throw new Error("Node.js archive SHA-256 verification failed.");
  }
  const temporary = mkdtempSync(join(tmpdir(), "amm-node-"));
  try {
    const zip = join(temporary, "node.zip");
    const extracted = join(temporary, "extracted");
    writeFileSync(zip, data);
    const result = spawnSync(
      "powershell.exe",
      [
        "-NoProfile",
        "-NonInteractive",
        "-Command",
        "Expand-Archive -LiteralPath $env:AMM_NODE_ARCHIVE -DestinationPath $env:AMM_NODE_DEST -Force",
      ],
      {
        stdio: "inherit",
        env: { ...process.env, AMM_NODE_ARCHIVE: zip, AMM_NODE_DEST: extracted },
      },
    );
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error("Failed to extract the Node.js archive.");
    const executable = join(extracted, archive.filename.slice(0, -4), "node.exe");
    if (!usableNode(executable)) throw new Error("Downloaded Node.js failed its runtime check.");
    mkdirSync(cache, { recursive: true });
    copyFileSync(executable, binary);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
  return binary;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const node = await nodeBinary();
  const cli = fileURLToPath(new URL("../node_modules/vitest/vitest.mjs", import.meta.url));
  console.log(`Running Vitest with Node.js: ${node}`);
  const result = spawnSync(node, [cli, "run", ...process.argv.slice(2)], {
    stdio: "inherit",
    env: { ...process.env, PATH: `${dirname(node)}${delimiter}${process.env.PATH || ""}` },
  });
  if (result.error) throw result.error;
  process.exit(result.status ?? 1);
}
