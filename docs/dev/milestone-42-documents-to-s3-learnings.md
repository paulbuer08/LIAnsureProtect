# Milestone 42 — Documents To S3 (Learnings)

## What we set out to do

Add an S3 adapter behind the existing `IDocumentStorageService` port so private evidence
documents can live in Amazon S3 under `Platform:Profile=Aws`, **without changing any business
flow, endpoint, EF model, schema, or the frontend**, and **without requiring an AWS account** (we
build and test against LocalStack).

## What shipped

- `AWSSDK.S3` (v4.0.100.1) added via central package management.
- `DocumentStorageOptions` extended with an optional `S3` sub-section
  (`BucketName`, `ServiceUrl`, `ForcePathStyle`, `Region`, `KmsKeyId`, `AccessKeyId`,
  `SecretAccessKey`).
- `S3DocumentStorageService : IDocumentStorageService` — `StoreAsync` (PutObject with the
  `evidence-documents/{guid}{ext}` key shape, upload content type, and SSE-KMS when a key is
  configured) and `OpenReadAsync` (GetObject → stream + content type; `NotFound` → `null`).
- `AddInfrastructure` `Aws` branch now registers `IAmazonS3` (LocalStack vs real AWS chosen by
  `ServiceUrl`) and the S3 adapter, failing fast when the bucket is missing. The old
  "arrives in Milestone 42" throw is gone.
- Tests: 4 mocked-`IAmazonS3` adapter unit tests (normal CI), updated `PlatformProfileSwitchTests`
  (Aws wires S3 / missing bucket fails fast), and an opt-in LocalStack round-trip test.
- `docker-compose.yml` gained a profile-scoped `localstack` service; `.env.example` documents the
  opt-in test switches.

## Decisions & why

### Keep the port; add an adapter (blast radius = 1 file + 1 branch)
The whole point of M32's ports & adapters work was this moment. Because every document caller
already depends on `IDocumentStorageService`, swapping storage backends touched no handler, no
controller, no test of the evidence flow. This is the cleanest possible proof that the
Local ⇄ AWS switch works.

### LocalStack, not a real account
`ServiceUrl` + `ForcePathStyle=true` point the identical SDK code at LocalStack. Real AWS is the
same code with `ServiceUrl` dropped, a `Region` set, and credentials coming from the default chain
(task/instance role) rather than static keys. So M42 has **zero cloud cost** and the M46 cutover
is configuration only.

### Static keys only for LocalStack
The `IAmazonS3` factory uses static `AccessKeyId`/`SecretAccessKey` **only** when they are set
(LocalStack). In real AWS they are left empty so `new AmazonS3Client(config)` uses the default
credential chain — no static keys in the cloud, matching the security-by-design guardrail.

### Fail fast on missing bucket
A bucketless S3 client would "work" until the first upload failed at runtime with a confusing
error. The factory throws `InvalidOperationException` at resolution when `DocumentStorage:S3` or
`BucketName` is missing under the Aws profile — the same loud-misconfiguration stance as the auth
and profile guards.

### `AutoCloseStream = false`
The evidence upload workflow owns and disposes the upload stream (it re-opens the stored object
for scanning). Setting `AutoCloseStream = false` on the `PutObjectRequest` stops the SDK from
closing the caller's stream out from under it.

### Testing an SDK adapter without "testing the mock"
The unit tests assert on the **request we build** (bucket, `evidence-documents/` prefix, content
type, SSE-KMS only when configured) and the **response we map** (stream + content type, and
`NotFound` → `null`). That is our logic, not the SDK's. The real SDK behavior (does PutObject
actually persist bytes?) is proven separately by the opt-in LocalStack round-trip test, mirroring
the existing PostgreSQL opt-in pattern so the required CI job stays green.

## Gotchas

- **AWS SDK v4**: `AWSSDK.S3` resolved to v4.0.100.1. The properties used
  (`ServerSideEncryptionMethod`, `ServerSideEncryptionKeyManagementServiceKeyId`,
  `GetObjectResponse.Headers.ContentType`, `GetObjectResponse.ResponseStream`,
  `AmazonS3Exception.StatusCode`) are stable across v3→v4, so no API surprises — but pin the major
  via central package management so a future v5 is an explicit upgrade.
- **Compose profile**: LocalStack sits under the `aws-local` compose profile so it never starts
  during the normal Postgres-only dev/CI flow. Start it explicitly with
  `docker compose --profile aws-local up -d`.
- **CI stays green**: the LocalStack test is env-gated (`LIANSUREPROTECT_RUN_S3_TESTS`) and
  skipped by default; the required "Backend (build, migrate, test)" job does not run LocalStack.

## Verification

- `dotnet test`: UnitTests 66 passed; IntegrationTests 137 passed, 2 skipped (PostgreSQL +
  S3 LocalStack opt-ins).
- LocalStack round-trip test run locally with `LIANSUREPROTECT_RUN_S3_TESTS=true` against
  `docker compose --profile aws-local up -d` — stores and re-reads bytes, and a missing key
  returns null.

## What M42 deliberately left for later

- **Presigned "Valet Key" download URLs** (browser → S3 directly): API streaming stays the default
  until CloudFront exists (M47).
- **Real bucket/KMS/IAM provisioning**: Terraform in Phase 2 (M45/M46).
- **S3-triggered Lambda malware scanning**: the scanner port stays local; the async scan pipeline
  is a later milestone. Clean-only gates are unchanged.
