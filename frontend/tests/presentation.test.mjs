import { test } from 'node:test';
import assert from 'node:assert/strict';
import { displayIdentity } from '../src/identity.js';
import { diagnosticRows, diagnosticText, vectorStoreLocation } from '../src/diagnostics.js';
import { incidentAnalytics } from '../src/analytics.js';

test('stable and historical identity resolve to the same human name', () => {
  const directory = [{ id: 'auth0|123', name: 'Aseel', auditName: 'aseel@example.com' }];
  assert.equal(displayIdentity('auth0|123', directory), 'Aseel');
  assert.equal(displayIdentity('aseel@example.com', directory), 'Aseel');
  assert.equal(displayIdentity('unmapped@example.com'), 'Responder');
  assert.equal(displayIdentity(null), 'Unassigned');
});
test('diagnostics preserve captured zero duration and reject meaningless values', () => {
  for (const value of ['?', 'undefined', 'null', '[object Object]', {}, null]) assert.equal(diagnosticText(value), 'Not available');
  assert.deepEqual(diagnosticRows({ modelDurationMilliseconds: 0, ragDurationMilliseconds: null, fallbackStage: 'after_model_response_validation' }), [['Model execution', '0 ms'], ['Fallback stage', 'Response validation']]);
  assert.deepEqual(diagnosticRows({ modelDurationMilliseconds: Infinity }), []);
});

test('vector locations identify the actual remote store or explicit local fallback', () => {
  const result = { vectorStoreProvider: 'qdrant', vectorStoreEndpoint: 'http://localhost:6333', vectorStoreCollection: 'runbooks', databasePath: 'cache.sqlite' };
  assert.equal(vectorStoreLocation(result), 'http://localhost:6333 · runbooks');
  assert.equal(vectorStoreLocation({ ...result, vectorStoreProvider: 'qdrant-unavailable' }), 'http://localhost:6333 · runbooks');
  assert.equal(vectorStoreLocation({ ...result, vectorStoreProvider: 'qdrant-unavailable/sqlite' }), 'cache.sqlite');
});
test('analytics handle empty data without inventing dates or counts', () => {
  assert.deepEqual(incidentAnalytics([]), { severity: [], status: [], days: [], missingTimestamp: 0 });
});
test('analytics count saved incidents, distinct lifecycle states and UTC creation dates', () => {
  const result = incidentAnalytics([
    { severity: 'Sev2', status: 'mitigated', createdAtUtc: '2026-09-15T23:00:00-05:00' },
    { severity: 'SEV-2', status: 'resolved', createdAtUtc: '2026-09-16T12:00:00Z' },
    { severity: 'Sev5', status: 'new', createdAtUtc: 'invalid' }
  ]);
  assert.deepEqual(result.days, [['2026-09-16', 2]]);
  assert.deepEqual(result.severity, [['sev2', 2], ['sev5', 1]]);
  assert.deepEqual(result.status, [['mitigated', 1], ['new', 1], ['resolved', 1]]);
  assert.equal(result.missingTimestamp, 1);
});

