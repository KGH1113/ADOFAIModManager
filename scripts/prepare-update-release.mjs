import { cpSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { basename, join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const targets = {
  "darwin-aarch64": ".app.tar.gz",
  "windows-x86_64": ".exe",
  "linux-x86_64": ".AppImage",
};

// GitHub normalizes spaces and special characters in uploaded asset names.
// Normalize before copying and constructing URLs so the manifest matches exactly.
export function releaseAssetName(path) {
  return basename(path).replace(/[^A-Za-z0-9._-]/g, ".");
}

export function createManifest(version, tag, repository, files, readSignature) {
  if (!/^\d+\.\d+\.\d+$/.test(version) || tag !== `v${version}`) {
    throw new Error("Release tag must match the stable application version (vX.Y.Z).");
  }
  if (!/^[\w.-]+\/[\w.-]+$/.test(repository)) throw new Error("Invalid repository");
  const platforms = {};
  for (const [platform, suffix] of Object.entries(targets)) {
    const matches = files.filter((file) => file.endsWith(suffix) && files.includes(`${file}.sig`));
    if (matches.length !== 1) throw new Error(`Expected one signed artifact for ${platform}`);
    const artifact = matches[0];
    const signature = readSignature(`${artifact}.sig`).trim();
    if (!signature) throw new Error(`Empty signature for ${platform}`);
    platforms[platform] = {
      signature,
      url: `https://github.com/${repository}/releases/download/${tag}/${releaseAssetName(artifact)}`,
    };
  }
  return { version, platforms };
}

function walk(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    return entry.isDirectory() ? walk(path) : [path];
  });
}

export function validateVersions(root, tag) {
  const version = JSON.parse(readFileSync(join(root, "package.json"), "utf8")).version;
  const versions = [
    JSON.parse(readFileSync(join(root, "frontend/package.json"), "utf8")).version,
    JSON.parse(readFileSync(join(root, "src-tauri/tauri.conf.json"), "utf8")).version,
    readFileSync(join(root, "src-tauri/Cargo.toml"), "utf8").match(/^version = "([^"]+)"/m)?.[1],
  ];
  if (versions.some((value) => value !== version) || tag !== `v${version}`) {
    throw new Error("Release tag and all four application versions must match.");
  }
  return version;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const [tag, input, output, notesFile] = process.argv.slice(2);
  const version = validateVersions(process.cwd(), tag);
  if (input && output) {
    const files = walk(input);
    const manifest = createManifest(version, tag, process.env.GITHUB_REPOSITORY, files, (path) =>
      readFileSync(path, "utf8"),
    );
    mkdirSync(output, { recursive: true });
    if (notesFile) manifest.notes = readFileSync(notesFile, "utf8");
    for (const platform of Object.values(manifest.platforms)) {
      const name = decodeURIComponent(new URL(platform.url).pathname.split("/").at(-1));
      const source = files.find((file) => releaseAssetName(file) === name);
      cpSync(source, join(output, name));
      cpSync(`${source}.sig`, join(output, `${name}.sig`));
    }
    // Include the macOS first-install DMG alongside the updater archive.
    for (const file of files.filter((path) => path.endsWith(".dmg")))
      cpSync(file, join(output, releaseAssetName(file)));
    writeFileSync(join(output, "latest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
  }
}
