import { test, expect, type BrowserContext } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
const root = path.resolve(".."),
  dir = path.join(root, "scripts/verification/FS012"),
  helper = path.join(dir, "bin/Debug/net10.0/FS012.Verification.dll");
const fixture = JSON.parse(
  fs
    .readFileSync(path.join(dir, ".private/fixture.json"), "utf8")
    .replace(/^\uFEFF/, ""),
);
if (
  process.env.FS012_LIVE_ENABLED !== "APPROVED_FS012_LOCAL_TRIAL" ||
  fixture.database !== "HotelOps_Canonical_20261106_f120001_Fresh" ||
  fixture.owner !== "DEV1_FS012"
)
  throw Error("ISOLATED_SCOPE_MISMATCH");
function call(args: string[]) {
  const started = performance.now();
  const r = spawnSync("dotnet", [helper, ...args], {
    env: process.env,
    encoding: "utf8",
    timeout: 60000,
    windowsHide: true,
  });
  fs.appendFileSync(
    path.join(root, "docs/fs012/HELPER_TIMINGS.jsonl"),
    JSON.stringify({
      runId: process.env.FS012_LIVE_RUN_ID,
      mode: args[0],
      durationMs: Math.round(performance.now() - started),
      exit: r.status,
      code: (r.error as NodeJS.ErrnoException)?.code ?? null,
    }) + "\n",
  );
  if (r.status !== 0)
    throw Error(
      "FS012_HELPER_FAILED mode=" +
        args[0] +
        " exit=" +
        r.status +
        " signal=" +
        r.signal +
        " code=" +
        (r.error as NodeJS.ErrnoException)?.code +
        "; private output redacted",
    );
  return JSON.parse(r.stdout.replace(/^\uFEFF/, ""));
}
const accounts = call(["credentials-ipc", "--private-ipc"]);
const reportPath = path.join(root, "docs/fs012/LIVE_RESULTS.json");
const runId = process.env.FS012_LIVE_RUN_ID!;
const previous = fs.existsSync(reportPath)
  ? JSON.parse(fs.readFileSync(reportPath, "utf8").replace(/^\uFEFF/, ""))
  : null;
const report: { mode: string; runId: string; checks: any[]; tests: any[] } =
  previous?.runId === runId
    ? previous
    : {
        mode: "REAL_AUTH_HTTPS_API_SQL_FS012_ONLY",
        runId,
        checks: [],
        tests: [],
      };
