# Regression scope

UC028 remains VERIFIED_LOCAL, not final shared/SRS acceptance. Confirmed task contract uses additive current-cycle/readiness event while preserving legacy room status; this differs from historical SRS wording about direct room status/history updates and remains a shared review point. No invented pending role permissions.

UC029 implemented candidate with EF commit-time proof and durable recovery; PROVISIONAL_NOT_ACCEPTED. Default-off fail-closed remains mandatory. Tests cover authoritative DTO/ETag/key, SQL counts, authorization, branch/ownership, replay/conflict/races, rollback, retry/delivery, recipient source security and task/readiness regression.

Existing UC032/Monitor business behavior unchanged. UC032 real write keeps IN_PROGRESS at 100%, stale 412/wrong owner 403; original Node/interception tests retained. No Reassign/Start/Complete/Rework endpoints added. Individual API and real UI evidence differ; see full run checks and coverage matrix.
