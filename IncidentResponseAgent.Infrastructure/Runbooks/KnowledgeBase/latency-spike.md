# Incident Runbook: API Latency Spike Investigation

## Metadata
- **Runbook ID:** api-latency-spike-investigation
- **Service/System:** Core API
- **Incident Type:** latency
- **Severity Range:** SEV-2
- **Owner Team:** Platform Engineering
- **Primary On-Call Role:** sre
- **Last Updated:** 2026-04-19

## Purpose
Use this runbook when request latency rises while the service is still responding. It helps identify whether the issue is caused by resource saturation, a dependency slowdown, or a recent change.

---

## Trigger Conditions
List the alerts, symptoms, or thresholds that should cause this runbook to be used.

- Alert: API latency elevated
- Condition: p95 latency > 2s for 5 minutes
- Symptom: users experiencing slow responses or timeouts

---

## Possible Impact
Describe the user or business impact.

- API requests are timing out
- user experience is degraded
- downstream services receive delayed requests

---

## Required Access / Tools
List the dashboards, logs, systems, scripts, and permissions needed.

- Dashboard: API Performance Overview
- Logs: service logs and request traces
- Traces: distributed tracing tool
- Cloud / Infra Access: Kubernetes, scaling controls
- Scripts / Commands: diagnostic scripts and scaling commands

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
	kubectl get pods -n api
	```
	Expected result:
	```text
	Pods should be Running with low restart counts
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
kubectl rollout undo deployment/core-api -n api
```

---

### Mitigation Option B: Database Connection Issue
1. Confirm connection pool exhaustion.
2. Restart affected service if safe.
3. Increase pool or scale service if approved.
4. Monitor connection usage and errors.

Command:
```bash
kubectl rollout restart deployment/core-api -n api
```

---

### Mitigation Option C: High CPU / Memory
1. Confirm resource saturation.
2. Scale service horizontally.
3. Restart unhealthy instances.
4. Disable heavy feature if needed.

Command:
```bash
kubectl scale deployment/core-api -n api --replicas=8
```

---

## Escalation
Define when and who to escalate to.

- Escalate if not stabilized within 30 minutes
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
We are investigating an issue affecting Core API. Current symptoms: elevated latency and slow responses. Mitigation in progress. Next update in 15 minutes.
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
- Final Fix: scale-out or dependency fix applied
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

- Architecture Diagram: core API architecture diagram
- Dashboard: API Performance Overview
- Logs: service logs and request traces
- Rollback Guide: deployment rollback guide
- Postmortem Template: postmortem template
- Related Runbooks: database connection issue, checkout 5xx, dependency outage