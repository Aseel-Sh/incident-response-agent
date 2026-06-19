import { spawn } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';

const pidFile = '.tmp/e2e-data/server.pid';

const child = spawn(
  'dotnet',
  ['.tmp/e2e-app/IncidentResponseAgent.Api.dll', '--contentRoot', '.'],
  // Do not let the child inherit Playwright/npm output handles. On Windows an
  // orphaned dotnet handle can otherwise keep the completed test command alive.
  { stdio: 'ignore', windowsHide: true }
);
mkdirSync('.tmp/e2e-data', { recursive: true });
writeFileSync(pidFile, String(child.pid), 'utf8');

let stopping = false;
function stop() {
  if (stopping) return;
  stopping = true;
  if (child.exitCode === null) child.kill();
	try { rmSync(pidFile, { force: true }); } catch { }
  setTimeout(() => process.exit(0), 750).unref();
}

process.on('SIGINT', stop);
process.on('SIGTERM', stop);
child.on('exit', code => process.exit(stopping ? 0 : (code ?? 1)));
