# UC092 linked incident read slice

2026-11-16. UC092 remains PARTIAL_IMPLEMENTATION; source/QA counters unchanged (39/144, 27/144 historical).

The baseline material_requests.incident_id FK supports an existing linked incident, while optional internal_request_material_links is a separate contract. MaterialLinkedWorkRead now resolves incident summary through the existing IncidentReadAccess with independent INCIDENT_VIEW and branch/data scope before checking optional IR schema. MaterialReadContract records the disclosed incident ID and revalidates its visibility before final disclosure. The UI displays the authorized incident number/title/status and a canonical incident detail link; otherwise it shows unavailable details without widening permissions. Historical link identity remains in the baseline material DTO.

Files: MaterialRequestContracts.cs, MaterialLinkedWorkRead.cs, MaterialReadContract.cs, MaterialHistoryView.tsx; matrix and handoff updated. No generated entities, database changes or new grants.

Actual checks: dotnet build HotelOps.slnx --no-restore PASS (0 warnings); frontend production build (TypeScript + Vite) PASS. Existing React Router directive and bundle-size warnings. New tests/API/SQL/browser acceptance NOT_RUN per code-first instruction; Human QA must check permitted/denied/revoked incident roles, branch isolation and operation without optional IR-link capability.

Remaining GBR-118 gaps: explicit cleaning-task, maintenance-occurrence and operational-task linkage needs typed persistence plus independent current target-view checks and an atomic requester-owned write path. Existing material content edit blocks changes after first issue; do not silently reuse it as a complete all-state Link to Work implementation. Existing maintenance_material_usages binds actual stock use to work logs and is not a material-request link table. No UC completion claimed from this read slice.
