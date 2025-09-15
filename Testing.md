# Testing

## Unit Scenarios
- Validation rejects missing/unsupported executor (Permanent failure).
- Retry: transient → success (attempts=2). Retry: timeout → final timeout (attempts>=1).
- Envelope contains attempts with classifications.

## Integration
- In‑process echo target + HTTP executor e2e.

## Test Matrix

| Scenario                       | Executor   | Expected Outcome | Transient? | Retries Used | Assertions |
|--------------------------------|------------|------------------|------------|--------------|------------|
| Valid GET proxied              | http       | Success          | N/A        | 0            | 200, status=Success, attempts=1 |
| 500 then 200                   | http       | Success          | Yes        | 1            | attempts=2, first=TransientFailure |
| Timeout per‑attempt            | http       | TimedOut         | Yes        | >=1          | final outcome Timeout |
| Unsupported executor           | n/a        | ValidationError  | No         | 0            | error message present |
| PS allowlist miss              | powershell | Failed           | No         | 0            | error contains 'not allowed' |
