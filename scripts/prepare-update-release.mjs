import { cpSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { basename, join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

// Updater platform -> the release build that produces it (downloaded into
// `update-<build>/`) and its update file. Both macOS builds produce an
// identically named .app.tar.gz, so `arch` keeps their release assets apart.
const targets = {
  "darwin-aarch64": { build: "aarch64-apple-darwin", suffix: ".app.tar.gz", arch: "aarch64" },
  "darwin-x86_64": { build: "x86_64-apple-darwin", suffix: ".app.tar.gz", arch: "x64" },
  "windows-x86_64": { build: "x86_64-pc-windows-msvc", suffix: ".exe" },
  "linux-x86_64": { build: "x86_64-unknown-linux-gnu", suffix: ".AppImage" },
};

// GitHub normalizes spaces and special characters in uploaded asset names.
// Normalize before copying and constructing URLs so the manifest matches exactly.
export function releaseAssetName(path) {
  return basename(path).replace(/[^A-Za-z0-9._-]/g, ".");
}

export function selectArtifacts(files) {
  return Object.entries(targets).map(([platform, { build, suffix, arch }]) => {
    const matches = files.filter(
      (file) =>
        file.endsWith(suffix) &&
        files.includes(`${file}.sig`) &&
        file.split(/[\\/]/).includes(`update-${build}`),
    );
    if (matches.length !== 1) throw new Error(`Expected one signed artifact for ${platform}`);
    const name = releaseAssetName(matches[0]);
    const asset = arch ? `${name.slice(0, -suffix.length)}_${arch}${suffix}` : name;
    return { platform, source: matches[0], asset };
  });
}

// This checks version metadata; the updater also verifies the cryptographic signature.
export function validateSignedVersion(signature, version, platform) {
  const comment = Buffer.from(signature, "base64").toString("utf8").split(/\r?\n/)[2];
  const signedVersion = comment?.startsWith("trusted comment: ")
    ? comment
        .slice("trusted comment: ".length)
        .split("\t")
        .find((field) => field.startsWith("version:"))
        ?.slice("version:".length)
    : undefined;
  if (signedVersion !== version) {
    throw new Error(`Missing or mismatched signed version for ${platform}`);
  }
}

export function createManifest(version, tag, repository, files, readSignature) {
  if (!/^\d+\.\d+\.\d+$/.test(version) || tag !== `v${version}`) {
    throw new Error("Release tag must match the stable application version (vX.Y.Z).");
  }
  if (!/^[\w.-]+\/[\w.-]+$/.test(repository)) throw new Error("Invalid repository");
  const platforms = {};
  for (const { platform, source, asset } of selectArtifacts(files)) {
    const signature = readSignature(`${source}.sig`).trim();
    if (!signature) throw new Error(`Empty signature for ${platform}`);
    validateSignedVersion(signature, version, platform);
    platforms[platform] = {
      signature,
      url: `https://github.com/${repository}/releases/download/${tag}/${asset}`,
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
    // Signatures ship inside latest.json; the updater never downloads .sig files.
    for (const { source, asset } of selectArtifacts(files)) cpSync(source, join(output, asset));
    // Include the macOS first-install DMGs (named per architecture) alongside the updater archives.
    for (const file of files.filter((path) => path.endsWith(".dmg")))
      cpSync(file, join(output, releaseAssetName(file)));
    writeFileSync(join(output, "latest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
  }
}
