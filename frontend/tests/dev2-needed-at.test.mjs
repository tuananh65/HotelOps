import { test } from "node:test";
import assert from "node:assert/strict";
import { register } from "node:module";
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import {
  materialRequestDemoInput,
  submitReadyRequest,
  sanitizedHttpFailure,
} from "./dev2-demo-inputs.mjs";
register(new URL("./dev2-ts-loader.mjs", import.meta.url));
const { createDev2Api } = await import("../src/dev2/api.ts");
const { operations } = await import("../src/dev2/catalog.generated.ts");
const input = {
  branchId: 3,
  departmentId: 4,
  warehouseId: 5,
  itemId: 6,
  uomId: 7,
  marker: "TEST",
};
const action = operations.find((x) => x.id === "A004A-043");
const response = (body, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });

test("browser demo input passes existing A043 transport with warehouse and future UTC NeededAt before A045", async () => {
  const now = new Date("2026-11-13T23:59:32Z"),
    values = materialRequestDemoInput(input, 8, now);
  assert.match(values.neededAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/);
  let posted;
  const api = createDev2Api(async (url, init) => {
    if (url === "/api/auth/session")
      return response({
        user: { id: "42", username: "staff", name: "Staff" },
        expiresAt: "2099-11-13T20:00:00Z",
        absoluteExpiresAt: "2099-11-14T20:00:00Z",
        mustChangePassword: false,
        csrfToken: "PRIVATE-TOKEN",
      });
    posted = JSON.parse(init.body);
    return response(
      { ...posted, requestId: 9, rowVersion: "AAAAAAAAAAE=" },
      201,
    );
  });
  const result = await api(action, {}, values, "42");
  assert.equal(result.ok, true);
  assert.equal(posted.sourceWarehouseId, input.warehouseId);
  assert.match(posted.neededAt, /Z$/);
  assert.ok(
    Math.abs(Date.parse(posted.neededAt) - now.getTime() - 86400000) < 60000,
  );
  assert.ok(submitReadyRequest(result.data, input.warehouseId, now));
  for (const invalid of [
    { ...result.data, neededAt: null },
    { ...result.data, sourceWarehouseId: null },
    { ...result.data, neededAt: "invalid" },
    { ...result.data, rowVersion: "" },
  ])
    assert.equal(submitReadyRequest(invalid, input.warehouseId, now), false);
  const script = readFileSync(
    new URL("./dev2-live-browser.mjs", import.meta.url),
    "utf8",
  );
  assert.ok(
    script.includes("materialRequestDemoInput(input, incident.incidentId)"),
  );
  assert.ok(
    script.indexOf('"A004A-043 persisted submit prerequisites') > 0 &&
    script.indexOf('"A004A-043 persisted submit prerequisites') <
      script.indexOf('"A004A-045"'),
  );
});
test("datetime-local future scheduling roundtrips across timezone and DST boundaries", () => {
  const helper = new URL("./dev2-demo-inputs.mjs", import.meta.url).href;
  for (const TZ of ["UTC", "Asia/Ho_Chi_Minh", "America/Los_Angeles"]) {
    const script = `import {materialRequestDemoInput} from ${JSON.stringify(helper)};const now=new Date('2026-03-07T20:25:32Z');const value=materialRequestDemoInput({},1,now).neededAt;if(Math.abs(Date.parse(value)-now.getTime()-86400000)>=60000)process.exit(1);`;
    const child = spawnSync(
      process.execPath,
      ["--input-type=module", "--eval", script],
      { env: { ...process.env, TZ }, windowsHide: true, encoding: "utf8" },
    );
    assert.equal(child.status, 0, TZ);
  }
});
test("product DRAFT contract still permits absent NeededAt", async () => {
  assert.equal(action.fields.find((x) => x.name === "neededAt").optional, true);
  let posted;
  const api = createDev2Api(async (url, init) =>
    url === "/api/auth/session"
      ? response({
          user: { id: "42", username: "staff", name: "Staff" },
          expiresAt: "2099-11-13T20:00:00Z",
          absoluteExpiresAt: "2099-11-14T20:00:00Z",
          mustChangePassword: false,
          csrfToken: "PRIVATE-TOKEN",
        })
      : ((posted = JSON.parse(init.body)), response({ requestId: 9 }, 201)),
  );
  const values = materialRequestDemoInput(input, 8);
  delete values.neededAt;
  assert.equal((await api(action, {}, values, "42")).ok, true);
  assert.equal(Object.hasOwn(posted, "neededAt"), false);
});
test("HTTP diagnostics retain only allowlisted static validation messages", () => {
  const known = sanitizedHttpFailure(400, {
    detail: "Submit requires SourceWarehouseId and NeededAt.",
    cookie: "SECRET",
    csrfToken: "SECRET",
  });
  assert.equal(known.category, "SUBMIT_CONTENT_INCOMPLETE");
  assert.equal(
    known.message,
    "Submit requires SourceWarehouseId and NeededAt.",
  );
  for (const problem of [
    {
      detail: "password=SECRET",
      title: "SECRET",
      errors: { neededAt: ["SECRET"] },
    },
    { detail: "Submit requires SourceWarehouseId and NeededAt. SECRET" },
    null,
  ]) {
    const value = sanitizedHttpFailure(400, problem);
    assert.equal(value.category, "VALIDATION_UNCLASSIFIED");
    assert.ok(!JSON.stringify(value).includes("SECRET"));
  }
});
