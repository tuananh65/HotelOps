# DEV3 offline integration harness

Run from the repository root:

```powershell
dotnet run --project tests/HotelOps.Dev3Unblock.NoSql
```

With no arguments this runs validation, authorization-denial, EF SQL translation,
CSRF middleware, dependency snapshot and command-order tests without a real SQL
connection. It is an executable harness, not a test adapter in HotelOps.slnx.

FASTTRACK-141A reconciles the harness with the unified source:

- Missing writer authority is rejected by `Dev2AccessDenied` before the older
  placement guard. This is the expected denial, not a reason to relax production
  authorization.
- Cookie authorization is inherited from `Dev2AuthorityPendingAttribute` on the
  controllers. CSRF is enforced by the shared `ApiCsrfMiddleware`, not action
  attributes. Nine cases execute that middleware with missing/invalid/valid
  synthetic tokens and assert whether the next delegate runs.
- Writer probes use a test-only capability fixture built from the existing
  compiled demo action contracts. It rejects real `SqlConnection` instances,
  restores the process-local demo flag, and fails if later authorization or
  persistence is reached. Its connection metadata matches the historical demo
  contract solely to test that code path; it cannot contact that machine.
- The eight order probes stop at the first actor User UPDLOCK statement. Handover
  accept/reject retain their existing preceding untracked discovery query. These
  probes do not verify receiver lock sorting, physical SQL locks, races, actual
  permission grants, commits, or end-to-end deactivation acceptance.

The separate explicit `--sql-placement` mode remains guarded by its existing
fixture/lease requirements and was **not run** by FASTTRACK-141A. Passing the
offline mode is not permission to execute SQL mode or enable a production writer.
