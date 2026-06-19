import { readFile, rm } from 'node:fs/promises';

export default async function teardownCoreFixture() {
  const pidFile = '.tmp/e2e-data/server.pid';
  try {
    const pid = Number.parseInt(await readFile(pidFile, 'utf8'), 10);
    if (Number.isInteger(pid) && pid > 0) process.kill(pid);
  } catch {
    // The server may already be stopped after an early startup failure.
  } finally {
    await rm(pidFile, { force: true });
  }
}
