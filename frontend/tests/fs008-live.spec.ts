import { test, expect, type BrowserContext } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
const root = path.resolve(".."),
  dir = path.join(root, "scripts/verification/FS008"),
  helper = path.join(dir, "bin/Debug/net10.0/FS008.Verification.dll");
const fixture = JSON.parse(
  fs
    .readFileSync(path.join(dir, ".private/fixture.json"), "utf8")
    .replace(/^\uFEFF/, ""),
);
if (
  process.env.FS008_LIVE_ENABLED !== "APPROVED_FS008_LOCAL_TRIAL" ||
  fixture.database !== "HotelOps_Canonical_20261106_220000_f080001_Fresh" ||
  fixture.owner !== "DEV1_FS008"
)
  throw Error("ISOLATED_SCOPE_MISMATCH");
function call(args: string[]) {
  const r = spawnSync("dotnet", [helper, ...args], {
    env: process.env,
    encoding: "utf8",
    timeout: 30000,
    windowsHide: true,
  });
  if (r.status !== 0)
    throw Error("FS008_HELPER_FAILED; private output redacted");
  return JSON.parse(r.stdout.replace(/^\uFEFF/, ""));
}
const accounts = call(["credentials-ipc", "--private-ipc"]);
const report: { mode: string; checks: unknown[]; tests: unknown[] } = {
  mode: "REAL_AUTH_HTTPS_API_SQL_FS008_ONLY",
  checks: [],
  tests: [],
};
function record(name: string, data: unknown) {
  report.checks.push({ name, data });
  fs.writeFileSync(
    path.join(root, "docs/fs008/LIVE_RESULTS.json"),
    JSON.stringify(report, null, 2) + "\n",
  );
}
async function login(c: BrowserContext, key: string) {
  const t = await c.request.get("/api/auth/csrf");
  expect(t.status()).toBe(200);
  const csrf = await t.json();
  expect(
    (
      await c.request.post("/api/auth/login", {
        headers: { "X-CSRF-TOKEN": csrf.csrfToken },
        data: {
          username: accounts[key].username,
          password: accounts[key].password,
        },
      })
    ).status(),
  ).toBe(200);
}
async function post(c: BrowserContext, url: string, data: object) {
  const t = await c.request.get("/api/auth/csrf");
  const token = await t.json();
  return c.request.post(url, {
    headers: { "X-CSRF-TOKEN": token.csrfToken },
    data,
  });
}
test.afterEach(async ({}, info) => {
  report.tests.push({ name: info.title, status: info.status });
  record("test", info.status);
});
test("FS008 create persists exact task/cycle/event/audit, duplicate protection and real React navigation", async ({
  page,
  browser,
}) => {
  const before = call([
      "lifecycle-inspect",
      "--local-sql-certificate-exception",
    ]),
    b = fixture.branchId;
  const room = before.rooms.find(
    (r: { roomId: number }) =>
      !before.tasks.some((t: { roomId: number }) => t.roomId === r.roomId),
  );
  expect(room).toBeTruthy();
  const anon = await browser.newContext();
  try {
    expect(
      (
        await anon.request.get(
          `/api/branches/${b}/cleaning-task-options?roomId=${room.roomId}`,
        )
      ).status(),
    ).toBe(401);
  } finally {
    await anon.close();
  }
  await login(page.context(), "owner");
  expect(
    (
      await page.request.get(
        `/api/branches/${b}/cleaning-task-options?roomId=${room.roomId}`,
      )
    ).status(),
  ).toBe(403);
  await page.context().clearCookies();
  await login(page.context(), "admin");
  const options = await page.request.get(
    `/api/branches/${b}/cleaning-task-options?roomId=${room.roomId}`,
  );
  expect(options.status()).toBe(200);
  const o = await options.json();
  expect(o.cleaningItemCount).toBe(2);
  expect(
    (
      await page.request.get(
        `/api/branches/${fixture.foreignBranchId}/cleaning-task-options?roomId=${room.roomId}`,
      )
    ).status(),
  ).toBe(403);
  await page.goto(`/cleaning/tasks/new?branchId=${b}&roomId=${room.roomId}`);
  await expect(
    page.getByRole("heading", { name: `${room.roomNumber} · Chi nhánh ${b}` }),
  ).toBeVisible();
  await page
    .getByLabel("Hạn UTC (ISO 8601)", { exact: true })
    .fill(new Date(Date.now() + 3600000).toISOString());
  await page
    .getByRole("button", { name: "Tạo công việc", exact: true })
    .click();
  await expect(page).toHaveURL(/\/cleaning\/tasks\/\d+\?branchId=/);
  await expect(
    page.getByRole("heading", { name: new RegExp(`CL-.*${room.roomNumber}`) }),
  ).toBeVisible();
  const after = call([
    "lifecycle-inspect",
    "--local-sql-certificate-exception",
  ]);
  const task = after.tasks.find(
    (t: { roomId: number }) => t.roomId === room.roomId,
  );
  expect(task).toBeTruthy();
  expect(
    after.cycles.some(
      (x: { taskId: number; roomId: number }) =>
        x.taskId === task.cleaningTaskId && x.roomId === room.roomId,
    ),
  ).toBe(true);
  expect(
    after.events.filter(
      (x: { taskId: number }) => x.taskId === task.cleaningTaskId,
    ),
  ).toHaveLength(1);
  expect(
    after.audits.some(
      (a: { entityId: string }) => a.entityId === String(task.cleaningTaskId),
    ),
  ).toBe(true);
  expect(
    after.rooms.find((r: { roomId: number }) => r.roomId === room.roomId)
      .operationalStatus,
  ).toBe(room.operationalStatus);
  const input = {
    roomId: room.roomId,
    cleaningTypeId: o.cleaningTypes[0].id,
    priorityId: o.priorities[0].id,
    dueAt: new Date(Date.now() + 3600000).toISOString(),
  };
  expect(
    (
      await post(page.context(), `/api/branches/${b}/cleaning-tasks`, input)
    ).status(),
  ).toBe(409);
  await page.goto(`/cleaning/tasks?branchId=${b}&search=${task.taskNo}`);
  await expect(
    page.getByRole("link", { name: task.taskNo, exact: true }),
  ).toBeVisible();
  record("create-atomic-sql-and-ui", {
    taskId: task.cleaningTaskId,
    branch: b,
    room: room.roomId,
    legacyUnchanged: true,
    cycle: true,
    event: 1,
    audit: true,
    duplicate: 409,
    anonymous: 401,
    forbidden: 403,
    foreign: 403,
  });
});
test("FS008 competing Create yields one201/one409; audit failure rolls back all mandatory records", async ({
  page,
}) => {
  await login(page.context(), "admin");
  const b = fixture.branchId,
    before = call(["lifecycle-inspect", "--local-sql-certificate-exception"]);
  const free = before.rooms.filter(
    (r: { roomId: number }) =>
      !before.tasks.some((t: { roomId: number }) => t.roomId === r.roomId),
  );
  expect(free.length).toBeGreaterThan(1);
  const option = await page.request.get(
    `/api/branches/${b}/cleaning-task-options?roomId=${free[0].roomId}`,
  );
  expect(option.status()).toBe(200);
  const o = await option.json();
  const make = (roomId: number) => ({
    roomId,
    cleaningTypeId: o.cleaningTypes[0].id,
    priorityId: o.priorities[0].id,
    dueAt: new Date(Date.now() + 3600000).toISOString(),
  });
  const token = await (await page.request.get("/api/auth/csrf")).json();
  const responses = await Promise.all([
    page.request.post(`/api/branches/${b}/cleaning-tasks`, {
      headers: { "X-CSRF-TOKEN": token.csrfToken },
      data: make(free[0].roomId),
    }),
    page.request.post(`/api/branches/${b}/cleaning-tasks`, {
      headers: { "X-CSRF-TOKEN": token.csrfToken },
      data: make(free[0].roomId),
    }),
  ]);
  expect(responses.map((x) => x.status()).sort()).toEqual([201, 409]);
  const preFault = call([
    "lifecycle-inspect",
    "--local-sql-certificate-exception",
  ]);
  call(["fault-on", "--local-sql-certificate-exception"]);
  try {
    const r = await post(
      page.context(),
      `/api/branches/${b}/cleaning-tasks`,
      make(free[1].roomId),
    );
    expect(r.status()).toBeGreaterThanOrEqual(500);
  } finally {
    call(["fault-off", "--local-sql-certificate-exception"]);
  }
  const postFault = call([
    "lifecycle-inspect",
    "--local-sql-certificate-exception",
  ]);
  expect(postFault).toEqual(preFault);
  record("create-race-and-rollback", {
    statuses: [201, 409],
    auditFailureRollback: true,
    noTaskCycleEventAuditPartial: true,
  });
});
test("FS008 UC032 regression read and UC029 mandatory dependency never commits", async ({
  page,
}) => {
  await login(page.context(), "owner");
  expect(
    (
      await page.request.get(
        `/api/branches/${fixture.branchId}/cleaning-tasks/${fixture.taskId}/progress`,
      )
    ).status(),
  ).toBe(200);
  await page.context().clearCookies();
  await login(page.context(), "admin");
  const before = call([
    "lifecycle-inspect",
    "--local-sql-certificate-exception",
  ]);
  const token = await (await page.request.get("/api/auth/csrf")).json();
  const r = await page.request.post(
    `/api/branches/${fixture.branchId}/cleaning-tasks/${fixture.taskId}/assign`,
    {
      headers: {
        "X-CSRF-TOKEN": token.csrfToken,
        "If-Match": '"0000000000000001"',
        "Idempotency-Key": crypto.randomUUID(),
      },
      data: {
        assigneeId: accounts.owner.userId,
        dueAt: new Date(Date.now() + 3600000).toISOString(),
        reason: "FS008 mandatory publisher unavailable",
        adoptUnassignedLegacy: false,
      },
    },
  );
  expect(r.status()).toBe(503);
  expect(
    call(["lifecycle-inspect", "--local-sql-certificate-exception"]),
  ).toEqual(before);
  record("uc032-read-assign-failclosed", {
    progress: 200,
    assignment: 503,
    mutation: false,
    reason: "MISSING_EXECUTABLE_SOURCE_A01_A02",
  });
});

