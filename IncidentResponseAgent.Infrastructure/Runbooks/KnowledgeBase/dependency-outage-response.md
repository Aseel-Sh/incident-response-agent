# Incident Runbook: Dependency Outage Response

## Metadata
- **Runbook ID:** dependency-outage-response
- **Service/System:** Downstream Dependency / Core API
- **Incident Type:** outage
- **Severity Range:** SEV-1
- **Owner Team:** Platform Engineering
- **Primary On-Call Role:** sre
- **Last Updated:** 2026-04-19

## Purpose
Use this runbook when a downstream dependency is unavailable or partially degraded and is causing customer-facing failures. It helps determine whether the correct response is failover, circuit breaking, or escalation to the dependency owner.

---

## Trigger Conditions
List the alerts, symptoms, or thresholds that should cause this runbook to be used.

- Alert: downstream service unavailable
- Condition: dependency error rate spikes for 5 minutes
- Symptom: upstream requests fail after calling a dependency

---

## Possible Impact
Describe the user or business impact.

- users cannot complete key flows
- requests are timing out or failing
- partial outage in dependent services

---

## Required Access / Tools
List the dashboards, logs, systems, scripts, and permissions needed.

- Dashboard: dependency health dashboard
- Logs: upstream and downstream service logs
- Traces: distributed tracing tool
- Cloud / Infra Access: service mesh / gateway / Kubernetes
- Scripts / Commands: failover and rollback tools

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
   kubectl get pods -n core
   ```
   Expected result:
   ```text
   Application pods should be Running and Ready
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
kubectl rollout undo deployment/core-api -n core
```

---

### Mitigation Option B: Database Connection Issue
1. Confirm connection pool exhaustion.
2. Restart affected service if safe.
3. Increase pool or scale service if approved.
4. Monitor connection usage and errors.

Command:
```bash
kubectl rollout restart deployment/core-api -n core
```

---

### Mitigation Option C: High CPU / Memory
1. Confirm resource saturation.
2. Scale service horizontally.
3. Restart unhealthy instances.
4. Disable heavy feature if needed.

Command:
```bash
kubectl scale deployment/core-api -n core --replicas=6
```

---

## Escalation
Define when and who to escalate to.

- Escalate if not stabilized within 15 minutes
- Escalate if customer impact is severe
- Escalate if data loss or security risk is suspected

Contacts:
- Senior Engineer: platform lead
- SRE / Platform: production platform duty engineer
- Database Owner: database operations on-call
- Security Team: incident security contact

---

## Communication

- Incident Channel: #incidents-platform
- Status Page Update Required: yes
- Update Frequency: every 15 minutes

### Update Template
```text
We are investigating an issue affecting a downstream dependency. Current symptoms: dependency outage and failing upstream requests. Mitigation in progress. Next update in 15 minutes.
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
- Final Fix: failover, rollback, or dependency recovery applied
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

- Architecture Diagram: dependency architecture diagram
- Dashboard: dependency health dashboard
- Logs: upstream and downstream service logs
- Rollback Guide: deployment rollback guide
- Postmortem Template: postmortem template
- Related Runbooks: checkout 5xx, auth failures, queue backlog
