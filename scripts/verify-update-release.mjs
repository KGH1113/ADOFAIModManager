import { readFileSync } from "node:fs";

const [manifestPath, releasePath] = process.argv.slice(2);
const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
const release = JSON.parse(readFileSync(releasePath, "utf8"));
if (release.tag_name !== `v${manifest.version}`) throw new Error("Release version mismatch");
for (const [platform, update] of Object.entries(manifest.platforms)) {
  const asset = release.assets.find((item) => item.browser_download_url === update.url);
  if (!asset || asset.state !== "uploaded" || asset.size === 0) {
    throw new Error(`Update URL does not match an uploaded asset: ${platform} ${update.url}`);
  }
  if (!release.assets.some((item) => item.name === `${asset.name}.sig` && item.size > 0)) {
    throw new Error(`Missing signature asset: ${platform}`);
  }
}
console.log(`Verified ${Object.keys(manifest.platforms).length} uploaded update URLs.`);
