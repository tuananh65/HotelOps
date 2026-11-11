import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const b = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "ts",
    contents: "export * from './src/roles/customRoleApi';",
  },
  bundle: true,
  write: false,
  format: "esm",
});
const { createCustomRoleApi, validCustomRole } = await import(
  "data:text/javascript;base64," +
    Buffer.from(b.outputFiles[0].text).toString("base64")
);
const input = {
  roleCode: "CUSTOM_X",
  roleName: "Custom name",
  description: null,
};
const dto = {
  roleId: 12,
  ...input,
  isSystemRole: false,
  isActive: true,
  assignedUserCount: 0,
  permissionGrantCount: 0,
};
const csrf = () =>
  Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" });
const created = (v) =>
  Response.json(v, { status: 201, headers: { Location: "/api/roles/12" } });
test("OFF prevents CSRF POST and PATCH", async () => {
  let n = 0;
  const api = createCustomRoleApi(async () => {
    n++;
    throw Error();
  });
  assert.equal((await api.create(input, () => true)).reason, "DISABLED");
  assert.equal((await api.update()).reason, "ROLE_VERSION_CONTRACT_REQUIRED");
  assert.equal(n, 0);
});
test("201 normalized custom role exact allowlisted payload", async () => {
  const calls = [];
  const api = createCustomRoleApi(async (u, o) => {
    calls.push([u, o]);
    return u.endsWith("/csrf") ? csrf() : created({ ...dto, secret: "SECRET" });
  }, true);
  const r = await api.create(
    { ...input, isSystemRole: true, actorUserId: 9, grants: ["ADMIN"] },
    () => true,
  );
  assert.equal(r.outcome, "CONFIRMED");
  assert.ok(!JSON.stringify(r).includes("SECRET"));
  assert.equal(calls.length, 2);
  assert.equal(calls[1][0], "/api/roles");
  assert.equal(calls[1][1].method, "POST");
  assert.equal(calls[1][1].credentials, "include");
  assert.equal(
    new Headers(calls[1][1].headers).get("X-CSRF-TOKEN"),
    "synthetic",
  );
  assert.deepEqual(JSON.parse(calls[1][1].body), input);
});
for (const status of [400, 401, 403, 409, 412, 503, 500])
  test("error " + status, async () => {
    let n = 0;
    const api = createCustomRoleApi(async (u) => {
      if (u.endsWith("/csrf")) return csrf();
      n++;
      return Response.json({ detail: "SECRET", traceId: "SECRET" }, { status });
    }, true);
    const r = await api.create(input, () => true);
    assert.equal(r.outcome, status >= 500 ? "OUTCOME_UNKNOWN" : "REJECTED");
    assert.equal(r.status, status);
    assert.ok(!JSON.stringify(r).includes("SECRET"));
    assert.equal(n, 1);
  });
for (const [name, change] of [
  ["system role", (v) => (v.isSystemRole = true)],
  ["granted authority", (v) => (v.permissionGrantCount = 1)],
  ["assigned user", (v) => (v.assignedUserCount = 1)],
  ["wrong code", (v) => (v.roleCode = "OTHER")],
  ["missing ID", (v) => delete v.roleId],
])
  test("malformed 201 " + name, async () => {
    const v = { ...dto };
    change(v);
    const r = await createCustomRoleApi(
      async (u) => (u.endsWith("/csrf") ? csrf() : created(v)),
      true,
    ).create(input, () => true);
    assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  });
test("network unknown no retry", async () => {
  let n = 0;
  const r = await createCustomRoleApi(async (u) => {
    if (u.endsWith("/csrf")) return csrf();
    n++;
    throw Error("SECRET");
  }, true).create(input, () => true);
  assert.equal(r.outcome, "OUTCOME_UNKNOWN");
  assert.equal(n, 1);
});
test("identity replacement during CSRF prevents POST", async () => {
  let current = true,
    n = 0;
  const r = await createCustomRoleApi(async () => {
    n++;
    current = false;
    return csrf();
  }, true).create(input, () => current);
  assert.equal(r.outcome, "NOT_DISPATCHED");
  assert.equal(n, 1);
});
test("invalid metadata sends nothing", async () => {
  assert.equal(validCustomRole({ ...input, roleName: " " }), false);
  let n = 0;
  await createCustomRoleApi(async () => {
    n++;
    throw Error();
  }, true).create({ ...input, roleCode: "" }, () => true);
  assert.equal(n, 0);
});

for (const [submitted, normalized] of [
  [" straße ", "STRAßE"],
  ["ß", "ß"],
  ["ﬀ", "ﬀ"],
]) {
  test(
    "201 invariant normalization without full case expansion: " + submitted,
    async () => {
      const calls = [];
      const result = await createCustomRoleApi(async (url, options) => {
        calls.push([url, options]);
        return url.endsWith("/csrf")
          ? csrf()
          : created({ ...dto, roleCode: normalized });
      }, true).create({ ...input, roleCode: submitted }, () => true);
      assert.equal(result.outcome, "CONFIRMED");
      assert.equal(result.data.roleCode, normalized);
      assert.equal(calls.length, 2);
      assert.equal(calls[1][1].method, "POST");
      assert.equal(JSON.parse(calls[1][1].body).roleCode, submitted);
    },
  );
}

for (const wrongCode of ["STRASSE", "OTHER", "straße"]) {
  test(
    "unrelated or noncanonical Unicode success remains unknown: " + wrongCode,
    async () => {
      let posts = 0;
      const result = await createCustomRoleApi(async (url) => {
        if (url.endsWith("/csrf")) return csrf();
        posts++;
        return created({ ...dto, roleCode: wrongCode, traceId: "SECRET" });
      }, true).create({ ...input, roleCode: "straße" }, () => true);
      assert.deepEqual(result, {
        outcome: "OUTCOME_UNKNOWN",
        reason: "INVALID_RESPONSE",
      });
      assert.equal(posts, 1);
      assert.ok(!JSON.stringify(result).includes("SECRET"));
    },
  );
}
