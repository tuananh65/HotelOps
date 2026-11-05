# DEV3 onboarding on another machine

Repository: https://github.com/tuananh65/HotelOps. Baseline branch: integration/fullstack-base (WIP/PARTIAL). Exact integration source pin is recorded in PUBLICATION.md after source commit; final remote SHA must match the FS-001 publication receipt. No DEV3 worktree/branch is created on DEV1's machine.

```powershell
git clone https://github.com/tuananh65/HotelOps.git
cd HotelOps
git fetch origin --prune
git ls-remote origin refs/heads/integration/fullstack-base
$baselineSha = git rev-parse origin/integration/fullstack-base
# Compare baselineSha with the verified final FS-001 SHA before continuing.
git switch -c feature/dev3-platform $baselineSha
dotnet restore HotelOps.slnx
dotnet build HotelOps.slnx
dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj
cd frontend
npm ci --no-audit --no-fund
npm run build
npm test
npm run dev -- --host localhost --port 5173
```

Read scripts/verification/HANDOVER_CODEX_A_20261105/README.md and HANDOVER_CODEX_B_20261105/HANDOVER.md. Full immutable handovers: A28f0a5e4f582bc56ba101b48915d33baadeb7b95; B21c06bf51bcbcf0c99e8c711302b71682590e41f. Use git show <SHA>:<path> for evidence not copied locally. Do not automatically execute archived/proposal scripts.

DEV3 owns FE01 Authentication/User/Role/Permission,FE02 Structure/room master data,FE12 Workday/Work-Time. Main paths: Security,Services/Authentication,Services/Users,HotelStructure master data,WorkTime. Preserve cookie/SQL-session/CSRF and OWN semantics; no JWT switch.

UC012 remains PARTIAL; UC013 historically credited; UC138 full workflow missing; UC139 B1.2 blocked; UC142 recovery HTTP inactive. Reconcile frontend RoomTypeList UC017 with backend recovery UC142. SCREEN_UC_API_MAPPING covers all80 screens but is not an approved backend permission matrix.

For API run/SQL configuration, follow ENVIRONMENT_SETUP after separate trial approval. COMMON files requiring review: Program.cs,DbContext/entities/schema,authorization policies,source-read/transaction/lifecycle/notification participants. Initial priorities: real auth transport/protected routes,route/security review,Workday dependencies with DEV1/DEV2. Do not infer schema rollout approval from a successful source build.

Exact tested source baseline: a02cb2f27e9f7944b4e7f8b03da9f6f22bf3808f. Before branching, run the pinned tree/history checks in PUBLICATION.md and compare final remote SHA with the FS-001 completion receipt. The final documentation commit has the same src/frontend trees.
