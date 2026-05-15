# Incident Runbook: Checkout API 5xx Triage

## Metadata
- **Runbook ID:** checkout-api-5xx-triage
- **Service/System:** Checkout API
- **Incident Type:** outage
- **Severity Range:** SEV-1
- **Owner Team:** Payments Platform
- **Primary On-Call Role:** backend engineer
- **Last Updated:** 2026-04-19

## Purpose
Use this runbook when checkout or order submission endpoints return HTTP 500 errors. It helps the on-call responder verify the issue, identify whether a recent deploy caused the outage, and decide when to roll back or escalate.

---

## Trigger Conditions
List the alerts, symptoms, or thresholds that should cause this runbook to be used.

- Alert: Checkout API 5xx rate elevated
- Condition: HTTP 500 rate > 2% for 5 minutes
- Symptom: users cannot complete checkout or order submission

---

## Possible Impact
Describe the user or business impact.

- users cannot place orders
- API requests are failing with 500 responses
- revenue impact due to blocked purchases

---

## Required Access / Tools
List the dashboards, logs, systems, scripts, and permissions needed.

- Dashboard: Checkout API Overview
- Logs: Checkout API structured logs
- Traces: request trace viewer
- Cloud / Infra Access: Kubernetes, service deployment access
- Scripts / Commands: deployment rollback script

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
	kubectl get pods -n checkout
	```
	Expected result:
	```text
	All pods should be Running and Ready
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
kubectl rollout undo deployment/checkout-api -n checkout
```

---

### Mitigation Option B: Database Connection Issue
1. Confirm connection pool exhaustion.
2. Restart affected service if safe.
3. Increase pool or scale service if approved.
4. Monitor connection usage and errors.

Command:
```bash
kubectl rollout restart deployment/checkout-api -n checkout
```

---

### Mitigation Option C: High CPU / Memory
1. Confirm resource saturation.
2. Scale service horizontally.
3. Restart unhealthy instances.
4. Disable heavy feature if needed.

Command:
```bash
kubectl scale deployment/checkout-api -n checkout --replicas=6
```

---

## Escalation
Define when and who to escalate to.

- Escalate if not stabilized within 15 minutes
- Escalate if customer impact is severe
- Escalate if data loss or security risk is suspected

Contacts:
- Senior Engineer: checkout on-call lead
- SRE / Platform: production platform duty engineer
- Database Owner: database operations on-call
- Security Team: incident security contact

---

## Communication

- Incident Channel: #incidents-checkout
- Status Page Update Required: yes
- Update Frequency: every 15 minutes

### Update Template
```text
We are investigating an issue affecting Checkout API. Current symptoms: HTTP 500 errors during order submission. Mitigation in progress. Next update in 15 minutes.
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
- Final Fix: rollback or targeted fix applied
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

- Architecture Diagram: checkout architecture diagram
- Dashboard: Checkout API Overview
- Logs: Checkout API structured logs
- Rollback Guide: deployment rollback guide
- Postmortem Template: postmortem template
- Related Runbooks: database connection issue, API latency spike
