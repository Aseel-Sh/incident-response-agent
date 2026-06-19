import { defineConfig } from '@playwright/test';
import aiConfig from './playwright.ai.config';

export default defineConfig({
  ...aiConfig,
  testMatch: /learning-loop\.spec\.ts/,
  outputDir: 'test-results/learning',
  reporter: [['list'], ['html', { outputFolder: 'playwright-report/learning', open: 'never' }]]
});