test("FS008 assigned source header independently enforces recipient OWN and branch, renders without monitor widening", async ({
  page,
}) => {
  const proof = call([
      "lifecycle-inspect",
      "--local-sql-certificate-exception",
    ]),
    a = proof.assignments.find(
      (x: { cleaningTaskId: number }) =>
        x.cleaningTaskId === fixture.inactiveTaskId,
    );
  expect(a).toBeTruthy();
  const url = `/api/branches/${fixture.branchId}/cleaning-tasks/${fixture.inactiveTaskId}?assignmentId=${a.assignmentId}`;
  await login(page.context(), "owner");
  const r = await page.request.get(url);
  expect(r.status()).toBe(200);
  const header = await r.json();
  expect(header.assignmentId).toBe(a.assignmentId);
  expect(header.id).toBe(fixture.inactiveTaskId);
  expect(
    (
      await page.request.get(
        `/api/branches/${fixture.branchId}/cleaning-monitor`,
      )
    ).status(),
  ).toBe(403);
  await page.goto(
    `/cleaning/tasks/${fixture.inactiveTaskId}?branchId=${fixture.branchId}&assignmentId=${a.assignmentId}`,
  );
  await expect(
    page.getByRole("heading", {
      name: `${header.taskNumber} · ${header.roomNumber}`,
    }),
  ).toBeVisible();
  expect(
    (
      await page.request.get(
        `/api/branches/${fixture.foreignBranchId}/cleaning-tasks/${fixture.inactiveTaskId}?assignmentId=${a.assignmentId}`,
      )
    ).status(),
  ).toBe(403);
  await page.context().clearCookies();
  await login(page.context(), "other");
  expect((await page.request.get(url)).status()).toBe(403);
  record("recipient-own-source-read", {
    owner: 200,
    foreignBranch: 403,
    otherAssignee: 403,
    monitorStillDenied: 403,
    realReact: true,
  });
});

