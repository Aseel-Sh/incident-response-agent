import {defineConfig} from '@playwright/test';
import base from './playwright.ai.config';
import { randomUUID } from 'node:crypto';
const server=base.webServer as any;
// Each run must index a fresh store so the batch assertion cannot hit a warm cache.
const fixturePath = `.tmp/e2e-batched/${randomUUID()}`;
export default defineConfig({...base,outputDir:'test-results/batched-rag',reporter:[['list'],['html',{outputFolder:'playwright-report/batched-rag',open:'never'}]],testMatch:/batched-rag\.spec\.ts/,webServer:{...server,env:{...server.env,
  Runbooks__SemanticRetrieval__EmbeddingProvider:'OpenRouter',
  Runbooks__SemanticRetrieval__Endpoint:'http://127.0.0.1:5199/v1',
  Runbooks__SemanticRetrieval__BatchSize:'32',
  Runbooks__SemanticRetrieval__AllowLocalEmbeddingFallback:'false',
  Agent__IncidentAnalysis__AllowLocalAnalysisFallback:'false',
  Runbooks__SemanticRetrieval__KnowledgeBasePath:'e2e/fixtures/ai/knowledge',
  Runbooks__SemanticRetrieval__DatabasePath:`${fixturePath}/rag.sqlite`,
  Runbooks__SemanticRetrieval__SourceRegistryPath:`${fixturePath}/sources.json`,
  Storage__Incidents__IncidentRecordsPath:`${fixturePath}/incidents.json`,
  Storage__Incidents__SessionDatabasePath:`${fixturePath}/sessions.sqlite`
}}});
