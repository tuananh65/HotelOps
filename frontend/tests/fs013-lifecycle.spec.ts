import { test, expect, type BrowserContext } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
const root = path.resolve(".."),
  dir = path.join(root, "scripts/verification/FS013");
const helper = path.join(dir, "bin/Debug/net10.0/FS013.Verification.dll");
const fixture = JSON.parse(
  fs
    .readFileSync(path.join(dir, ".private/fixture.json"), "utf8")
    .replace(/^\uFEFF/, ""),
);
if (
  fixture.database !== "HotelOps_Canonical_20261106_f130001_Fresh" ||
  fixture.owner !== "DEV1_FS013" ||
  process.env.FS013_LIVE_ENABLED !== "APPROVED_FS013_LOCAL_TRIAL"
)
  throw Error("FS013_SCOPE_REQUIRED");
function call(mode: string, extra: string[] = []) {
  const r = spawnSync(
    "dotnet",
    [
      helper,
      mode,
      ...(mode === "credentials-ipc"
        ? ["--private-ipc"]
        : ["--local-sql-certificate-exception"]),
      ...extra,
    ],
    { encoding: "utf8", windowsHide: true, timeout: 60000 },
  );
  if (r.status !== 0)
    throw Error(
      "PRIVATE_HELPER_FAILED_" + mode + "_" + r.status + "_" + r.error?.name,
    );
  return JSON.parse(r.stdout.replace(/^\uFEFF/, ""));
}
const accounts = call("credentials-ipc");
const record = (name: string, data: unknown) => {
  const file = path.join(root, "docs/fs013/LIFECYCLE_LIVE_RESULTS.json");
  const prior = fs.existsSync(file)
    ? JSON.parse(fs.readFileSync(file, "utf8"))
    : null;
  const checks =
    prior?.runId === process.env.FS013_LIFECYCLE_RUN_ID ? prior.checks : [];
  checks.push({ name, data });
  fs.writeFileSync(
    file,
    JSON.stringify(
      {
        level: "REAL_AUTH_HTTPS_API_SQL",
        runId: process.env.FS013_LIFECYCLE_RUN_ID,
        checks,
        testerAcceptance: false,
      },
      null,
      2,
    ) + "\n",
  );
};
test.afterEach(async ({}, info) => record(info.title, { status: info.status }));
async function csrf(c: BrowserContext) {
  return (await (await c.request.get("/api/auth/csrf")).json()).csrfToken;
}
async function login(c: BrowserContext, role: string) {
  const r = await c.request.post("/api/auth/login", {
    headers: { "X-CSRF-TOKEN": await csrf(c) },
    data: {
      username: accounts[role].username,
      password: accounts[role].password,
    },
  });
  expect(r.status()).toBe(200);
}
const url = (id: number, action: string) =>
  `/api/branches/${fixture.branchId}/cleaning-tasks/${id}/${action}`;
async function post(
  c: BrowserContext,
  id: number,
  action: string,
  tag: string,
  body: unknown,
  key?: string,
) {
  return c.request.post(url(id, action), {
    headers: {
      "X-CSRF-TOKEN": await csrf(c),
      "If-Match": tag,
      ...(key ? { "Idempotency-Key": key } : {}),
    },
    data: body,
  });
}
async function createAssigned(c: BrowserContext) {
  const source = call("lifecycle-inspect"),
    room = source.rooms.find(
      (r: any) => !source.tasks.some((t: any) => t.roomId === r.roomId),
    );
  expect(room).toBeTruthy();
  const options = await (
    await c.request.get(
      `/api/branches/${fixture.branchId}/cleaning-task-options?roomId=${room.roomId}`,
    )
  ).json();
  const created = await c.request.post(
    `/api/branches/${fixture.branchId}/cleaning-tasks`,
    {
      headers: { "X-CSRF-TOKEN": await csrf(c) },
      data: {
        roomId: room.roomId,
        cleaningTypeId: options.cleaningTypes[0].id,
        priorityId: options.priorities[0].id,
        dueAt: new Date(Date.now() + 3600000).toISOString(),
      },
    },
  );
  expect(created.status()).toBe(201);
  const task = await created.json();
  const assigned = await post(
    c,
    task.id,
    "assign",
    created.headers().etag,
    {
      assigneeId: accounts.owner.userId,
      dueAt: new Date(Date.now() + 3600000).toISOString(),
      reason: "FS013 lifecycle first assignment",
      adoptUnassignedLegacy: false,
    },
    crypto.randomUUID(),
  );
  expect(assigned.status()).toBe(200);
  return { id: task.id as number, tag: assigned.headers().etag };
}

