# Task47 — DEV2 integration handoff checklist

- [ ] Team supplies final commit/revision and approved Git handoff. No merge/cherry-pick
  was performed in Task47; source remains on DEV1 HEAD 6278a88 plus local changes.
- [ ] Confirm coordinated writer source is actually present, not only audit receipts:
  Maintenance assign/start/reopen; Warehouse manager/create/reactivate; Asset
  custody/receiver/create/accept/reopen; Material Request requester/resubmit/current
  approval-cycle assignment and all eligibility-changing paths.
- [ ] Same retained User UPDLOCK,HOLDLOCK protocol, ascending participant IDs, caller
  transaction ownership; validate DeletedAt/AccountStatus/EmploymentStatus after locks.
- [ ] Feature locks after Users; authoritative reread/restart on participant drift;
  no old ACTIVE snapshot authorizes writes; no reverse lock acquisition on alternate paths.
- [ ] Verify latest Material Request cycle means max RevisionNo, with parent
  PENDING_APPROVAL and cycle PENDING; historical/superseded rows do not block.
- [ ] Review legacy rows and external writers. Missing workflow audit marker cannot
  make current responsibility disappear.
- [ ] Re-run real SQL writer-first/deactivate-first tests on integrated source, with
  lock-manager evidence, timeouts/deadlocks/cancellation and rollback assertions.
- [ ] Integrate complete DEV1 predicates/writers, atomic LastAdmin/session/audit workflow,
  then all UC004/005/006/007/010/Authorization/Startup regressions.

HTTP Deactivate remains explicitly forbidden in Task47, even if this checklist later passes.
