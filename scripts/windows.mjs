import { spawn, spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const command = process.argv[2] ?? 'run';
const file = resolve(root, 'scripts', command === 'run' ? 'run-windows.ps1' : 'build-windows.ps1');
let scriptPath = file;
if (process.platform !== 'win32') {
  if (!process.env.WSL_DISTRO_NAME) {
    process.stderr.write('La construction et le lancement de Jerry nécessitent Windows, directement ou via WSL.\n');
    process.exit(1);
  }
  const converted = spawnSync('wslpath', ['-w', file], { encoding: 'utf8' });
  if (converted.status !== 0) throw new Error(converted.stderr || 'Chemin Windows introuvable.');
  scriptPath = converted.stdout.trim();
}
const args = ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', scriptPath];
if (command === 'test') args.push('-TestOnly');
args.push(...process.argv.slice(3));
const child = spawn('powershell.exe', args, { stdio: 'inherit', cwd: root });
child.on('error', error => { process.stderr.write(error.message + '\n'); process.exitCode = 1; });
child.on('exit', code => { process.exitCode = code ?? 1; });