test("FS008 Create rejects unauthorized writes, invalid contracts and missing CSRF without persistence", async ({
  page,
}) => {
  const before = call([
    "lifecycle-inspect",
    "--local-sql-certificate-exception",
  ]);
  const b = fixture.branchId;
  const room = before.rooms[0].roomId;
  await login(page.context(), "admin");
  const optionsResponse = await page.request.get(
    `/api/branches/${b}/cleaning-task-options?roomId=${room}`,
  );
  expect(optionsResponse.status()).toBe(200);
  const options = await optionsResponse.json();
  const payload = {
    roomId: room,
    cleaningTypeId: options.cleaningTypes[0].id,
    priorityId: options.priorities[0].id,
    dueAt: new Date(Date.now() + 3600000).toISOString(),
  };
  const url = `/api/branches/${b}/cleaning-tasks`;
  expect((await page.request.post(url, { data: payload })).status()).toBe(400);
  expect(
    (
      await post(page.context(), url, { ...payload, status: "COMPLETED" })
    ).status(),
  ).toBe(400);
  expect(
    (
      await post(page.context(), url, {
        ...payload,
        dueAt: new Date(Date.now() - 3600000).toISOString(),
      })
    ).status(),
  ).toBe(400);
  expect(
    (
      await post(page.context(), url, {
        ...payload,
        cleaningTypeId: 2147483647,
      })
    ).status(),
  ).toBe(400);
  expect(
    (
      await post(
        page.context(),
        `/api/branches/${fixture.foreignBranchId}/cleaning-tasks`,
        payload,
      )
    ).status(),
  ).toBe(403);
  await page.context().clearCookies();
  await login(page.context(), "owner");
  expect((await post(page.context(), url, payload)).status()).toBe(403);
  expect(
    call(["lifecycle-inspect", "--local-sql-certificate-exception"]),
  ).toEqual(before);
  record("create-negative-request-and-write-authority", {
    missingCsrf: 400,
    unknownField: 400,
    pastDue: 400,
    invalidReference: 400,
    foreignWrite: 403,
    ownerWrite: 403,
    mutation: false,
  });
});
