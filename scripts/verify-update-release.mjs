import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";

export function verifyManifest(manifest, release) {
  if (release.tagName !== `v${manifest.version}`) throw new Error("Release version mismatch");
  for (const [platform, update] of Object.entries(manifest.platforms)) {
    const url = new URL(update.url);
    const asset = release.assets.find((item) => item.name === url.pathname.split("/").at(-1));
    if (!asset || asset.state !== "uploaded" || asset.size === 0) {
      throw new Error(`Update URL does not match an uploaded asset: ${platform} ${update.url}`);
    }
    // Draft asset URLs use an untagged-* placeholder until publication. Compare
    // the final tag URL after replacing only that one placeholder path segment.
    const uploaded = new URL(asset.url);
    const parts = uploaded.pathname.split("/");
    if (release.isDraft && parts.at(-2)?.startsWith("untagged-")) {
      parts[parts.length - 2] = release.tagName;
      uploaded.pathname = parts.join("/");
    }
    if (uploaded.href !== url.href) throw new Error(`Incorrect release URL: ${platform}`);
    if (!release.assets.some((item) => item.name === `${asset.name}.sig` && item.size > 0)) {
      throw new Error(`Missing signature asset: ${platform}`);
    }
  }
  return Object.keys(manifest.platforms).length;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const [manifestPath, releasePath] = process.argv.slice(2);
  const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
  const release = JSON.parse(readFileSync(releasePath, "utf8"));
  console.log(`Verified ${verifyManifest(manifest, release)} uploaded update URLs.`);
}
