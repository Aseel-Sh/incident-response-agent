import { copyFile, cp, mkdir, rm } from 'node:fs/promises';
import { resolve } from 'node:path';

const dataDirectory = resolve('.tmp/e2e-data');
const appDirectory = resolve('.tmp/e2e-app');
const aiDirectory = resolve('.tmp/e2e-ai');

await rm(dataDirectory, { recursive: true, force: true });
await rm(appDirectory, { recursive: true, force: true });
await rm(aiDirectory, { recursive: true, force: true });
await mkdir(dataDirectory, { recursive: true });
await copyFile(resolve('e2e/fixtures/monitoring/logs.json'), resolve(dataDirectory, 'logs.json'));
await copyFile(resolve('e2e/fixtures/monitoring/metrics.json'), resolve(dataDirectory, 'metrics.json'));
await mkdir(resolve(aiDirectory, 'knowledge'), { recursive: true });
await cp(resolve('e2e/fixtures/ai/knowledge'), resolve(aiDirectory, 'knowledge'), { recursive: true });
await copyFile(resolve('e2e/fixtures/ai/logs.json'), resolve(aiDirectory, 'logs.json'));
await copyFile(resolve('e2e/fixtures/ai/metrics.json'), resolve(aiDirectory, 'metrics.json'));
