import { spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const [mode = 'dev', ...args] = process.argv.slice(2);
const targetIndex = args.indexOf('--target');
if (targetIndex >= 0) process.env.AMM_TARGET = args[targetIndex + 1];
for (const task of [['scripts/prepare-desktop.mjs'], ['run', 'tauri', mode, ...args]]) {
  const result = spawnSync(process.execPath, task, { cwd: root, stdio: 'inherit', env: process.env });
  if (result.status !== 0) process.exit(result.status ?? 1);
}
if (mode === 'build' && process.platform === 'darwin') {
  const profile = args.includes('--debug') ? 'debug' : 'release';
  const base = process.env.AMM_TARGET ? join('src-tauri/target', process.env.AMM_TARGET) : 'src-tauri/target';
  const app = join(root, base, profile, 'bundle/macos/ADOFAI Mod Manager.app');
  if (existsSync(app)) {
    mkdirSync(join(root, 'build'), { recursive: true });
    const destination = join(root, 'build/ADOFAI Mod Manager.app');
    rmSync(destination, { recursive: true, force: true });
    cpSync(app, destination, { recursive: true });
    console.log(destination);
  }
}