// Explicit forward recovery after a preserved failed attempt. Never reset a task,
// delete history or clear an interval directly to make a test pass.
test.beforeAll(async ({ browser }) => {
  record("suite_started", {
    forwardRecovery: process.env.FS013_REUSE_WORKDAY_FIXTURE === "FORWARD_ONLY",
  });
  if (process.env.FS013_REUSE_WORKDAY_FIXTURE !== "FORWARD_ONLY") return;
  const context = await browser.newContext();
  try {
    await login(context, "admin");
    const proof = call("candidate-lifecycle-inspect");
    for (const interval of proof.intervals.filter(
      (i: any) => i.closedAt === null,
    )) {
      const current = await context.request.get(
        url(interval.workItemId, "execution"),
      );
      expect(current.status()).toBe(200);
      const next =
        interval.userId === accounts.owner.userId
          ? accounts.other.userId
          : accounts.owner.userId;
      const moved = await post(
        context,
        interval.workItemId,
        "reassign",
        current.headers().etag,
        {
          assigneeId: next,
          dueAt: new Date(Date.now() + 3600000).toISOString(),
          reason: "FS013 forward recovery after preserved failed run",
        },
        crypto.randomUUID(),
      );
      expect(moved.status()).toBe(200);
      record("forward_reassignment_releases_prior_interval", {
        taskId: interval.workItemId,
        intervalId: interval.intervalId,
        status: 200,
        reset: false,
      });
    }
  } finally {
    await context.close();
  }
});
test("FS013 Start and Reassign preserve real ownership, intervals, historical replay and two mandatory recipients", async ({
  page,
  browser,
}) => {
  const admin = page.context(),
    owner = await browser.newContext(),
    other = await browser.newContext();
  try {
    await login(admin, "admin");
    await login(owner, "owner");
    await login(other, "other");
    call("candidate-block-unused-room"); // Synthetic effective block; past expected release is not actual release.
    const source = await createAssigned(admin);
    if (process.env.FS013_REUSE_WORKDAY_FIXTURE === "FORWARD_ONLY") {
      record(
        "workday_fixture_forward_end",
        call("candidate-end-fixture-workdays"),
      );
    }
    const beforeNoDay = call("candidate-lifecycle-inspect");
    const noDay = await post(owner, source.id, "start", source.tag, {
      resolveBlocker: false,
    });
    expect(noDay.status()).toBe(409);
    expect((await noDay.json()).title).toBe("TIME_WORKDAY_REQUIRED");
    expect(call("candidate-lifecycle-inspect")).toEqual(beforeNoDay);
    record("no_working_day_is_atomic_409", { noMutation: true, status: 409 });
    call("candidate-start-fixture-workdays"); // SQL foundation fixture, never impersonates HTTP Workday acceptance.
    const wrong = await post(other, source.id, "start", source.tag, {
      resolveBlocker: false,
    });
    expect(wrong.status()).toBe(403);
    await page.context().clearCookies();
    await login(page.context(), "owner");
    await page.goto(
      `/cleaning/tasks/${source.id}?branchId=${fixture.branchId}&execution=1`,
    );
    await expect(
      page.getByRole("button", { name: "Bắt đầu dọn phòng", exact: true }),
    ).toBeEnabled();
    const startedResponse = page.waitForResponse(
      (r) =>
        r.url().endsWith(url(source.id, "start")) &&
        r.request().method() === "POST",
    );
    await page
      .getByRole("button", { name: "Bắt đầu dọn phòng", exact: true })
      .click();
    const started = await startedResponse;
    expect(started.status()).toBe(200);
    const first = await started.json();
    expect(first.state).toBe("IN_PROGRESS");
    expect(first.intervalId).toBeGreaterThan(0);
    expect(first.hasEffectiveBlock).toBe(true);
    record("start_preserves_effective_room_block", {
      hasEffectiveBlock: true,
      expectedReleaseDoesNotRelease: true,
    });
    expect(
      (
        await post(owner, source.id, "start", source.tag, {
          resolveBlocker: false,
        })
      ).status(),
    ).toBe(412);
    const snapshot = call("candidate-lifecycle-inspect");
    const oldInterval = snapshot.intervals.find(
      (i: any) => i.intervalId === first.intervalId,
    );
    expect(oldInterval.closedAt).toBeNull();
    await admin.clearCookies();
    await login(admin, "admin");
    const secondTask = await createAssigned(admin);
    const beforeOverlap = call("candidate-lifecycle-inspect");
    const overlap = await post(owner, secondTask.id, "start", secondTask.tag, {
      resolveBlocker: false,
    });
    expect(overlap.status()).toBe(409);
    expect((await overlap.json()).title).toBe("TIME_INTERVAL_ALREADY_OPEN");
    expect(call("candidate-lifecycle-inspect")).toEqual(beforeOverlap);
    record("second_open_interval_rejected", { status: 409, noMutation: true });
    const next = {
      assigneeId: accounts.other.userId,
      dueAt: new Date(Date.now() + 3600000).toISOString(),
      reason: "FS013 reassignment staffing change",
    };
    const key = crypto.randomUUID(),
      tag = started.headers().etag;
    const reassigned = await post(admin, source.id, "reassign", tag, next, key);
    expect(reassigned.status()).toBe(200);
    const result = await reassigned.json();
    expect(result.contract).toBe("PROVISIONAL_NOT_ACCEPTED");
    expect(result.previousAssigneeId).toBe(accounts.owner.userId);
    expect(result.assigneeId).toBe(accounts.other.userId);
    let proof = call("candidate-lifecycle-inspect");
    expect(proof.notices.filter((i: any) => i.operation === key)).toHaveLength(
      2,
    );
    const closed = proof.intervals.find(
      (i: any) => i.intervalId === first.intervalId,
    );
    expect(closed.closedAt).not.toBeNull();
    expect(
      proof.assignments.find(
        (a: any) => a.assignmentId === result.previousAssignmentId,
      ).endedAt,
    ).toBe(closed.closedAt);
    expect(
      (
        await post(owner, source.id, "start", reassigned.headers().etag, {
          resolveBlocker: false,
        })
      ).status(),
    ).toBe(403);
    const newStart = await post(
      other,
      source.id,
      "start",
      reassigned.headers().etag,
      { resolveBlocker: false },
    );
    expect(newStart.status()).toBe(200);
    expect((await newStart.json()).actualStartAtUtc).toBe(
      first.actualStartAtUtc,
    );
    const beforeReplay = call("candidate-lifecycle-inspect");
    const replay = await post(admin, source.id, "reassign", tag, next, key);
    expect(replay.status()).toBe(200);
    expect(await replay.json()).toEqual(result);
    expect(call("candidate-lifecycle-inspect")).toEqual(beforeReplay);
    expect(
      (
        await post(
          admin,
          source.id,
          "reassign",
          tag,
          { ...next, reason: "Different immutable command" },
          key,
        )
      ).status(),
    ).toBe(409);
    // Controlled delivery failure retains source + intents; always clear the scratch-only fault.
    const actor = accounts.admin.userId;
    call("candidate-inbox-fault-on");
    try {
      expect(
        call("candidate-reassign-deliver", [
          `--operation=${key}`,
          `--actor=${actor}`,
        ]).status,
      ).toBe("RETRY_SCHEDULED");
    } finally {
      call("candidate-inbox-fault-off");
    }
    await new Promise((r) => setTimeout(r, 5500));
    expect(
      call("candidate-reassign-deliver", [
        `--operation=${key}`,
        `--actor=${actor}`,
      ]).status,
    ).toBe("DELIVERED");
    expect(
      call("candidate-reassign-deliver", [
        `--operation=${key}`,
        `--actor=${actor}`,
      ]).status,
    ).toBe("DELIVERED");
    expect(
      call("candidate-reassign-deliver", [
        `--operation=${key}`,
        `--actor=${actor}`,
      ]).status,
    ).toBe("EMPTY");
    proof = call("candidate-lifecycle-inspect");
    expect(
      proof.notices.filter(
        (i: any) => i.operation === key && i.status === "DELIVERED",
      ),
    ).toHaveLength(2);
    const oldReceipt = proof.delivered.find(
      (n: any) => n.operation === key && n.recipientKind === "PREVIOUS",
    );
    expect(oldReceipt).toBeTruthy();
    expect(oldReceipt.actionUrl).toBeNull();
    expect(oldReceipt.sourceId).toBeNull();
    expect(
      (await owner.request.get(url(source.id, "execution"))).status(),
    ).toBe(403);
    record("previous_recipient_minimal_receipt", {
      actionUrl: null,
      sourceId: null,
      oldOwnerRead: 403,
    });
    const backTag = newStart.headers().etag;
    const back = await post(
      admin,
      source.id,
      "reassign",
      backTag,
      { ...next, assigneeId: accounts.owner.userId },
      crypto.randomUUID(),
    );
    expect(back.status()).toBe(200);
    record("start_reassign_replay_two_recipients", {
      firstStart: 200,
      wrongOwner: 403,
      stale: 412,
      reassign: 200,
      replay: 200,
      keyConflict: 409,
      previousIntervalClosed: true,
      actualStartPreserved: true,
      notificationRetry: true,
      deliveredRecipients: 2,
    });
  } finally {
    await owner.close();
    await other.close();
  }
});
test("FS013 Reassign mandatory staging failure rolls source interval and assignment back", async ({
  page,
  browser,
}) => {
  const admin = page.context(),
    owner = await browser.newContext();
  try {
    await login(admin, "admin");
    await login(owner, "owner");
    const source = await createAssigned(admin);
    const started = await post(owner, source.id, "start", source.tag, {
      resolveBlocker: false,
    });
    expect(started.status()).toBe(200);
    const before = call("candidate-lifecycle-inspect");
    call("candidate-reassign-stage-fault-on");
    try {
      const r = await post(
        admin,
        source.id,
        "reassign",
        started.headers().etag,
        {
          assigneeId: accounts.other.userId,
          dueAt: new Date(Date.now() + 3600000).toISOString(),
          reason: "FS013 mandatory staging rollback",
        },
        crypto.randomUUID(),
      );
      expect(r.status()).toBe(503);
    } finally {
      call("candidate-reassign-stage-fault-off");
    }
    expect(call("candidate-lifecycle-inspect")).toEqual(before);
    record("reassign_staging_failure_rolls_interval_and_source_back", {
      status: 503,
      exactSnapshotUnchanged: true,
    });
    const close = await post(
      admin,
      source.id,
      "reassign",
      started.headers().etag,
      {
        assigneeId: accounts.other.userId,
        dueAt: new Date(Date.now() + 3600000).toISOString(),
        reason: "FS013 release owner interval safely",
      },
      crypto.randomUUID(),
    );
    expect(close.status()).toBe(200);
  } finally {
    await owner.close();
  }
});
test("FS013 concurrent Start versus Reassign and invalid authority never leave mismatched owner/time", async ({
  page,
  browser,
}) => {
  const admin = page.context(),
    owner = await browser.newContext(),
    other = await browser.newContext();
  try {
    await login(admin, "admin");
    await login(owner, "owner");
    await login(other, "other");
    const source = await createAssigned(admin);
    const command = {
      assigneeId: accounts.other.userId,
      dueAt: new Date(Date.now() + 3600000).toISOString(),
      reason: "FS013 concurrent reassignment",
    };
    const before = call("candidate-lifecycle-inspect");
    expect(
      (
        await post(
          admin,
          source.id,
          "reassign",
          source.tag,
          { ...command, reason: "short" },
          crypto.randomUUID(),
        )
      ).status(),
    ).toBe(400);
    expect(
      (
        await post(
          admin,
          source.id,
          "reassign",
          source.tag,
          { ...command, actorId: 1 },
          crypto.randomUUID(),
        )
      ).status(),
    ).toBe(400);
    expect(
      (
        await post(
          owner,
          source.id,
          "reassign",
          source.tag,
          command,
          crypto.randomUUID(),
        )
      ).status(),
    ).toBe(403);
    expect(call("candidate-lifecycle-inspect")).toEqual(before);
    const [a, b] = await Promise.all([
      post(owner, source.id, "start", source.tag, { resolveBlocker: false }),
      post(
        admin,
        source.id,
        "reassign",
        source.tag,
        command,
        crypto.randomUUID(),
      ),
    ]);
    expect([a.status(), b.status()].filter((s) => s === 200)).toHaveLength(1);
    expect(
      [a.status(), b.status()].every((s) => [200, 403, 412].includes(s)),
    ).toBeTruthy();
    const proof = call("candidate-lifecycle-inspect"),
      active = proof.assignments.filter(
        (x: any) => x.cleaningTaskId === source.id && x.endedAt === null,
      );
    expect(active).toHaveLength(1);
    const open = proof.intervals.filter(
      (x: any) => x.workItemId === source.id && x.closedAt === null,
    );
    if (a.status() === 200) {
      expect(open).toHaveLength(1);
      expect(open[0].userId).toBe(active[0].assigneeUserId);
    } else expect(open).toHaveLength(0);
    record("concurrent_start_reassign", {
      statuses: [a.status(), b.status()],
      activeAssignments: active.length,
      openIntervals: open.length,
      invalidReason: 400,
      serverField: 400,
      unauthorizedReassign: 403,
    });
  } finally {
    await owner.close();
    await other.close();
  }
});
