# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: fs012-live.spec.ts >> FS012 revoked recipient cannot receive source data; actor replay reauthorizes; persisted guard rejects missing records
- Location: tests\fs012-live.spec.ts:773:1

# Error details

```
Error: expect(received).toBe(expected) // Object.is equality

Expected: 200
Received: 503
```

# Test source

```ts
  1   | import { test, expect, type BrowserContext } from "@playwright/test";
  2   | import fs from "node:fs";
  3   | import path from "node:path";
  4   | import { spawnSync } from "node:child_process";
  5   | const root = path.resolve(".."),
  6   |   dir = path.join(root, "scripts/verification/FS012"),
  7   |   helper = path.join(dir, "bin/Debug/net10.0/FS012.Verification.dll");
  8   | const fixture = JSON.parse(
  9   |   fs
  10  |     .readFileSync(path.join(dir, ".private/fixture.json"), "utf8")
  11  |     .replace(/^\uFEFF/, ""),
  12  | );
  13  | if (
  14  |   process.env.FS012_LIVE_ENABLED !== "APPROVED_FS012_LOCAL_TRIAL" ||
  15  |   fixture.database !== "HotelOps_Canonical_20261106_f120001_Fresh" ||
  16  |   fixture.owner !== "DEV1_FS012"
  17  | )
  18  |   throw Error("ISOLATED_SCOPE_MISMATCH");
  19  | function call(args: string[]) {
  20  |   const started = performance.now();
  21  |   const r = spawnSync("dotnet", [helper, ...args], {
  22  |     env: process.env,
  23  |     encoding: "utf8",
  24  |     timeout: 60000,
  25  |     windowsHide: true,
  26  |   });
  27  |   fs.appendFileSync(
  28  |     path.join(root, "docs/fs012/HELPER_TIMINGS.jsonl"),
  29  |     JSON.stringify({
  30  |       runId: process.env.FS012_LIVE_RUN_ID,
  31  |       mode: args[0],
  32  |       durationMs: Math.round(performance.now() - started),
  33  |       exit: r.status,
  34  |       code: (r.error as NodeJS.ErrnoException)?.code ?? null,
  35  |     }) + "\n",
  36  |   );
  37  |   if (r.status !== 0)
  38  |     throw Error(
  39  |       "FS012_HELPER_FAILED mode=" +
  40  |         args[0] +
  41  |         " exit=" +
  42  |         r.status +
  43  |         " signal=" +
  44  |         r.signal +
  45  |         " code=" +
  46  |         (r.error as NodeJS.ErrnoException)?.code +
  47  |         "; private output redacted",
  48  |     );
  49  |   return JSON.parse(r.stdout.replace(/^\uFEFF/, ""));
  50  | }
  51  | const accounts = call(["credentials-ipc", "--private-ipc"]);
  52  | const reportPath = path.join(root, "docs/fs012/LIVE_RESULTS.json");
  53  | const runId = process.env.FS012_LIVE_RUN_ID!;
  54  | const previous = fs.existsSync(reportPath)
  55  |   ? JSON.parse(fs.readFileSync(reportPath, "utf8").replace(/^\uFEFF/, ""))
  56  |   : null;
  57  | const report: { mode: string; runId: string; checks: any[]; tests: any[] } =
  58  |   previous?.runId === runId
  59  |     ? previous
  60  |     : {
  61  |         mode: "REAL_AUTH_HTTPS_API_SQL_FS012_ONLY",
  62  |         runId,
  63  |         checks: [],
  64  |         tests: [],
  65  |       };
  66  | function record(name: string, data: unknown) {
  67  |   report.checks.push({ name, data });
  68  |   fs.writeFileSync(
  69  |     path.join(root, "docs/fs012/LIVE_RESULTS.json"),
  70  |     JSON.stringify(report, null, 2) + "\n",
  71  |   );
  72  | }
  73  | async function login(c: BrowserContext, key: string) {
  74  |   const t = await c.request.get("/api/auth/csrf");
  75  |   expect(t.status()).toBe(200);
  76  |   const csrf = await t.json();
  77  |   expect(
  78  |     (
  79  |       await c.request.post("/api/auth/login", {
  80  |         headers: { "X-CSRF-TOKEN": csrf.csrfToken },
  81  |         data: {
  82  |           username: accounts[key].username,
  83  |           password: accounts[key].password,
  84  |         },
  85  |       })
  86  |     ).status(),
> 87  |   ).toBe(200);
      |     ^ Error: expect(received).toBe(expected) // Object.is equality
  88  | }
  89  | async function post(c: BrowserContext, url: string, data: object) {
  90  |   const t = await c.request.get("/api/auth/csrf");
  91  |   const token = await t.json();
  92  |   return c.request.post(url, {
  93  |     headers: { "X-CSRF-TOKEN": token.csrfToken },
  94  |     data,
  95  |   });
  96  | }
  97  | test.afterEach(async ({}, info) => {
  98  |   report.tests.push({ name: info.title, status: info.status });
  99  |   record("test", info.status);
  100 | });
  101 | test("FS012 create persists exact task/cycle/event/audit, duplicate protection and real React navigation", async ({
  102 |   page,
  103 |   browser,
  104 | }) => {
  105 |   const before = call([
  106 |       "lifecycle-inspect",
  107 |       "--local-sql-certificate-exception",
  108 |     ]),
  109 |     b = fixture.branchId;
  110 |   const room = before.rooms.find(
  111 |     (r: { roomId: number }) =>
  112 |       !before.tasks.some((t: { roomId: number }) => t.roomId === r.roomId),
  113 |   );
  114 |   expect(room).toBeTruthy();
  115 |   const anon = await browser.newContext();
  116 |   try {
  117 |     expect(
  118 |       (
  119 |         await anon.request.get(
  120 |           `/api/branches/${b}/cleaning-task-options?roomId=${room.roomId}`,
  121 |         )
  122 |       ).status(),
  123 |     ).toBe(401);
  124 |   } finally {
  125 |     await anon.close();
  126 |   }
  127 |   await login(page.context(), "owner");
  128 |   expect(
  129 |     (
  130 |       await page.request.get(
  131 |         `/api/branches/${b}/cleaning-task-options?roomId=${room.roomId}`,
  132 |       )
  133 |     ).status(),
  134 |   ).toBe(403);
  135 |   await page.context().clearCookies();
  136 |   await login(page.context(), "admin");
  137 |   const options = await page.request.get(
  138 |     `/api/branches/${b}/cleaning-task-options?roomId=${room.roomId}`,
  139 |   );
  140 |   expect(options.status()).toBe(200);
  141 |   const o = await options.json();
  142 |   expect(o.cleaningItemCount).toBe(2);
  143 |   expect(
  144 |     (
  145 |       await page.request.get(
  146 |         `/api/branches/${fixture.foreignBranchId}/cleaning-task-options?roomId=${room.roomId}`,
  147 |       )
  148 |     ).status(),
  149 |   ).toBe(403);
  150 |   await page.goto(`/cleaning/tasks/new?branchId=${b}&roomId=${room.roomId}`);
  151 |   await expect(
  152 |     page.getByRole("heading", { name: `${room.roomNumber} · Chi nhánh ${b}` }),
  153 |   ).toBeVisible();
  154 |   await page
  155 |     .getByLabel("Hạn UTC (ISO 8601)", { exact: true })
  156 |     .fill(new Date(Date.now() + 3600000).toISOString());
  157 |   await page
  158 |     .getByRole("button", { name: "Tạo công việc", exact: true })
  159 |     .click();
  160 |   await expect(page).toHaveURL(/\/cleaning\/tasks\/\d+\?branchId=/);
  161 |   await expect(
  162 |     page.getByRole("heading", { name: new RegExp(`CL-.*${room.roomNumber}`) }),
  163 |   ).toBeVisible();
  164 |   const after = call([
  165 |     "lifecycle-inspect",
  166 |     "--local-sql-certificate-exception",
  167 |   ]);
  168 |   const task = after.tasks.find(
  169 |     (t: { roomId: number }) => t.roomId === room.roomId,
  170 |   );
  171 |   expect(task).toBeTruthy();
  172 |   expect(
  173 |     after.cycles.some(
  174 |       (x: { taskId: number; roomId: number }) =>
  175 |         x.taskId === task.cleaningTaskId && x.roomId === room.roomId,
  176 |     ),
  177 |   ).toBe(true);
  178 |   expect(
  179 |     after.events.filter(
  180 |       (x: { taskId: number }) => x.taskId === task.cleaningTaskId,
  181 |     ),
  182 |   ).toHaveLength(1);
  183 |   expect(
  184 |     after.audits.some(
  185 |       (a: { entityId: string; actorUserId: number; branchId: number }) =>
  186 |         a.entityId === String(task.cleaningTaskId) &&
  187 |         a.actorUserId === accounts.admin.userId &&
```