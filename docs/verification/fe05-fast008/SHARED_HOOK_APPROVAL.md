# Scoped approval record

Project Leader FAST008 approved Option 1 (scoped shared hook) in this session. Approved boundary: optional onUsersLocked after complete existing sorted UserEligibilityLock and before Gate/source locks, same caller connection/transaction, exact locked-user results, no DMV authority, unchanged default callers/authorization/public APIs, rollback on callback or roster failure. No further approval is needed for this exact scope.

The reviewed full-file before/after snapshots were retained under ignored TestResults/fast008/reviewed-proposals. The committed shared-hook.patch is the minimal applied diff. No other shared component change is authorized by this record.

The separate PL source-read decision keeps acceptedConcreteBinding=null for all three recipient types. This record does not grant permissions, database deployment, production issuer activation or UC acceptance.
