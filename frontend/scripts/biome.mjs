import { spawnSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

function git(root, args) {
  const result = spawnSync("git", args, { cwd: root, maxBuffer: 16 * 1024 * 1024 });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(result.stderr.toString());
  return result.stdout;
}

export function normalizeCheckoutLineEndings(root) {
  const files = git(root, ["ls-files", "-z", "--", "frontend", "scripts"])
    .toString()
    .split("\0")
    .filter((file) => /\.(?:[cm]?js|jsx|ts|tsx|css|html)$/.test(file));
  let count = 0;
  for (const file of files) {
    const path = resolve(root, file);
    let current;
    try {
      current = readFileSync(path);
    } catch (error) {
      if (error.code === "ENOENT") continue;
      throw error;
    }
    if (!current.includes("\r\n")) continue;
    const normalized = Buffer.from(current.toString().replace(/\r\n/g, "\n"));
    // Only repair checkout differences. Preserve real edits and staged content.
    if (!normalized.equals(git(root, ["show", `:${file}`]))) continue;
    writeFileSync(path, normalized);
    count++;
  }
  return count;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2);
  if (["check", "format", "lint", "ci"].includes(args[0])) {
    const root = fileURLToPath(new URL("../../", import.meta.url));
    const count = normalizeCheckoutLineEndings(root);
    if (count > 0) console.log(`Normalized LF checkout for ${count} unchanged files.`);
  }
  const binary = fileURLToPath(
    new URL("../node_modules/@biomejs/biome/bin/biome", import.meta.url),
  );
  const result = spawnSync(process.execPath, [binary, ...args], { stdio: "inherit" });
  if (result.error) throw result.error;
  process.exit(result.status ?? 1);
}