function record(name: string, data: unknown) {
  report.checks.push({ name, data });
  fs.writeFileSync(
    path.join(root, "docs/fs012/LIVE_RESULTS.json"),
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
test("FS012 create persists exact task/cycle/event/audit, duplicate protection and real React navigation", async ({
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
      (a: { entityId: string; actorUserId: number; branchId: number }) =>
        a.entityId === String(task.cleaningTaskId) &&
        a.actorUserId === accounts.admin.userId &&
        a.branchId === b,
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
  const monitorReply = page.waitForResponse(
    (r) =>
      r.request().method() === "GET" &&
      new URL(r.url()).pathname === `/api/branches/${b}/cleaning-monitor`,
  );
  await page.goto(`/cleaning/tasks?branchId=${b}&search=${task.taskNo}`);
  const monitor = await monitorReply;
  expect(monitor.status()).toBe(200);
  const monitorData = await monitor.json();
  expect(monitorData.items.some((x: any) => x.taskNumber === task.taskNo)).toBe(
    true,
  );
  record("create-monitor-api-ready", {
    status: monitor.status(),
    matchingTask: true,
  });
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
test("FS012 competing Create yields one201/one409; audit failure rolls back all mandatory records", async ({
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
test("FS012 UC032 regression read and UC029 stale task version never commits", async ({
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
        reason: "FS012 mandatory publisher unavailable",
        adoptUnassignedLegacy: false,
      },
    },
  );
  expect(r.status()).toBe(412);
  expect(
    call(["lifecycle-inspect", "--local-sql-certificate-exception"]),
  ).toEqual(before);
  record("uc032-read-assign-failclosed", {
    progress: 200,
    assignment: 412,
    mutation: false,
    reason: "STALE_SEED_TASK_VERSION",
  });
});

test("FS012 assigned source header independently enforces recipient OWN and branch, renders without monitor widening", async ({
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

test("FS012 Create rejects unauthorized writes, invalid contracts and missing CSRF without persistence", async ({
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

async function createTask(c: BrowserContext) {
  const proof = call([
    "lifecycle-inspect",
    "--local-sql-certificate-exception",
  ]);
  const room = proof.rooms.find(
    (r: { roomId: number }) =>
      !proof.tasks.some((t: { roomId: number }) => t.roomId === r.roomId),
  );
  expect(room).toBeTruthy();
  const opt = await (
    await c.request.get(
      `/api/branches/${fixture.branchId}/cleaning-task-options?roomId=${room.roomId}`,
    )
  ).json();
  expect(opt.cleaningItems.length).toBe(opt.cleaningItemCount);
  const r = await post(c, `/api/branches/${fixture.branchId}/cleaning-tasks`, {
    roomId: room.roomId,
    cleaningTypeId: opt.cleaningTypes[0].id,
    priorityId: opt.priorities[0].id,
    dueAt: new Date(Date.now() + 3600000).toISOString(),
  });
  expect(r.status()).toBe(201);
  return { task: await r.json(), tag: r.headers().etag };
}
const inspectNotification = () =>
  call(["candidate-inspect", "--local-sql-certificate-exception"]);
const mode = (name: string, publication?: string) =>
  call([
    `candidate-${name}`,
    "--local-sql-certificate-exception",
    ...(publication ? [`--publication=${publication}`] : []),
  ]);
async function assign(
  c: BrowserContext,
  id: number,
  tag: string,
  key: string,
  data: object,
) {
  const csrf = await (await c.request.get("/api/auth/csrf")).json();
  return c.request.post(
    `/api/branches/${fixture.branchId}/cleaning-tasks/${id}/assign`,
    {
      headers: {
        "X-CSRF-TOKEN": csrf.csrfToken,
        "If-Match": tag,
        "Idempotency-Key": key,
      },
      data,
    },
  );
}
function command() {
  return {
    assigneeId: accounts.owner.userId,
    dueAt: new Date(Date.now() + 3600000).toISOString(),
    reason: "FS012 SQL acceptance",
    adoptUnassignedLegacy: false,
  };
}

test("FS012 real React assignment commits mandatory records, durable replay and recipient inbox delivery", async ({
  page,
  browser,
}) => {
  await login(page.context(), "admin");
  const n = await createTask(page.context());
  const before = inspectNotification();
  await page.goto(
    `/cleaning/assignment?branchId=${fixture.branchId}&taskId=${n.task.id}`,
  );
  await expect(page.locator("select[name=assigneeId]")).toBeVisible();
  await page
    .locator("select[name=assigneeId]")
    .selectOption(String(accounts.owner.userId));
  await page
    .locator("input[name=dueAt]")
    .fill(new Date(Date.now() + 3600000).toISOString());
  await page
    .locator("input[name=reason]")
    .fill("FS012 real browser assignment");
  const pending = page.waitForResponse(
    (r) =>
      r.url().endsWith(`/cleaning-tasks/${n.task.id}/assign`) &&
      r.request().method() === "POST",
  );
  await page.locator("form button[type=button]").click();
  const response = await pending;
  expect(response.status()).toBe(200);
  const result = await response.json();
  expect(result.notification).toBe("PENDING_DELIVERY");
  await expect(
    page.getByText(
      `Assignment ${result.assignmentId} committed. Notification pending delivery.`,
    ),
  ).toBeVisible();
  const request = response.request(),
    payload = request.postDataJSON(),
    headers = request.headers();
  const proof = inspectNotification();
  expect(proof.operations.length).toBe(before.operations.length + 1);
  const pub = proof.publications.find(
    (p: any) => p.assignmentId === result.assignmentId,
  );
  expect(pub).toBeTruthy();
  const facts = JSON.parse(pub.payloadJson);
  expect(facts.assigneeUserId).toBe(accounts.owner.userId);
  expect(facts.actorId).toBe(accounts.admin.userId);
  expect(facts.taskId).toBe(n.task.id);
  expect(facts.taskCycleId).toBe(n.task.id);
  const restart = await browser.newContext();
  try {
    await login(restart, "admin");
    const replay = await assign(
      restart,
      n.task.id,
      headers["if-match"],
      headers["idempotency-key"],
      payload,
    );
    expect(replay.status()).toBe(200);
    expect(await replay.json()).toEqual(result);
  } finally {
    await restart.close();
  }
  expect(
    (
      await assign(
        page.context(),
        n.task.id,
        headers["if-match"],
        headers["idempotency-key"],
        { ...payload, reason: "changed command" },
      )
    ).status(),
  ).toBe(409);
  expect(
    (
      await assign(
        page.context(),
        n.task.id,
        headers["if-match"],
        crypto.randomUUID(),
        payload,
      )
    ).status(),
  ).toBe(412);
  expect(inspectNotification()).toEqual(proof);
  expect(mode("deliver", result.publicationId).status).toBe("DELIVERED");
  const delivered = inspectNotification();
  const intent = delivered.intents.find(
    (x: any) => x.publicationId === result.publicationId,
  );
  expect(intent.status).toBe("DELIVERED");
  expect(intent.userId).toBe(accounts.owner.userId);
  expect(
    delivered.inbox.some(
      (x: any) =>
        x.notificationId === intent.inboxNotificationId &&
        x.userId === accounts.owner.userId,
    ),
  ).toBeTruthy();
  await page.context().clearCookies();
  await login(page.context(), "owner");
  expect(
    (
      await page.request.get(
        `/api/branches/${fixture.branchId}/cleaning-tasks/${n.task.id}?assignmentId=${result.assignmentId}`,
      )
    ).status(),
  ).toBe(200);
  const inbox = await page.request.get(
    `/api/branches/${fixture.branchId}/cleaning-assignment-notifications`,
  );
  expect(inbox.status()).toBe(200);
  expect(
    (await inbox.json()).some(
      (x: any) => x.assignmentId === result.assignmentId,
    ),
  ).toBeTruthy();
  const other = await browser.newContext();
  try {
    await login(other, "other");
    const hidden = await other.request.get(
      `/api/branches/${fixture.branchId}/cleaning-assignment-notifications`,
    );
    expect(hidden.status()).toBe(200);
    expect(
      (await hidden.json()).some(
        (x: any) => x.assignmentId === result.assignmentId,
      ),
    ).toBe(false);
  } finally {
    await other.close();
  }
  record("real-assignment-replay-delivery", {
    assignmentId: result.assignmentId,
    mandatoryStage: true,
    restartReplay: 200,
    conflictingReplay: 409,
    newKeyStale: 412,
    recipientOnly: true,
    inboxDelivered: true,
    acceptedA01: false,
  });
});

test("FS012 concurrent same-key and competing assignments, staging failure rollback, durable retry after restart", async ({
  page,
}) => {
  await login(page.context(), "admin");
  const n = await createTask(page.context()),
    key = crypto.randomUUID(),
    body = command();
  const pair = await Promise.all([
    assign(page.context(), n.task.id, n.tag, key, body),
    assign(page.context(), n.task.id, n.tag, key, body),
  ]);
  expect(pair.map((x) => x.status())).toEqual([200, 200]);
  const pairResult = await pair[0].json();
  expect(pairResult).toEqual(await pair[1].json());
  const competing = await createTask(page.context());
  const race = await Promise.all([
    assign(
      page.context(),
      competing.task.id,
      competing.tag,
      crypto.randomUUID(),
      command(),
    ),
    assign(
      page.context(),
      competing.task.id,
      competing.tag,
      crypto.randomUUID(),
      command(),
    ),
  ]);
  expect(race.map((x) => x.status()).sort()).toEqual([200, 412]);
  const competingResult = await race.find((x) => x.status() === 200)!.json();
  const rollback = await createTask(page.context()),
    pre = inspectNotification(),
    sourceBefore = call([
      "lifecycle-inspect",
      "--local-sql-certificate-exception",
    ]);
  mode("stage-fault-on");
  try {
    expect(
      (
        await assign(
          page.context(),
          rollback.task.id,
          rollback.tag,
          crypto.randomUUID(),
          command(),
        )
      ).status(),
    ).toBe(503);
  } finally {
    mode("stage-fault-off");
  }
  expect(inspectNotification()).toEqual(pre);
  expect(
    call(["lifecycle-inspect", "--local-sql-certificate-exception"]),
  ).toEqual(sourceBefore);
  mode("inbox-fault-on");
  try {
    expect(mode("deliver", pairResult.publicationId).status).toBe(
      "RETRY_SCHEDULED",
    );
  } finally {
    mode("inbox-fault-off");
  }
  const retry = inspectNotification();
  expect(
    retry.intents.some(
      (x: any) =>
        x.failureCode === "TRANSIENT_DELIVERY_ERROR" && x.status === "PENDING",
    ),
  ).toBeTruthy();
  mode("retry-now", pairResult.publicationId);
  expect(mode("deliver", pairResult.publicationId).status).toBe("DELIVERED");
  expect(mode("claim-only", competingResult.publicationId).status).toBe(
    "CLAIM_PERSISTED",
  );
  mode("extend-claim", competingResult.publicationId);
  expect(mode("deliver", competingResult.publicationId).status).toBe("EMPTY");
  mode("expire-claim", competingResult.publicationId);
  expect(mode("deliver", competingResult.publicationId).status).toBe(
    "DELIVERED",
  );
  const after = inspectNotification();
  expect(after.operations.length).toBe(pre.operations.length);
  record("assignment-concurrency-rollback-retry", {
    sameKey: [200, 200],
    competing: [200, 412],
    stageFaultRollback: true,
    deliveryFailureKeepsAssignment: true,
    restartRetry: true,
    deadlockAccepted: false,
  });
});

test("FS012 revoked recipient cannot receive source data; actor replay reauthorizes; persisted guard rejects missing records", async ({
  page,
}) => {
  await login(page.context(), "admin");
  const n = await createTask(page.context()),
    key = crypto.randomUUID(),
    body = command();
  const r = await assign(page.context(), n.task.id, n.tag, key, body);
  expect(r.status()).toBe(200);
  const assignedResult = await r.json();
  mode("recipient-revoke");
  try {
    expect(mode("deliver", assignedResult.publicationId).status).toBe(
      "SOURCE_READ_DENIED",
    );
    const denied = await page.context().browser()!.newContext();
    try {
      await login(denied, "owner");
      expect(
        (
          await denied.request.get(
            `/api/branches/${fixture.branchId}/cleaning-assignment-notifications`,
          )
        ).status(),
      ).toBe(403);
    } finally {
      await denied.close();
    }
  } finally {
    mode("recipient-restore");
  }
  const state = inspectNotification(),
    result = await r.json();
  const intent = state.intents.find(
    (x: any) => x.publicationId === result.publicationId,
  );
  expect(intent.status).toBe("TERMINAL");
  expect(intent.inboxNotificationId).toBeNull();
  mode("actor-revoke");
  try {
    expect(
      (await assign(page.context(), n.task.id, n.tag, key, body)).status(),
    ).toBe(403);
  } finally {
    mode("actor-restore");
  }
  expect(inspectNotification()).toEqual(state);
  const guard = mode("guard-proof");
  expect(guard.cases.length).toBe(7);
  expect(guard.rawExternalCommitInterception).toBe(false);
  record("source-security-and-scoped-guard", {
    revokedRecipient: "SOURCE_READ_DENIED",
    revokedActorReplay: 403,
    unauthorizedInboxRows: 0,
    guard,
  });
});

test("FS012 blocked room Create preserves legacy state, keeps effective block and exposes actual item preview", async ({
  page,
}) => {
  await login(page.context(), "admin");
  const room = mode("block-unused-room");
  const opts = await (
    await page.request.get(
      `/api/branches/${fixture.branchId}/cleaning-task-options?roomId=${room.roomId}`,
    )
  ).json();
  expect(opts.cleaningItems.length).toBe(2);
  await page.goto(
    `/cleaning/tasks/new?branchId=${fixture.branchId}&roomId=${room.roomId}`,
  );
  await expect(
    page.getByText(opts.cleaningItems[0].label, { exact: false }),
  ).toBeVisible();
  await page
    .locator("input[name=dueAt]")
    .fill(new Date(Date.now() + 3600000).toISOString());
  const pending = page.waitForResponse(
    (r) =>
      r.url().endsWith(`/api/branches/${fixture.branchId}/cleaning-tasks`) &&
      r.request().method() === "POST",
  );
  await page
    .getByRole("button", { name: "Save and Assign", exact: true })
    .click();
  const response = await pending;
  expect(response.status()).toBe(201);
  const created = await response.json();
  expect(created.hasEffectiveBlock).toBe(true);
  expect(created.readiness).toBe("PENDING_CLEANING");
  expect(created.itemsInitialized).toBe(false);
  await expect(page).toHaveURL(
    new RegExp(
      `/cleaning/assignment\\?branchId=${fixture.branchId}&taskId=${created.id}`,
    ),
  );
  const proof = call([
    "lifecycle-inspect",
    "--local-sql-certificate-exception",
  ]);
  expect(
    proof.rooms.find((x: any) => x.roomId === room.roomId).operationalStatus,
  ).toBe(room.operationalStatus);
  expect(proof.events.find((x: any) => x.taskId === created.id).blocked).toBe(
    true,
  );
  const duplicate = await (
    await page.request.get(
      `/api/branches/${fixture.branchId}/cleaning-task-options?roomId=${room.roomId}`,
    )
  ).json();
  expect(duplicate.existingTaskId).toBe(created.id);
  await page.goto(
    `/cleaning/tasks/new?branchId=${fixture.branchId}&roomId=${room.roomId}`,
  );
  await expect(
    page.getByRole("link", { name: "Open existing cleaning task" }),
  ).toHaveAttribute("href", new RegExp(`/cleaning/tasks/${created.id}\\?`));
  record("uc028-blocked-preview-continuation", {
    legacyUnchanged: true,
    effectiveBlock: true,
    expectedReleaseDoesNotRelease: true,
    readiness: "PENDING_CLEANING",
    snapshotNotFrozen: true,
    saveAndAssignNavigation: true,
    existingTaskLink: true,
  });
});

test("FS012 persisted recipient inactivity, lost branch access and superseded assignment never create inbox rows", async ({
  page,
}) => {
  await login(page.context(), "admin");
  const results = [];
  for (const [mutate, restore, terminal] of [
    ["recipient-inactive", "recipient-active", "RECIPIENT_INACTIVE"],
    [
      "recipient-branch-revoke",
      "recipient-branch-restore",
      "RECIPIENT_OUT_OF_SCOPE",
    ],
    ["supersede-latest", null, "SUPERSEDED"],
  ]) {
    const n = await createTask(page.context());
    const r = await assign(
      page.context(),
      n.task.id,
      n.tag,
      crypto.randomUUID(),
      command(),
    );
    expect(r.status()).toBe(200);
    const result = await r.json();
    mode(mutate!, result.publicationId);
    try {
      expect(mode("deliver", result.publicationId).status).toBe(terminal);
    } finally {
      if (restore) mode(restore);
    }
    const proof = inspectNotification(),
      intent = proof.intents.find(
        (x: any) => x.publicationId === result.publicationId,
      );
    expect(intent.status).toBe("TERMINAL");
    expect(intent.inboxNotificationId).toBeNull();
    results.push(terminal);
  }
  const parity = mode("model-parity");
  expect(parity.rows.length).toBe(3);
  record("terminal-source-policy-and-model", {
    results,
    parity,
    unauthorizedInboxRows: 0,
  });
});

test("FS012 real UC032 PUT retains IN_PROGRESS at 100 percent and enforces ownership and stale tag", async ({
  page,
}) => {
  await login(page.context(), "owner");
  const url = `/api/branches/${fixture.branchId}/cleaning-tasks/${fixture.taskId}/progress`;
  const before = await page.request.get(url);
  expect(before.status()).toBe(200);
  const data = await before.json(),
    tag = before.headers().etag;
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  const body = {
    percent: 100,
    items: data.items.map((x: any) => ({
      id: x.id,
      done: true,
      note: "FS012 UC032 regression",
    })),
    note: "FS012 real SQL UC032",
  };
  const saved = await page.request.put(url, {
    headers: { "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag },
    data: body,
  });
  expect(saved.status()).toBe(200);
  const result = await saved.json();
  expect(result.state).toBe("IN_PROGRESS");
  expect(result.percent).toBe(100);
  const fresh = await (await page.request.get("/api/auth/csrf")).json();
  expect(
    (
      await page.request.put(url, {
        headers: { "X-CSRF-TOKEN": fresh.csrfToken, "If-Match": tag },
        data: body,
      })
    ).status(),
  ).toBe(412);
  const proof = call(["persistence", "--local-sql-certificate-exception"]);
  expect(proof.status).toBe("IN_PROGRESS");
  expect(proof.ownerIsAuthor).toBe(true);
  expect(proof.auditMatches).toBe(true);
  await page.context().clearCookies();
  await login(page.context(), "other");
  const other = await (await page.request.get("/api/auth/csrf")).json();
  expect(
    (
      await page.request.put(url, {
        headers: {
          "X-CSRF-TOKEN": other.csrfToken,
          "If-Match": saved.headers().etag,
        },
        data: body,
      })
    ).status(),
  ).toBe(403);
  record("uc032-real-write-regression", {
    read: 200,
    put: 200,
    percent: 100,
    state: "IN_PROGRESS",
    stale: 412,
    foreignOwner: 403,
    sqlPersistence: true,
    auditIdentity: true,
  });
});

test("FS012 assignment authority rejects anonymous, forbidden, foreign branch and invalid key without mutation", async ({
  page,
  browser,
}) => {
  await login(page.context(), "admin");
  const source = call([
      "lifecycle-inspect",
      "--local-sql-certificate-exception",
    ]),
    notification = inspectNotification();
  const task = source.tasks.find(
    (x: any) =>
      x.status === "PENDING" &&
      !source.assignments.some(
        (a: any) => a.cleaningTaskId === x.cleaningTaskId,
      ),
  );
  expect(task).toBeTruthy();
  const options = await page.request.get(
    `/api/branches/${fixture.branchId}/cleaning-tasks/${task.cleaningTaskId}/assignment-options`,
  );
  expect(options.status()).toBe(200);
  const tag = options.headers().etag,
    body = command();
  const anon = await browser.newContext();
  try {
    expect(
      (
        await assign(anon, task.cleaningTaskId, tag, crypto.randomUUID(), body)
      ).status(),
    ).toBe(401);
  } finally {
    await anon.close();
  }
  expect(
    (
      await assign(
        page.context(),
        task.cleaningTaskId,
        tag,
        "invalid-guid",
        body,
      )
    ).status(),
  ).toBe(400);
  const csrf = await (await page.request.get("/api/auth/csrf")).json();
  expect(
    (
      await page.request.post(
        `/api/branches/${fixture.foreignBranchId}/cleaning-tasks/${task.cleaningTaskId}/assign`,
        {
          headers: {
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": tag,
            "Idempotency-Key": crypto.randomUUID(),
          },
          data: body,
        },
      )
    ).status(),
  ).toBe(403);
  await page.context().clearCookies();
  await login(page.context(), "owner");
  expect(
    (
      await assign(
        page.context(),
        task.cleaningTaskId,
        tag,
        crypto.randomUUID(),
        body,
      )
    ).status(),
  ).toBe(403);
  expect(
    call(["lifecycle-inspect", "--local-sql-certificate-exception"]),
  ).toEqual(source);
  expect(inspectNotification()).toEqual(notification);
  record("assignment-write-authority", {
    anonymous: 401,
    invalidKey: 400,
    foreignBranch: 403,
    forbiddenActor: 403,
    mutation: false,
  });
});
