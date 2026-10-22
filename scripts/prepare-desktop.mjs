import { spawnSync } from 'node:child_process';
import { cpSync, mkdirSync, readFileSync, chmodSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
function run(command, args) {
  const result = spawnSync(command, args, { cwd: root, stdio: 'inherit' });
  if (result.status !== 0) throw new Error(`${command} failed (${result.status})`);
}
const target = process.env.TAURI_ENV_TARGET_TRIPLE || process.env.AMM_TARGET ||
  spawnSync('rustc', ['--print', 'host-tuple'], { encoding: 'utf8' }).stdout.trim();
const targets = {
  'aarch64-apple-darwin': 'osx-arm64', 'x86_64-apple-darwin': 'osx-x64',
  'x86_64-pc-windows-msvc': 'win-x64', 'aarch64-pc-windows-msvc': 'win-arm64',
  'x86_64-unknown-linux-gnu': 'linux-x64', 'aarch64-unknown-linux-gnu': 'linux-arm64',
};
const rid = targets[target];
if (!rid) throw new Error(`Unsupported desktop target: ${target}`);
const extension = rid.startsWith('win') ? '.exe' : '';
const output = join(root, 'build', `tauri-engine-${rid}`);
run('dotnet', ['publish', 'Engine/NativeUmm.Cli/NativeUmm.Cli.csproj', '-c', 'Release', '-r', rid, '-o', output]);
mkdirSync(join(root, 'src-tauri/binaries'), { recursive: true });
const binary = join(root, `src-tauri/binaries/UMMInstallerEngine-${target}${extension}`);
cpSync(join(output, `UMMInstallerEngine${extension}`), binary);
if (!extension) chmodSync(binary, 0o755);
const payload = join(root, 'src-tauri/resources/UMMPayload');
mkdirSync(payload, { recursive: true });
cpSync(join(root, 'Resources/UMMPayload'), payload, { recursive: true });
if (rid.startsWith('osx')) {
  const harmony = readFileSync(join(payload, '0Harmony.dll'));
  for (const marker of ['MonoMod.Core, Version=1.3.3.0', 'MethodPatcher', 'Arm64Arch', 'exhelper_macos_arm64.dylib']) {
    if (!harmony.includes(Buffer.from(marker))) throw new Error(`Incompatible Harmony payload: ${marker}`);
  }
}
mkdirSync(join(root, 'frontend/public/assets'), { recursive: true });
cpSync(join(root, 'Resources/AppIconMaster.png'), join(root, 'frontend/public/assets/app-icon.png'));
run(process.execPath, ['run', 'tauri', 'icon', 'Resources/AppIconMaster.png', '--output', 'src-tauri/icons']);
console.log(`Desktop resources ready: ${target}`);
