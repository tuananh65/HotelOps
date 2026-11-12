# UC012 synthetic fixture seeder — source-only candidate

No execution approval is supplied. Do not run `--seed` under FASTTRACK-045.
The only verification mode used here is `--self-test`, which opens no SQL,
loads no credentials/certificate and starts no HTTPS listener.

```powershell
dotnet build scripts/verification/UC012Fixtures/UC012Fixtures.csproj
dotnet scripts/verification/UC012Fixtures/bin/Debug/net10.0/UC012Fixtures.dll --self-test
```

The candidate implements one serializable fixture transaction on an empty,
GUID/isolation/schema-verified `HotelOps_UC012_Trial_*` database. It creates a
Branch/Department, two ACTIVE identities with distinct supplied secret inputs,
current IdentityV3 hashes, STANDARD primary memberships and two assignments to
the existing canonical SYS_ADMIN role. Only the allowed actor is unbound; the
denied actor is branch-bound. Existing catalog rows/grants are never changed.
Role names do not authorize requests; actual effective admission is still tested
by the existing HTTPS runner. No sessions or historical credentials are seeded.

The new fixture marker commits with its rows. One transaction-owned application
lock serializes provisioners. Any existing marker or personnel/Branch/session/audit
data rejects replay without repair or overwrite. A lost commit/receipt response
requires owner inspection, never automatic retry; repeated invocation is a
fail-closed rejection, not a second seed or a claim of verified idempotent success.
Receipt creation after commit can fail: its absence is not proof of rollback.

Before future authorized use, the plan must bind named fixture/security execution
approvals, exact host/server/database GUID/isolation marker, fresh exclusive lease,
safe run/prefix keys, approved schema/manifest hashes and candidate API assembly
hash. Design approval alone fails validation. The approved executable manifest
must contain `approved`, `owner`, `databaseGuid`, `leaseId`, `runId`, `schemaSha256`
and `identityProfile="UC012.SYS_ADMIN.UnboundVsBranch.v1"`; this is a reviewed
derivative of the design manifest, not a declaration that it has been approved.
Provisioning uses a separate authorized provisioner, never the restricted runtime
principal. This tool creates no database, login, grants, schema or lease.

Future CLI shape, NOT authorized or invoked in this task:
`--seed approved-plan.json exact-plan-SHA256 NEW-receipt.json`.
It additionally requires process-only `UC012_FIXTURE_EXECUTE=APPROVED`,
`UC012_FIXTURE_SQL`, `UC012_ACTOR_PASSWORD`, `UC012_DENIED_PASSWORD`.
SQL must require encryption, normal certificate/hostname verification and
ConnectRetryCount=0; no provider retries or EF retry strategy is enabled.
Passwords must be fresh independent cryptographically random secrets supplied by
the authorized owner; local length/equality checks do not prove randomness.
Never commit those inputs, command-line credentials or hash/session material.

The fixture receipt contains generated IDs/names, ownership and artifact bindings,
not passwords/hashes/tokens. It is compatible with PR72's JSON fixture checks;
it is not the full trial receipt and does not authorize acceptance or attest
restricted principal rights. SQL translation, locking races, real rollback,
permission evaluation and HTTPS admission remain NOT_RUN.

UC006/UC010 production source unchanged: existing dependency readers/fingerprint,
final transactional rereads and immutable release gates are retained. Their
missing writer participation/SQL session-audit-race acceptance cannot be supplied
by synthetic no-SQL fixtures. No duplicate production candidate is introduced.
