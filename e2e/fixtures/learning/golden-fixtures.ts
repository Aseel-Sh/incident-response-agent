export const learningFixtures = {
  approvedRecurring: {
    title: 'Recurring database pool saturation - approved learning source',
    description: 'database-api requests are timing out while waiting for the checkout database connection pool.',
    severity: 'sev2', serviceName: 'database-api', environment: 'production', tags: ['database', 'pool', 'recurring'],
    expected: { priorOutcomesAffectRecommendations: true, approvedKnowledgeReused: true, preserveFollowUpContext: true }
  },
  futureRecurring: {
    title: 'Recurring database pool saturation - new occurrence',
    description: 'database-api checkout requests again show connection-pool wait time and query latency.',
    severity: 'sev2', serviceName: 'database-api', environment: 'production', tags: ['database', 'pool', 'recurring'],
    expected: { priorOutcomesAffectRecommendations: true, approvedKnowledgeReused: true }
  },
  rejectedUpdate: {
    title: 'Recurring database pool saturation - rejected learning',
    description: 'database-api pool wait warning proposed untrusted remediation.',
    severity: 'sev2', serviceName: 'database-api', environment: 'production', tags: ['database', 'pool', 'rejected'],
    expected: { rejectedUpdatesExcluded: true }
  },
  deletedIncident: {
    title: 'Recurring database pool saturation - deleted source',
    description: 'database-api pool saturation record that will be deleted.',
    severity: 'sev2', serviceName: 'database-api', environment: 'production', tags: ['database', 'pool', 'deleted'],
    expected: { deletedIncidentsExcluded: true }
  },
  falsePositive: {
    title: 'False positive database pool warning', description: 'A warning with no verified impact.',
    severity: 'sev4', serviceName: 'database-api', environment: 'production', tags: ['false-positive'],
    expected: { falsePositivesExcluded: true }
  },
  ignoredCandidate: {
    title: 'Ignored database pool warning', description: 'An ignored warning with no verified impact.',
    severity: 'sev4', serviceName: 'database-api', environment: 'production', tags: ['ignored'],
    expected: { ignoredCandidatesExcluded: true }
  },
  followUp: {
    title: 'Follow-up recurring database pool saturation',
    description: 'Validate the latest mitigation while preserving the original database evidence and action outcomes.',
    severity: 'sev3', serviceName: 'database-api', environment: 'production', tags: ['database', 'pool', 'follow-up'],
    expected: { preserveFollowUpContext: true, priorOutcomesAffectRecommendations: true }
  }
} as const;

export const feedbackCases = [
  { usefulness: 'Useful', correctness: 'Correct', reasons: ['shallow', 'missing evidence', 'hallucinated evidence'] },
  { usefulness: 'Partially Useful', correctness: 'Partially Correct', reasons: ['wrong SEV', 'wrong root cause', 'bad remediation'] },
  { usefulness: 'Not Useful', correctness: 'Wrong', reasons: ['ignored runbook', 'repeated failed past action', 'other'] }
] as const;
