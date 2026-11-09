import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const compiled = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "ts",
    contents:
      "export * from './src/workday/scheduledWorkdayApi';export * from './src/workday/scheduledContracts';",
  },
  bundle: true,
  write: false,
  format: "esm",
});
const { createScheduledWorkdayApi, strongWorkdayTag } = await import(
  "data:text/javascript;base64," +
    Buffer.from(compiled.outputFiles[0].text).toString("base64")
);
const tag = '"AAAAAAAAAAE="';
const body = (end = false) => ({
  workdayId: 20,
  branchId: 1,
  departmentId: 12,
  workDate: "2026-11-08",
  shiftAssignmentId: 10,
  isUnscheduled: false,
  status: end ? "ENDED" : "WORKING",
  startedAtUtc: "2026-11-08T01:00:00Z",
  endedAtUtc: end ? "2026-11-08T09:00:00Z" : null,
  rowVersion: "AAAAAAAAAAE=",
});
const response = (v, status = 200) =>
  Response.json(v, { status, headers: { ETag: tag } });
const csrf = () =>
  response({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic-token" });
for (const op of ["start", "end"])
  test(op + " exact request and confirmed DTO", async () => {
    const calls = [];
    const api = createScheduledWorkdayApi(async (u, o) => {
      calls.push([u, o]);
      return u.endsWith("/csrf")
        ? csrf()
        : response(
            { ...body(op === "end"), secret: "SECRET" },
            op === "start" ? 201 : 200,
          );
    }, true);
    const r =
      op === "start"
        ? await api.start(1, 10, () => true)
        : await api.end(1, 20, 10, tag, () => true);
    assert.equal(r.outcome, "CONFIRMED");
    assert.ok(!JSON.stringify(r).includes("SECRET"));
    assert.equal(calls.length, 2);
    const [u, o] = calls[1];
    assert.equal(
      u,
      op === "start"
        ? "/api/branches/1/workdays"
        : "/api/branches/1/workdays/20/end",
    );
    assert.equal(o.method, "POST");
    assert.equal(o.credentials, "include");
    assert.equal(o.redirect, "error");
    assert.equal(new Headers(o.headers).get("X-CSRF-TOKEN"), "synthetic-token");
    assert.equal(
      new Headers(o.headers).get("If-Match"),
      op === "end" ? tag : null,
    );
    assert.deepEqual(
      JSON.parse(o.body),
      op === "start" ? { shiftAssignmentId: 10 } : { pauseAll: false },
    );
  });
test("default OFF sends zero CSRF and POST", async () => {
  let n = 0;
  const api = createScheduledWorkdayApi(async () => {
    n++;
    throw Error();
  });
  assert.equal((await api.start(1, 10, () => true)).error.reason, "DISABLED");
  assert.equal(
    (await api.end(1, 20, 10, tag, () => true)).error.reason,
    "DISABLED",
  );
  assert.equal(n, 0);
});
for (const [branch, id] of [
  [0, 10],
  [1, -1],
  [Number.MAX_SAFE_INTEGER + 1, 10],
])
  test("invalid start refs " + branch + "/" + id, async () => {
    let n = 0;
    assert.equal(
      (
        await createScheduledWorkdayApi(async () => {
          n++;
          throw Error();
        }, true).start(branch, id, () => true)
      ).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(n, 0);
  });
for (const v of ["W/" + tag, '"AAAAAAAAAAE"', '"' + "A".repeat(64) + '"', "*"])
  test("invalid end tag " + v, async () => {
    assert.equal(strongWorkdayTag(v), false);
    let n = 0;
    assert.equal(
      (
        await createScheduledWorkdayApi(async () => {
          n++;
          throw Error();
        }, true).end(1, 20, 10, v, () => true)
      ).outcome,
      "NOT_DISPATCHED",
    );
    assert.equal(n, 0);
  });
for (const status of [400, 401, 403, 409, 412, 503, 500, 404])
  test("sanitized failure " + status, async () => {
    let posts = 0;
    const api = createScheduledWorkdayApi(async (u) => {
      if (u.endsWith("/csrf")) return csrf();
      posts++;
      return response(
        { status, title: "SECRET", detail: "SECRET", traceId: "SECRET" },
        status,
      );
    }, true);
    const r = await api.start(1, 10, () => true);
    assert.equal(
      r.outcome,
      [400, 401, 403, 409, 412].includes(status)
        ? "REJECTED"
        : "OUTCOME_UNKNOWN",
    );
    assert.equal(r.error.status, status);
    assert.equal(posts, 1);
    assert.ok(!JSON.stringify(r).includes("SECRET"));
  });
for (const [name, change] of [
  ["wrong assignment", (v) => (v.shiftAssignmentId = 11)],
  ["wrong branch", (v) => (v.branchId = 2)],
  ["wrong state", (v) => (v.status = "ENDED")],
  ["unscheduled", (v) => (v.isUnscheduled = true)],
  ["wrong version", (v) => (v.rowVersion = "AAAAAAAAAAI=")],
])
  test("malformed success " + name, async () => {
    const v = body();
    change(v);
    const api = createScheduledWorkdayApi(
      async (u) => (u.endsWith("/csrf") ? csrf() : response(v, 201)),
      true,
    );
    assert.equal(
      (await api.start(1, 10, () => true)).outcome,
      "OUTCOME_UNKNOWN",
    );
  });
test("network after POST is unknown and no replay", async () => {
  let n = 0;
  const api = createScheduledWorkdayApi(async (u) => {
    if (u.endsWith("/csrf")) return csrf();
    n++;
    throw Error("SECRET");
  }, true);
  const r = await api.start(1, 10, () => true);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.equal(n, 1);
  assert.ok(!JSON.stringify(r).includes("SECRET"));
});
test("identity replacement during CSRF prevents POST", async () => {
  let valid = true,
    n = 0;
  const api = createScheduledWorkdayApi(async () => {
    n++;
    valid = false;
    return csrf();
  }, true);
  assert.equal((await api.start(1, 10, () => valid)).outcome, "NOT_DISPATCHED");
  assert.equal(n, 1);
});
test("late mutation response cannot confirm new identity", async () => {
  let valid = true;
  const api = createScheduledWorkdayApi(async (u) => {
    if (u.endsWith("/csrf")) return csrf();
    valid = false;
    return response(body(), 201);
  }, true);
  assert.equal(
    (await api.start(1, 10, () => valid)).outcome,
    "OUTCOME_UNKNOWN",
  );
});
test("malformed JSON cannot confirm start", async () => {
  const api = createScheduledWorkdayApi(
    async (u) =>
      u.endsWith("/csrf")
        ? csrf()
        : new Response("{", {
            status: 201,
            headers: { "Content-Type": "application/json" },
          }),
    true,
  );
  assert.equal((await api.start(1, 10, () => true)).outcome, "OUTCOME_UNKNOWN");
});
test("cancelled POST remains unknown, not a confirmed revoke", async () => {
  const c = new AbortController();
  const api = createScheduledWorkdayApi(async (u) => {
    if (u.endsWith("/csrf")) return csrf();
    c.abort();
    return response(body(), 201);
  }, true);
  assert.equal(
    (await api.start(1, 10, () => true, c.signal)).outcome,
    "OUTCOME_UNKNOWN",
  );
});
test("end rejects response for another workday", async () => {
  const api = createScheduledWorkdayApi(
    async (u) =>
      u.endsWith("/csrf") ? csrf() : response({ ...body(true), workdayId: 21 }),
    true,
  );
  assert.equal(
    (await api.end(1, 20, 10, tag, () => true)).outcome,
    "OUTCOME_UNKNOWN",
  );
});

for (const endedAtUtc of [
  "2026-02-30T09:00:00Z",
  "2027-02-29T09:00:00Z",
  "2026-04-31T09:00:00Z",
  "2026-11-08T24:00:00Z",
  "2026-11-08T09:00:00+14:01",
  "2026-11-08T09:00:00+15:00",
  "2026-11-08T09:00:00-15:00",
  "2026-11-08T09:00:00+14:30",
  "2026-11-08T09:00:00-14:30",
  "not-a-timestamp",
  "2025-12-31T23:59:59Z",
]) {
  test(
    "invalid calendar or clock cannot confirm End: " + endedAtUtc,
    async () => {
      const calls = [];
      const api = createScheduledWorkdayApi(async (url, options) => {
        calls.push([url, options]);
        return url.endsWith("/csrf")
          ? csrf()
          : response({
              ...body(true),
              startedAtUtc: "2026-01-01T00:00:00Z",
              endedAtUtc,
              secret: "SECRET",
            });
      }, true);
      const result = await api.end(1, 20, 10, tag, () => true);
      assert.deepEqual(result, {
        outcome: "OUTCOME_UNKNOWN",
        error: { reason: "INVALID_RESPONSE" },
      });
      assert.equal(
        calls.filter(([, options]) => options.method === "POST").length,
        1,
      );
      assert.equal(calls[1][0], "/api/branches/1/workdays/20/end");
      assert.equal(new Headers(calls[1][1].headers).get("If-Match"), tag);
      assert.ok(!JSON.stringify(result).includes("SECRET"));
    },
  );
}
for (const endedAtUtc of [
  "2028-02-29T09:00:00Z",
  "2026-11-08T09:00:00.1234567+00:00",
  "2026-11-08T23:00:00+14:00",
]) {
  test("valid DateTimeOffset End remains accepted: " + endedAtUtc, async () => {
    const result = await createScheduledWorkdayApi(
      async (url) =>
        url.endsWith("/csrf")
          ? csrf()
          : response({ ...body(true), endedAtUtc }),
      true,
    ).end(1, 20, 10, tag, () => true);
    assert.equal(result.outcome, "CONFIRMED");
    assert.equal(result.data.endedAtUtc, endedAtUtc);
  });
}

for (const phase of ["csrf", "post"]) {
  for (const firstOperation of ["start", "end"]) {
    for (const secondOperation of ["start", "end"]) {
      test(
        "client excludes overlapping " +
          firstOperation +
          "/" +
          secondOperation +
          " during " +
          phase,
        async () => {
          const calls = [],
            waiters = [];
          const api = createScheduledWorkdayApi(async (url, options) => {
            calls.push([url, options]);
            const isCsrf = url.endsWith("/csrf");
            const finish = () =>
              isCsrf
                ? csrf()
                : response(
                    body(url.endsWith("/end")),
                    url.endsWith("/end") ? 200 : 201,
                  );
            if ((phase === "csrf" && isCsrf) || (phase === "post" && !isCsrf))
              return new Promise((resolve) =>
                waiters.push(() => resolve(finish())),
              );
            return finish();
          }, true);
          const invoke = (operation) =>
            operation === "start"
              ? api.start(1, 10, () => true)
              : api.end(1, 20, 10, tag, () => true);
          const first = invoke(firstOperation);
          await new Promise(setImmediate);
          const second = invoke(secondOperation);
          await new Promise(setImmediate);
          waiters.splice(0).forEach((finish) => finish());
          const [firstResult, secondResult] = await Promise.all([
            first,
            second,
          ]);
          assert.equal(firstResult.outcome, "CONFIRMED");
          assert.deepEqual(secondResult, {
            outcome: "NOT_DISPATCHED",
            error: { reason: "BUSY" },
          });
          assert.equal(
            calls.filter(([, options]) => options.method === "POST").length,
            1,
          );
          assert.equal(
            calls.filter(([url]) => url === "/api/auth/csrf").length,
            1,
          );
        },
      );
    }
  }
}
for (const firstOutcome of ["confirmed", "rejected", "preflight-failed"]) {
  test(
    "client releases exclusion after " +
      firstOutcome +
      " without queuing a second operation",
    async () => {
      let calls = 0;
      const api = createScheduledWorkdayApi(async (url) => {
        calls++;
        if (firstOutcome === "preflight-failed" && calls === 1)
          return Response.json({}, { status: 401 });
        if (url.endsWith("/csrf")) return csrf();
        if (firstOutcome === "rejected" && calls === 2)
          return Response.json({}, { status: 409 });
        return response(body(), 201);
      }, true);
      const first = await api.start(1, 10, () => true);
      assert.equal(
        first.outcome,
        firstOutcome === "confirmed"
          ? "CONFIRMED"
          : firstOutcome === "rejected"
            ? "REJECTED"
            : "NOT_DISPATCHED",
      );
      const before = calls;
      await new Promise(setImmediate);
      assert.equal(calls, before);
      assert.equal((await api.start(1, 10, () => true)).outcome, "CONFIRMED");
      assert.equal(calls, before + 2);
    },
  );
}
