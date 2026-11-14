import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const b = await build({
  entryPoints: ["src/workday/shiftPlanApi.ts"],
  bundle: true,
  write: false,
  platform: "node",
  format: "esm",
});
const { createShiftPlanApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(b.outputFiles[0].text).toString("base64")
);
const drafts = [
  {
    employeeId: 8,
    branchId: 1,
    departmentId: 2,
    templateId: 10,
    shiftDate: "2028-02-29",
  },
];
const preview = {
  rows: [
    {
      index: 0,
      startUtc: "2028-02-29T08:00:00Z",
      endUtc: "2028-02-29T16:00:00Z",
      issues: [],
    },
  ],
  requiresServerRevalidation: true,
};
const secret = "PRIVATE-SHIFT-PLAN";
const json = (v, status = 200) =>
  new Response(JSON.stringify(v), {
    status,
    headers: { "Content-Type": "application/json" },
  });
function harness(
  reply = () => json(preview),
  bootstrap = () => json({ headerName: "X-CSRF-TOKEN", csrfToken: secret }),
  enabled = false,
) {
  const calls = [];
  return {
    calls,
    api: createShiftPlanApi(async (url, options) => {
      calls.push({ url, ...options });
      return url === "/api/auth/csrf" ? bootstrap() : reply();
    }, enabled),
  };
}
const signal = () => new AbortController().signal;
test("real preview transport exact scoped URL, CSRF and strict body", async () => {
  const h = harness();
  assert.deepEqual(await h.api.preview(drafts, signal()), preview);
  assert.equal(h.calls.length, 2);
  const post = h.calls[1];
  assert.equal(post.url, "/api/branches/1/departments/2/shift-plans/preview");
  assert.equal(post.method, "POST");
  assert.deepEqual(JSON.parse(post.body), {
    drafts: [{ employeeId: 8, templateId: 10, shiftDate: "2028-02-29" }],
  });
  assert.equal(post.headers["X-CSRF-TOKEN"], secret);
  for (const c of h.calls) {
    assert.equal(c.credentials, "include");
    assert.equal(c.cache, "no-store");
  }
});
test("Save default OFF sends no request", async () => {
  const h = harness();
  assert.equal((await h.api.save(drafts, signal())).outcome, "NOT_DISPATCHED");
  assert.equal(h.calls.length, 0);
});
test("Save confirmed only with exact counts", async () => {
  const h = harness(
    () =>
      json(
        {
          createdShifts: 1,
          createdAssignments: 1,
          notificationCount: 1,
          detail: secret,
        },
        201,
      ),
    undefined,
    true,
  );
  const r = await h.api.save(drafts, signal());
  assert.deepEqual(r, {
    ok: true,
    createdShifts: 1,
    createdAssignments: 1,
    notificationCount: 1,
  });
  assert.equal(h.calls[1].url, "/api/branches/1/departments/2/shift-plans");
  assert.equal(JSON.stringify(r).includes(secret), false);
});
for (const status of [401, 403, 503])
  test(
    "preflight " + status + " never sends POST and preserves status",
    async () => {
      const h = harness(
        undefined,
        () => json({ detail: secret }, status),
        true,
      );
      await assert.rejects(
        h.api.preview(drafts, signal()),
        (e) => e.status === status && !e.message.includes(secret),
      );
      assert.equal(h.calls.length, 1);
    },
  );
for (const status of [400, 401, 403, 500, 503])
  test(
    "preview denied/unavailable " + status + " sanitized, no retry",
    async () => {
      const h = harness(() => json({ detail: secret }, status));
      await assert.rejects(
        h.api.preview(drafts, signal()),
        (e) => e.status === status && !JSON.stringify(e).includes(secret),
      );
      assert.equal(h.calls.length, 2);
    },
  );
for (const value of [
  { ...preview, requiresServerRevalidation: false },
  { ...preview, rows: [] },
  {
    rows: [{ index: 0, startUtc: null, endUtc: null, issues: [99] }],
    requiresServerRevalidation: true,
  },
  {},
])
  test("malformed preview " + JSON.stringify(value), async () => {
    const h = harness(() => json(value));
    await assert.rejects(h.api.preview(drafts, signal()));
    assert.equal(h.calls.length, 2);
  });
