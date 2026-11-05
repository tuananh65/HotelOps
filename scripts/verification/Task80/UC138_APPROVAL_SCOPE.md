# UC138 — C1 narrow schema/mapping review, NOT APPROVED / NOT APPLIED

Full UC138 is blocked by mandatory notifications and GBR005 rowversion support. This is
one concrete request for the **minimum rowversion schema/model gate only**; no public writer,
DI, middleware, RBAC, DEV2 source or publisher implementation is authorized by this draft.

## Exact additive scope for a NEW disposable trial only

SQL candidate target `scripts/verification/Task80/proposal/01_ShiftConcurrency.forward.sql`
(not created/executed yet): guarded explicit new canonical trial name + trial marker + approval
session flag, transaction/XACT_ABORT, exactly:

```sql
ALTER TABLE dbo.shift_templates ADD row_version rowversion NOT NULL;
ALTER TABLE dbo.work_shifts ADD row_version rowversion NOT NULL;
ALTER TABLE dbo.shift_assignments ADD row_version rowversion NOT NULL;
```

No data reset, status/relationship/column type change, role grants, shared DB or EF migration.
Full script after approval must validate actual metadata, exact existing-column compatibility,
package marker and safe replay; fail on partial/incompatible installation. Populated legacy
rows must survive. Empty-owned rollback/recovery only; refuse dropping recorded concurrency
evidence on populated data. No source mappings activated until DDL/build/EF checks pass.

### Five exact C# targets, minimal additive diff

New `src/HotelOps.Api/Entities/ShiftTemplate.Concurrency.cs`:

```csharp
namespace HotelOps.Api.Entities;
public partial class ShiftTemplate
{
    public byte[] RowVersion { get; set; } = null!;
}
```

New `Entities/WorkShift.Concurrency.cs` and `Entities/ShiftAssignment.Concurrency.cs`,
same namespace and exact property, respective existing partial class names. Existing
generated entity files remain untouched.

New `src/HotelOps.Api/Data/HotelOpsDbContext.ShiftConcurrency.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Data;
public partial class HotelOpsDbContext
{
    private static void ConfigureShiftConcurrency(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entities.ShiftTemplate>().Property(x => x.RowVersion)
            .HasColumnName("row_version").IsRowVersion();
        modelBuilder.Entity<Entities.WorkShift>().Property(x => x.RowVersion)
            .HasColumnName("row_version").IsRowVersion();
        modelBuilder.Entity<Entities.ShiftAssignment>().Property(x => x.RowVersion)
            .HasColumnName("row_version").IsRowVersion();
    }
}
```

Existing `src/HotelOps.Api/Data/HotelOpsDbContext.cs`, one insertion only:

```diff
 ConfigureWorkTime(modelBuilder);
+ConfigureShiftConcurrency(modelBuilder);
 OnModelCreatingPartial(modelBuilder);
```

This affects shared EF mapping and future writers/readers on all3 tables. Legacy databases
without the new columns need an explicit deployment/compatibility decision before model
activation: do not silently break B1.1/A1 reads or add an unapproved startup bypass. If an
additional compatibility mapping/gate file is necessary, submit its exact diff separately;
this request does not authorize extra shared changes.

Required C1 verification: disposable fresh/populated legacy preservation, EF mapping and
stale-update conflict, replay/partial-install rejection, empty rollback/reapply, retained
A1/B1.1 ShiftAssignment/Workday/ShiftAttendance compatibility, build/diff and affected SQL
regressions. This C1 gate alone earns no complete UC.

## Remaining dependencies before UC138 public workflows

- Existing SQL permissions: SHIFT_MANAGE/BRANCH for DEPT_MANAGER and SHIFT_VIEW; proposed
  branch admin resource binding must be reviewed, no new permission/role grants. DH role
  remains branch-wide; selected department is business context. Own-only B1.1 resolver is
  not silently expanded into administrative ownership.
- DEV2 joint-approved publisher/replay implementation with stable operation mapping and
  durable readable inbox. Proposed logical shift-assignment-changed/cancelled event must be
  added to its actual approved registry; no guessed NotificationType or local queue.
  Resolve affected users from committed assignments; actor/branch/source/version/old-new
  effective windows/reason/idempotency facts share the caller-owned transaction.
- Explicit template semantics: future-only materialization vs propagation to existing future
  WorkShifts; preserve historical snapshots. Any propagation rechecks all affected employees'
  cross-branch overlaps under sorted User locks, rowversions and audit/intents.
- Explicit overnight/equal-time semantics. Existing CrossesMidnight is evidence of a model
  field, not approval to interpret equal times as24 hours. Code length30/name100 and branch/code
  unique index are schema limits; case-insensitive GBR003 validation must be verified.
- Cancel only eligible future assignments using SQL clock, preserve history and A1 references;
  coordinate Cancel vs Start Workday under same sorted User/time locks. Require no started
  workday/interval dependency and approved cancellation reason/status policy; no hard deletion.
- Full acceptance: real authenticated HTTPS SQL-backed branch authorization, definitions+
  weekly roster/create/edit/assign/cancel, stale version, cross-user/branch/global overlap races,
  transactional audit and fault rollback, notification delivery/retry/replay, and A1/B1.1 regression.

No source restoration from cf8fb02 or notification-dependency waiver is included. Current
safe overlap reader remains an internal read component; full UC138 stays NOT_IMPLEMENTED.
