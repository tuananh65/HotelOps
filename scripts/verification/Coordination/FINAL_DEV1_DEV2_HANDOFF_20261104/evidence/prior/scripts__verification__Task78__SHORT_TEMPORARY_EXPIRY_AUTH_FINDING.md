# Existing authentication finding — outside B1.1, NOT FIXED

Trials `HotelOps_Canonical_20261104_062644_e9f68a23_Fresh` and
`HotelOps_Canonical_20261104_063341_c44f2751_Fresh` reproduced a restricted cookie
being rejected with401 `SESSION_INVALID`; actual CookieAuthenticationHandler log says
`Ticket expired`. Owner normal-session round trip200 in the second trial rules out
a general cookie capture/Data Protection failure. Failed runs are not counted as PASS.

Fixture temporary credential expiry was SQL server UTC +10 minutes. SQL EF DateTime
values are read as Unspecified. `SqlSessionPolicy.AbsoluteDeadline` selects that
Unspecified temporary cap when it is earlier than created+15 minutes.
`HotelOpsCookieTicket.Create` then constructs `new DateTimeOffset(s.AbsoluteExpiresAtUtc)`;
on the machine's UTC+7 timezone this interprets the value as local and shifts the ticket's
expiry seven hours backwards. This explanation follows the source and observed expired
ticket; it is not a newly implemented/fixed authentication contract.

The accepted B1 trial uses expiry+1 hour: the existing restricted session cap is the
UTC `created+15 minutes`, so a valid restricted cookie is verified to receive403 on
Workday endpoints. This does **not** demonstrate that short temporary expiry is repaired.
No production authentication source, account gates or credential policy was changed.

Minimal candidate for a separate reviewed shared-auth fix:

File: `src/HotelOps.Api/Services/Authentication/SqlSessionPolicy.cs`, method AbsoluteDeadline.

```diff
 var deadline = created.Add(temporaryExpiry.HasValue ? RestrictedLifetime : NormalAbsoluteLifetime);
-return temporaryExpiry.HasValue && temporaryExpiry.Value < deadline ? temporaryExpiry.Value : deadline;
+return temporaryExpiry.HasValue && temporaryExpiry.Value < deadline
+    ? DateTime.SpecifyKind(temporaryExpiry.Value, DateTimeKind.Utc) : deadline;
```

Candidate assumes the documented SQL expiry is UTC. Before applying, review all callers
and UTC storage contract, then test shorter-than15-minute and longer temporary expiry,
exact expiry, normal cookies, SQL policy and real HTTPS/cookie round trip on UTC+7.
This candidate is **not approved, applied or tested as a fix**, and is not part of the
approved Program.cs patch. B1 scheduled Workday functionality does not depend on applying it.
