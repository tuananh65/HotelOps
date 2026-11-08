# CORE08 isolated trial preparation

Project Leader issuance of PL-CORE07-SCOPE-01 was confirmed by the user through CORE08. Exact pack SHA256: 216fac817035fa5971bd2c53daeb094a5923809192a05a0c9d7c93614fba57b2. The preserved original PROPOSED header does not revoke scoped implementation authority and does not approve business, grants or schema.

This scope implements C07 content-policy/read-ACL/SLA interfaces, C08 designated-receiver preconditions, C09 strong If-Match parsing/comparison, C12 caller-provided current eligibility checks, C13 immutable in-memory checklist snapshots and C14 proposed occurrence identity plus explicit DST rejection. Inputs are supplied explicitly; no MIME/size/recurrence/actor policy defaults are invented.

No DI registration, HTTP endpoint, generated entity, shared mapping, permission grant, stock engine, ReturnWork transaction, schema, producer or worker is changed. These are disconnected prototypes, not production authorization or durable idempotency. C09 requires a separately approved durable server version before use; current Asset has no RowVersion. C13/C14 do not persist runs, advance next due or provide a durable race lock. C08 does not finalize custody. C12 caller snapshots do not prove lock-wait rereads or deactivation race correctness.

Build: dotnet build HotelOps.slnx
Focused tests: dotnet run --project tests/HotelOps.Core08.NoSql/HotelOps.Core08.NoSql.csproj
The solution includes API only; dotnet test success alone is not functional test coverage. Tests use explicit synthetic policy inputs, no database connection or SQL mutation.

C01 frozen engine, C02 read-only schema and C03 deny adapters retain their previous source and evidence. C04/C05 transaction services and C06 cycle/concurrency harness are unchanged; their historical evidence is not new CORE08 positive HTTP acceptance. C10/C11 real Work-Time/RoomBlock participants are unavailable in this source; no guessed interface is wired. Further recurrence/anchor, evidence storage, business states, grants, DDL and public action activation remain blocked by their exact contracts.

Candidate technical preparation only. No full module or UC DONE, no production enablement, no develop/main merge. Publication is a six-file independently buildable review scope, not the inherited Full Stack application.
