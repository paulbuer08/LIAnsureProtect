# Milestone 42 — Documents To S3 (Design)

## One-sentence goal

Give the existing `IDocumentStorageService` port a **second adapter that talks to Amazon S3**,
selected when `Platform:Profile=Aws`, so private evidence documents can live in S3 — **without
changing any business flow, endpoint, database table, or the frontend**, and **without needing a
real AWS account** (we develop and test against LocalStack, a local S3 emulator).

## Why this milestone exists (simple English)

Today, when a customer uploads an evidence document, the bytes are written to a folder on the
server's disk by `LocalDocumentStorageService`. That is fine locally but not for real cloud
hosting: cloud servers are disposable (a new container has none of the old files), files must be
encrypted and access-controlled, and many servers must share one store.

**S3** (Simple Storage Service) is AWS's managed object store — think "an infinite, encrypted,
private filing cabinet reachable over the network". This milestone writes the adapter that puts
documents into S3 and reads them back, exactly matching the port the rest of the app already uses.

> **Analogy:** the app has always plugged its documents into a **standard wall socket**
> (`IDocumentStorageService`). Until now the only plug was a local filing cabinet. M42 adds an
> S3 plug. The appliances (upload, scan, download, review gates) don't change — only what's
> behind the socket.

## The one design rule that makes this safe

The `IDocumentStorageService` port already exists and is used everywhere documents are stored,
scanned, or downloaded (see the evidence flow in `docs/encyclopedia/08-flow-underwriting.md`).
**We change nothing about the port or its callers.** We only add a new class that implements it
and register that class under the `Aws` profile. This is the Ports & Adapters pattern paying off:
the blast radius is one new file + one registration branch.

## No AWS account needed: LocalStack

**LocalStack** is a Docker container that emulates AWS APIs (including S3) on `localhost`. The
same AWS SDK code that talks to real S3 talks to LocalStack when we point the client's
`ServiceURL` at `http://localhost:4566` and use path-style addressing. So:

- Day-to-day development and the round-trip integration test run against LocalStack — **free, no
  cloud, no bill**.
- The *identical* adapter code runs against real S3 in Phase 2 (M46+) by changing configuration
  only (drop the `ServiceUrl`, set a region + real credentials/instance role + KMS key).

## What gets built

### 1. `AWSSDK.S3` package
Added via central package management (`Directory.Packages.props`). It is the official AWS SDK
S3 client (`IAmazonS3`).

### 2. Extended `DocumentStorageOptions`
A new optional `S3` sub-section:

| Setting | Meaning |
|---|---|
| `BucketName` | The private S3 bucket documents live in (**required** when `Aws`). |
| `ServiceUrl` | LocalStack endpoint (`http://localhost:4566`); empty in real AWS. |
| `ForcePathStyle` | `true` for LocalStack; real AWS uses virtual-host style. |
| `Region` | AWS region (e.g. `us-east-1`) when not using `ServiceUrl`. |
| `KmsKeyId` | Optional KMS key → server-side encryption (SSE-KMS) on every upload. |
| `AccessKeyId` / `SecretAccessKey` | LocalStack dummy creds; **empty in real AWS** so the default credential chain (instance role) is used — no static keys in the cloud. |

### 3. `S3DocumentStorageService : IDocumentStorageService`
- `StoreAsync` → `PutObjectRequest` with key `evidence-documents/{guid}{ext}`, the upload's
  content type, and — when `KmsKeyId` is set — SSE-KMS. Returns the storage key (same key shape
  the local adapter already produces, so stored metadata is compatible).
- `OpenReadAsync` → `GetObjectRequest`; returns the object stream + content type, or **`null`
  when the object is missing** (`AmazonS3Exception` with `NotFound`) — matching the local
  adapter's "missing file → null" contract so the download/scan gates behave identically.

### 4. Composition-root wiring (`AddInfrastructure`)
The `Aws` branch (which used to `throw "arrives in Milestone 42"`) now:
- registers `IAmazonS3` from the options (LocalStack vs real AWS decided by `ServiceUrl`),
- **fails fast** with a clear message if `BucketName` is missing (never silently mis-wire),
- registers `S3DocumentStorageService` as the `IDocumentStorageService`.

The `Local` branch is unchanged.

## How this is tested (and why CI stays green)

1. **Adapter unit tests** (run in normal `dotnet test`, no Docker): a mocked `IAmazonS3` proves
   *our* logic — the key prefix, content type, SSE-KMS applied only when configured, download
   mapping, and missing-object → null. These test the request we build and the response we map,
   not the SDK.
2. **Updated `PlatformProfileSwitchTests`**: the `Aws` profile now wires `S3DocumentStorageService`
   (given S3 config), and a missing bucket still **fails fast**.
3. **Opt-in LocalStack round-trip test** (env-gated exactly like the existing PostgreSQL opt-in
   test): stores real bytes and reads them back through a LocalStack container. It is **skipped by
   default**, so the required CI job stays green; it runs where LocalStack is available (local CI /
   manual). A profile-scoped LocalStack service is added to `docker-compose.yml`.

## Explicitly out of scope (later milestones)

- **Presigned "Valet Key" download URLs** (browser downloads straight from S3) — prepared for but
  not switched on; API streaming stays the default until CloudFront exists (M47). Documented as a
  follow-up.
- **Provisioning real S3 buckets, KMS keys, IAM** — that is Terraform in Phase 2 (M45/M46).
- **S3-triggered Lambda malware scanning** — the scanner port stays local; the async scan
  pipeline is a later milestone. Clean-only download/accept gates are unchanged.
- **Object Lock / lifecycle / legal hold** — bucket-level concerns provisioned in Phase 2.

## Acceptance criteria

- All existing evidence tests pass unchanged (the flow is untouched).
- New adapter unit tests + updated switch tests pass in normal CI.
- The opt-in LocalStack round-trip test passes where LocalStack runs.
- `docs/encyclopedia/` Chapters 2, 3, and 8 updated to show the S3 adapter as a live option.
- Learnings, CHANGELOG, project-status, and roadmap status updated.
