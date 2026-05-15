# Incident Runbook: Queue Backlog Growth

## Metadata
- **Runbook ID:** queue-backlog-growth
- **Service/System:** Message Queue / Worker Service
- **Incident Type:** queue backlog
- **Severity Range:** SEV-2
- **Owner Team:** Platform Engineering
- **Primary On-Call Role:** backend engineer
- **Last Updated:** 2026-04-19

## Purpose
Use this runbook when queue depth grows faster than workers can process messages. It helps determine whether the issue is caused by worker failure, downstream slowness, or a change in message volume.

---

## Trigger Conditions
List the alerts, symptoms, or thresholds that should cause this runbook to be used.

- Alert: queue depth elevated
- Condition: backlog remains above threshold for 10 minutes
- Symptom: delayed processing, stale user actions, or retry storms

---

## Possible Impact
Describe the user or business impact.

- customer actions are delayed
- background jobs are not completing on time
- downstream systems receive bursts of retries later

---

## Required Access / Tools
List the dashboards, logs, systems, scripts, and permissions needed.

- Dashboard: queue and worker metrics
- Logs: worker logs and message processor logs
- Traces: distributed trace viewer
- Cloud / Infra Access: queue console, Kubernetes
- Scripts / Commands: worker scale and restart commands

---

## Initial Verification
Confirm the incident is real before taking action.

1. Check whether the alert is still firing.
2. Confirm the issue in dashboards or logs.
3. Identify affected service, endpoint, or dependency.
4. Check whether a recent deploy or config change happened.

---

## Diagnosis Steps
Work through these steps in order.

1. Check service health  
   Command:
   ```bash
   kubectl get pods -n workers
   ```
   Expected result:
   ```text
   Worker pods should be Running and Ready
   ```

2. Check infrastructure resource usage  
   - CPU  
   - Memory  
   - Network  
   - Pod/container restarts  

3. Check dependencies  
   - Database  
   - Cache  
   - Queue  
   - External APIs  

4. Check recent changes  
   - Deployment  
   - Feature flag  
   - Config change  
   - Migration  

---

## Decision Points
Use simple branching logic.

- If recent deployment caused the issue -> go to **Mitigation Option A**
- If database connections are exhausted -> go to **Mitigation Option B**
- If CPU or memory is saturated -> go to **Mitigation Option C**
- If root cause is unclear after checks -> go to **Escalation**

---

## Mitigation Actions

### Mitigation Option A: Roll Back Deployment
1. Identify latest deployment.
2. Roll back to last stable version.
3. Monitor metrics after rollback.

Command:
```bash
kubectl rollout undo deployment/message-worker -n workers
```

---

### Mitigation Option B: Database Connection Issue
1. Confirm connection pool exhaustion.
2. Restart affected service if safe.
3. Increase pool or scale service if approved.
4. Monitor connection usage and errors.

Command:
```bash
kubectl rollout restart deployment/message-worker -n workers
```

---

### Mitigation Option C: High CPU / Memory
1. Confirm resource saturation.
2. Scale service horizontally.
3. Restart unhealthy instances.
4. Disable heavy feature if needed.

Command:
```bash
kubectl scale deployment/message-worker -n workers --replicas=8
```

---

## Escalation
Define when and who to escalate to.

- Escalate if not stabilized within 20 minutes
- Escalate if customer impact is severe
- Escalate if data loss or security risk is suspected

Contacts:
- Senior Engineer: worker service lead
- SRE / Platform: production platform duty engineer
- Database Owner: database operations on-call
- Security Team: incident security contact

---

## Communication

- Incident Channel: #incidents-workers
- Status Page Update Required: yes
- Update Frequency: every 15 minutes

### Update Template
```text
We are investigating an issue affecting message processing. Current symptoms: queue backlog growth and delayed jobs. Mitigation in progress. Next update in 15 minutes.
```

---

## Recovery Validation
Confirm system is stable.

- Alert cleared
- Error rate back to baseline
- Latency normalized
- Health checks passing
- Logs show no new errors
- Core user flow tested successfully

---

## Resolution
Summarize how the incident was resolved.

- Root Cause: placeholder example, replace after the real incident
- Final Fix: worker scale-up or dependency fix applied
- Time Stabilized: 2026-04-19T00:00:00Z

---

## Post-Incident Follow-Up

- [ ] Create incident report / postmortem
- [ ] Add missing alerts if needed
- [ ] Improve dashboards if needed
- [ ] Update runbook with new findings
- [ ] Create engineering tasks for permanent fix

---

## Related Links

- Architecture Diagram: queue architecture diagram
- Dashboard: queue and worker metrics
- Logs: worker logs and message processor logs
- Rollback Guide: deployment rollback guide
- Postmortem Template: postmortem template
- Related Runbooks: database connection exhaustion, auth failures, API latency
