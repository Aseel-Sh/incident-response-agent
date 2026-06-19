# Database Latency and Pool Saturation

## Observable signals

- Confirm `request_error_rate` and database query latency from metrics.
- Correlate connection timeout logs using the incident time window.
- Treat pool saturation as a hypothesis until pool utilization is measured.

## Safe actions

1. Inspect database connection-pool utilization and wait time.
2. Compare p95 query latency before and after the incident timestamp.
3. Reduce traffic only after validating saturation; do not restart the database blindly.
