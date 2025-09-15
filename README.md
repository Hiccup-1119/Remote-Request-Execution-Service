# Remote Request Execution (RRE) Service

A production‑grade, extensible service to normalize, validate, execute, and observe remote requests (HTTP + session‑based PowerShell) with resilience.

```
┌ client ──/api/{**path}──────┐
│                             │
│  normalize → validate → orchestrate → retry/timeout → executor(http|ps) │
│                             │
└── envelope + headers + metrics/logs ────────────────────────────────────┘
```

## Design Highlights
- **Unified proxy API**: catch‑all route `/api/{**path}`; health at `/ping`; metrics at `/metrics`.
- **Executors**: `IExecutor` abstraction. `HttpExecutor` forwards method/path/query/filtered headers/body. `PowerShellExecutor` uses an **allowlist** (logical op → cmd) to prevent arbitrary execution and simulates EXO in this sample.
- **Resilience**: custom retry with **exponential backoff + full jitter**; per‑attempt **timeout** via linked CTS. Transient taxonomy: timeouts, connection failures, HTTP `408/429/5xx (≠501/505)`, and known throttling messages.
- **Response envelope**: requestId, correlationId, executorType, UTC timestamps, status, attempt summaries, and executor result (HTTP: status/headers/truncated body; PS: command/stdout/stderr/objects).
- **Trace headers**: `X-RRE-Request-Id`, `X-RRE-Correlation-Id`, `X-RRE-Executor`, `X-RRE-Attempts`. Rationale: consistent grepping across services and log joins.
- **Observability**: JSON console logs; `/metrics` exposes totals + avg/p95 latency (approx. via sliding window of last 1024 latencies).
- **Security**: input size limits (configurable via Kestrel/`MaxRequestBodySize`), header allowlist, token redaction in logs, PS allowlist, explicit session disposal (stubbed here), and auth failure strategy (classify as permanent).
- **Config**: `appsettings.json` + env overrides. Defaults: 3 attempts, 200ms base delay, 2s cap.
- **Container**: multi‑stage Dockerfile → small Alpine runtime.

## How to Run
```bash
# dotnet
cd src/RreService
dotnet run
# Docker
docker build -t rre:dev . && docker run -p 8080:8080 -e INSTANCE_ID=dev rre:dev
```

### Sample Request (HTTP executor via headers)
```bash
curl -s -H 'X-RRE-Executor: http' \
     -H 'X-RRE-BaseUrl: http://localhost:5001' \
     http://localhost:8080/api/echo?hello=world | jq
```

## Trade‑offs & Extensibility
- **No Polly**: custom, minimal retry keeps deps light and shows math explicitly.
- **PS executor**: stubbed to avoid external network; interface ready for real EXO (create→reuse/expire→dispose, per‑tenant key cache).
- **Metrics**: in‑memory sliding window; for production, export Prometheus histogram.
- **Future policies**: add decorators around `IExecutor` or inject a policy chain.

## Testing
- **Unit**: validation, retry (success‑after‑failure + timeout), executor selection, envelope construction.
- **Integration**: end‑to‑end HTTP using in‑process TestServer (no external network).
- **Determinism**: fake clock; no `Task.Delay` sleeps in tests.

## AI Usage
See `AI_USAGE.md`.

## If I had more time…
- Real EXO session mgmt (RunspacePool, per‑tenant keyed cache, idle TTL, auth failure quarantine).
- Circuit breaker + token bucket rate limiting.
- Structured log enrichment (request/response ids) and OpenTelemetry traces.
- Pluggable validation with JSON Schema and problem‑details responses.
