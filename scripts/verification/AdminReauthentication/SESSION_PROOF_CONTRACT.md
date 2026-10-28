# Server session proof contract

Storage: a singleton process-local ReauthenticationProofStore, synchronized with a private lock, maximum 10,000 entries. No persistence or client proof transport.

Key: validated user ID, actual SQL session ID and the protected session credential's existing SHA-256 hash. Entry: cloned user rowversion, backend verification time and absolute expiry. Neither current password nor password hash is stored.

Lifetime is no more than five minutes from successful password verification, additionally capped by the validated session's current idle/absolute deadline. Time comes from the injected backend TimeProvider. At the deadline (>=), in a backwards-before-issuance clock state, or with changed rowversion, matching fails. Touch does not update an entry or move its deadline. Expired entries are rejected/removed on access and pruned on publication; capacity eviction also fails closed.

No public store method lets a controller publish arbitrary proof. Only the password-verifying service publishes, after its SQL transaction commits. No endpoint accepts a proof timestamp, ID, boolean or session key from the request body.

Every use first calls real SQL session validation within the same transaction as reset. Thus revoked/logout/expired/inactive/restricted sessions cannot use even an unexpired cache record. Credential rotation changes rowversion and revokes target sessions, invalidating all related old proofs without changing the schema.

Rowversion binding is deliberately conservative: any user write, including profile, successful login LastLoginAt or failed-login counters, can require reauthentication again. It is not a new permission grant or a claim that every such write changes the password. A second session cannot borrow the first session's proof even with the same user/password.

Proof is reusable within its window for approved sensitive operations, subject to SQL/session/version checks. There is no one-time-consume contract. Concurrent reauthentication serializes SQL user verification; cache publication does not extend older verification deadlines and only retains a bounded result.

Restart/recycle loses proofs. Another application instance has its own empty store and denies a proof created elsewhere; no fallback to LastUsedAt/CreatedAt or client state is allowed. Multi-instance transparent reuse is not implemented. Route a reauthenticated workflow consistently to its instance or perform reauth again; a distributed proof store would require separate design/verification.

SQL remains authoritative for validity; this is not offline authorization. Provider errors are not interpreted as a valid session/proof. Proof is checked again immediately before committing reset.
