import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { normalizeCheckoutLineEndings } from "./biome.mjs";

function fixture(t) {
  const root = mkdtempSync(join(tmpdir(), "amm-checkout-"));
  t.after(() => rmSync(root, { recursive: true, force: true }));
  const git = (...args) => {
    const result = spawnSync("git", args, { cwd: root, encoding: "utf8" });
    assert.equal(result.status, 0, result.stderr);
    return result.stdout;
  };
  git("init", "--quiet");
  git("config", "core.autocrlf", "true");
  mkdirSync(join(root, "frontend"));
  const path = join(root, "frontend", "example.ts");
  const source = "export const answer = 42;\n";
  writeFileSync(path, source);
  git("add", ".");
  const commit = () =>
    git(
      "-c",
      "user.name=Test",
      "-c",
      "user.email=test@example.invalid",
      "commit",
      "--quiet",
      "-m",
      "fixture",
    );
  commit();
  // Updating attributes does not rewrite files already checked out as CRLF.
  writeFileSync(path, source.replace(/\n/g, "\r\n"));
  writeFileSync(join(root, ".gitattributes"), "*.ts text eol=lf\n");
  git("add", ".gitattributes");
  commit();
  return { root, path, source, git };
}

test("repairs an existing Windows checkout and leaves Git clean", (t) => {
  const { root, path, source, git } = fixture(t);
  assert.equal(normalizeCheckoutLineEndings(root), 1);
  assert.equal(readFileSync(path, "utf8"), source);
  assert.equal(git("status", "--porcelain"), "");
  assert.equal(normalizeCheckoutLineEndings(root), 0);
});

test("preserves real local edits and untracked files", (t) => {
  const { root, path, source } = fixture(t);
  const edited = `${source}export const extra = 1;\n`.replace(/\n/g, "\r\n");
  const untracked = join(root, "frontend", "untracked.ts");
  writeFileSync(path, edited);
  writeFileSync(untracked, edited);
  assert.equal(normalizeCheckoutLineEndings(root), 0);
  assert.equal(readFileSync(path, "utf8"), edited);
  assert.equal(readFileSync(untracked, "utf8"), edited);
});

test("compares against the index and preserves staged edits", (t) => {
  const { root, path, source, git } = fixture(t);
  const staged = `${source}export const extra = 1;\n`;
  writeFileSync(path, staged);
  git("add", "frontend/example.ts");
  writeFileSync(path, staged.replace(/\n/g, "\r\n"));
  const before = git("diff", "--cached");
  assert.equal(normalizeCheckoutLineEndings(root), 1);
  assert.equal(git("diff", "--cached"), before);
  assert.equal(git("diff"), "");
});
