# Chapter 7, Step 6: Container deployment record

> **Note (Step 7):** This record describes the Step 6 image `risk-inference:chapter-06-v1`. In Step 7 the `/health` endpoint was replaced by `/health/live` and `/health/ready`. The final image is `risk-inference:chapter-07`. See `docs/dmls/chapter-07.md` for the final results.

This record shows how the risk inference service was packaged and checked in a Docker container.
The measurements come from one machine. They show that the container works. They are not a capacity statement.

## Deployment details

| Item | Value |
|---|---|
| Image tag | `risk-inference:chapter-06-v1` |
| Image ID | `sha256:eb27783beaf961c58b678d10f115566afcb8615d487c5ecb3c4fa2d2a3f9c043` |
| Image size | 398,864,456 bytes (about 380 MiB) |
| Platform | `linux/amd64`, Ubuntu 24.04 base image |
| Base images | `mcr.microsoft.com/dotnet/sdk:10.0` (build), `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime) |
| Runtime in the container | .NET 10.0.15 (`Microsoft.NETCore.App` and `Microsoft.AspNetCore.App`) |
| SDK in the build stage | 10.0.404 |
| Local development SDK | 10.0.401 |
| Docker engine | 29.8.0 |
| Container user | `app` (UID 1654), not root |
| Port | 8080 (`ASPNETCORE_HTTP_PORTS`) |
| Model file | `Models/chapter-06-risk-classifier.zip` |
| Model content SHA-256 | `0fd45f32162ab491340192b4023e1990a3d13935cd050f7b133745e8e309b571` |
| Model version | `chapter-06-v1` |
| Decision policy version | `risk-policy-v1` |
| Feature schema version | `risk-features-v1` |
| High-risk threshold | `0.1121373102068901` |
| Inference mode | `Pooled` |

The container shows these values in its start-up log line and in `GET /health`.

## What is in the image

- The published API and the `RiskClassificationLab.dll` assembly (the Chapter 6 types and the custom feature mapping).
- `/app/Models/chapter-06-risk-classifier.zip`, the saved ML.NET model with its normalization and feature engineering.
- `/app/Models/chapter-06-risk-classifier.manifest.json`, with the hash, the threshold and the policy and feature-schema versions.
- `/app/Models/chapter-06-risk-classifier.policy.json`, the Chapter 6 policy file. It lists the 10 model features.

The service finds the manifest from its content root (`/app`). No path in the image depends on the Windows host.

## Build and run

Build from the repository root, because the image needs the `Models` folder:

```
docker build -f src/RiskInference.Api/Dockerfile -t risk-inference:chapter-06-v1 .
docker run --rm -p 8080:8080 risk-inference:chapter-06-v1
```

To use another model, mount a folder with a manifest and a model, and set
`RiskModel__ManifestPath` to the manifest path in the container.

## Validation results

| Check | Result |
|---|---|
| Build image | Pass. The first full build took 61 s with a failure, then 25 s after the fix (see issue 2). |
| Container starts, model loads, integrity checks pass | Pass. The log shows model, policy, feature-schema version, threshold and hash. |
| `GET /health` | Pass. HTTP 200 with the versions. |
| Invalid request | Pass. HTTP 400 for bad values, bad JSON and an empty body. The body has no exception text. |
| Bad artifacts stop the start-up | Pass for six cases (below). Each exits with code 1 and never listens. |
| Restart the container | Pass. 2,000 answers before and after the restart are identical, bit for bit. |
| Concurrent requests | Pass. 32 workers, 194,000 responses, 0 mismatches. |
| Stop the container | Pass. `docker stop` took 1.1 and 1.6 s. Exit code 0. The log shows "Application is shutting down...". |

### Bad artifacts

Each case ran in a container with a changed copy of the artifact. Each one exited with code 1,
printed `STARTUP FAILED` with the reason, and did not listen on the port.

| Case | Reason in the message |
|---|---|
| Model file is not a zip | `End of Central Directory record could not be found` |
| Hash in the manifest is wrong | `content SHA-256 0fd45f32… does not match the frozen deadbeef…` |
| Threshold of 1.5 | `threshold must be greater than 0 and less than 1. Found 1.5` |
| Model file is missing | `Could not find file '/tmp/bad/chapter-06-risk-classifier.zip'` |
| Decision policy version `risk-policy-v9` | `decisionPolicyVersion 'risk-policy-v9' is not supported` |
| Manifest file is missing | `The model manifest '/tmp/bad/manifest.json' does not exist` |

### Start-up time

Time from the start of `docker run` until the first `GET /health` returned 200. Each cold start used a new container from the same image.

| Run | Time |
|---|---|
| Cold start 1 | 1,092 ms |
| Cold start 2 | 1,749 ms |
| Cold start 3 | 1,837 ms |
| `docker restart` (includes the stop) | 1,906 ms |

These times include the Docker start and a poll every 50 ms. The in-process model load took 238 to 305 ms in earlier benchmarks.

## Inference consistency: Chapter 6, local API, Docker API

The check used 2,000 fixed Chapter 6 samples: every 5th record of `train-5000.csv` (1,000 records) and all 1,000 records of `test-1000.csv`.
The test records were only scored. Nothing was tuned with them.

The reference is the Chapter 6 pipeline: the frozen model, loaded with the Chapter 6 `FinalModelStore`, one batch `Transform`, and the High score read by slot name.
Tool: `dotnet run -c Release --project src/RiskInference.Benchmark -- verify <url>`.

| Target | Responses compared | Mismatches | Largest score difference |
|---|---|---|---|
| Local API (Windows, Release) | 194,000 | 0 | 0 |
| Docker API (Linux container) | 194,000 | 0 | 0 |

- Labels and threshold decisions match the reference exactly. Of the 141 flagged samples, 63 were predicted Low or Medium.
- The local API and the Docker API gave identical results for all 2,000 samples: labels, scores and flags, bit for bit.
- The score tolerance of the check is 1e-5. The measured difference was 0.

## Stop under load

50 clients sent requests without a pause. After 4 s, `docker stop` ran.

| After the stop began | Count |
|---|---|
| HTTP 200 | 627 |
| HTTP errors (for example 503) | 0 |
| Answers cut in the middle (`IncompleteRead`, `BadStatusLine`) | 0 |
| Idle keep-alive connections that the server closed | 34 |
| New connections closed at once (Docker port proxy) | 411 |
| New connections refused | 200 |

No error response and no cut answer appeared. The failed connections are connections that the server or the Docker port proxy closed.
This test cannot prove that no request was lost on a connection that closed without a response.
The test `Requests_in_progress_finish_when_the_host_stops_and_new_requests_are_refused` checks that case directly.
It uses slow requests on a real port, and all 10 requests in progress finished with a correct answer.

## Issues found during Step 6

1. **The Docker daemon was not running.** I started Docker Desktop. This is a local setup issue.
2. **Publish failed with NETSDK1152.** `RiskClassificationLab` is a web project, so its `appsettings.json` flowed into the API publish folder and collided with the API file.
   The publish also took the Lab `appsettings.Development.json`, its apphost, its runtime config and its `web.config`.
   Fix: a target in `RiskInference.Api.csproj` publishes only the Lab assembly and its symbols.
3. **A bad artifact crashed the process.** The unhandled exception ended the container with exit code 139 and a stack trace.
   Fix: `Program.cs` now prints `STARTUP FAILED: <reason>` and exits with code 1.
4. **My first bad-artifact test used the wrong path.** Git Bash changed `/tmp/bad/...` into a Windows path.
   The service still refused to start, but not for the reason under test. I repeated the test with path conversion off.

## Open points

- **No `HEALTHCHECK` in the image.** The ASP.NET runtime image has no `curl`. Use the orchestrator probe on `GET /health`.
- **Stop timeout.** The app waits up to 30 s (`RiskModel__ShutdownTimeoutSeconds`) for requests in progress. Docker stops a container after 10 s by default.
  If requests can last longer than 10 s, run with `docker stop --stop-timeout 35` or the matching setting in the orchestrator.
  Requests in this service last about 1 ms.
- **Linux native libraries.** The model loaded and scored on Linux with no extra packages. LightGBM is not used by the service.
- **One machine, one run.** All results come from one Windows machine with Docker Desktop. I did not test other CPU types.