for (const input of [
  [],
  [...drafts, { ...drafts[0], branchId: 9 }],
  [{ ...drafts[0], employeeId: 0 }],
  [{ ...drafts[0], shiftDate: "2027-02-29" }],
  Array(501).fill(drafts[0]),
])
  test(
    "invalid or mixed request never dispatched " + input.length,
    async () => {
      const h = harness();
      await assert.rejects(h.api.preview(input, signal()));
      assert.equal(h.calls.length, 0);
    },
  );
test("Save network uncertainty has no replay", async () => {
  const h = harness(
    () => {
      throw Error(secret);
    },
    undefined,
    true,
  );
  const r = await h.api.save(drafts, signal());
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.equal(h.calls.length, 2);
  assert.equal(JSON.stringify(r).includes(secret), false);
});
for (const value of [
  { createdShifts: 0, createdAssignments: 1, notificationCount: 1 },
  { createdShifts: 1, createdAssignments: 2, notificationCount: 1 },
  { createdShifts: 1, createdAssignments: 1, notificationCount: 0 },
])
  test(
    "Save incomplete receipt is unknown " + JSON.stringify(value),
    async () => {
      const h = harness(() => json(value, 201), undefined, true);
      assert.equal(
        (await h.api.save(drafts, signal())).outcome,
        "OUTCOME_UNKNOWN",
      );
    },
  );
test("concurrent preview and Save do not queue or overlap", async () => {
  let release;
  const wait = new Promise((r) => (release = r));
  const h = harness(
    async () => {
      await wait;
      return json(preview);
    },
    undefined,
    true,
  );
  const first = h.api.preview(drafts, signal());
  assert.equal((await h.api.save(drafts, signal())).outcome, "NOT_DISPATCHED");
  release();
  await first;
  assert.equal(h.calls.filter((c) => c.method === "POST").length, 1);
});
test("draft snapshot cannot be replaced while bootstrap awaits", async () => {
  let release;
  const wait = new Promise((r) => (release = r));
  const input = drafts.map((d) => ({ ...d }));
  const h = harness(undefined, async () => {
    await wait;
    return json({ headerName: "X-CSRF-TOKEN", csrfToken: secret });
  });
  const first = h.api.preview(input, signal());
  input[0].employeeId = 99;
  input[0].branchId = 99;
  release();
  await first;
  assert.equal(JSON.parse(h.calls[1].body).drafts[0].employeeId, 8);
  assert.ok(h.calls[1].url.includes("/branches/1/"));
});
test("cancelled preflight has no invented status", async () => {
  const h = harness(undefined, () => {
    throw Error(secret);
  });
  await assert.rejects(
    h.api.preview(drafts, signal()),
    (e) => e.status === undefined,
  );
  assert.equal(h.calls.length, 1);
});
for (const status of [401, 403, 503])
  test(`082 unreadable CSRF ${status} preserves preview status without dispatch`, async () => {
    const h = harness(undefined, () => {
      const r = json({ detail: secret }, status);
      r.text = async () => {
        throw Error(secret);
      };
      return r;
    });
    await assert.rejects(
      h.api.preview(drafts, signal()),
      (e) => e.status === status && !e.message.includes(secret),
    );
    assert.equal(h.calls.length, 1);
  });
for (const profile of ["network", "malformed", "unreadable-success"])
  test(`082 ${profile} bootstrap supplies no invented status`, async () => {
    const h = harness(undefined, () => {
      if (profile === "network") throw Error(secret);
      const r = json({});
      if (profile === "unreadable-success")
        r.text = async () => {
          throw Error(secret);
        };
      return r;
    });
    await assert.rejects(
      h.api.preview(drafts, signal()),
      (e) => e.status === undefined,
    );
    assert.equal(h.calls.length, 1);
  });
test("082 bootstrap failure status does not leak into subsequent operation", async () => {
  let i = 0;
  const h = harness(undefined, () => {
    if (i++) throw Error(secret);
    const r = json({}, 401);
    r.text = async () => {
      throw Error(secret);
    };
    return r;
  });
  await assert.rejects(
    h.api.preview(drafts, signal()),
    (e) => e.status === 401,
  );
  await assert.rejects(
    h.api.preview(drafts, signal()),
    (e) => e.status === undefined,
  );
  assert.equal(h.calls.length, 2);
});
