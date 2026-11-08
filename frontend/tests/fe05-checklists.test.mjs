import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: [
    "src/features/inspectionChecklists/api.ts",
    "src/features/inspectionChecklists/model.ts",
  ],
  outdir: ".test-output/fe05",
  bundle: true,
  platform: "node",
  format: "esm",
  outExtension: { ".js": ".mjs" },
});
const { createChecklistApi, ChecklistApiError } =
  await import("../.test-output/fe05/api.mjs");
const model = await import("../.test-output/fe05/model.mjs");
const tag = '"' + "A".repeat(64) + '"';
const criterion = () => ({
  text: "Sạch",
  group: "Phòng",
  mandatory: true,
  passCondition: "Không bụi",
  description: null,
});
const data = () => ({
  id: 42,
  branchId: 7,
  code: "CL1",
  name: "Checklist",
  description: null,
  active: true,
  roomTypeIds: [11],
  versions: [
    {
      id: 1,
      number: 1,
      status: "DRAFT",
      usedByInspection: false,
      criteria: [{ ...criterion(), id: 10, order: 1 }],
    },
  ],
});
const ok = (value) =>
  new Response(JSON.stringify(value), {
    status: 200,
    headers: { "Content-Type": "application/json", ETag: tag },
  });
const csrf = () =>
  ok({ csrfToken: "test-only-csrf", headerName: "X-CSRF-TOKEN" });
test("create maps exact DTO and sends cookie/CSRF transport", async () => {
  const calls = [];
  const api = createChecklistApi(async (path, options) => {
    calls.push({ path, options });
    return path === "/api/auth/csrf" ? csrf() : ok(data());
  });
  const input = model.createInput(" cl1 ", " Checklist ", "", "11", [
    criterion(),
  ]);
  const result = await api.create(7, input);
  assert.equal(result.value.id, 42);
  assert.equal(calls.length, 2);
  assert.equal(calls[1].path, "/api/branches/7/inspection-checklists");
  assert.equal(calls[1].options.credentials, "include");
  assert.equal(calls[1].options.headers["X-CSRF-TOKEN"], "test-only-csrf");
  assert.deepEqual(JSON.parse(calls[1].options.body), input);
});
test("read uses verified detail route and preserves ETag", async () => {
  const api = createChecklistApi(async (path, options) => {
    assert.equal(path, "/api/branches/7/inspection-checklists/42");
    assert.equal(options.method, "GET");
    return ok(data());
  });
  assert.equal((await api.read(7, 42)).etag, tag);
});
test("update sends criteria/reason only with server If-Match", async () => {
  const api = createChecklistApi(async (path, options) => {
    if (path === "/api/auth/csrf") return csrf();
    assert.equal(options.method, "PUT");
    assert.equal(options.headers["If-Match"], tag);
    assert.deepEqual(Object.keys(JSON.parse(options.body)), [
      "criteria",
      "reason",
    ]);
    return ok(data());
  });
  await api.update(7, 42, tag, [criterion()], "Cập nhật");
});
for (const action of ["publish", "retire"])
  test(action + " sends confirmed transition and ETag", async () => {
    const api = createChecklistApi(async (path, options) => {
      if (path === "/api/auth/csrf") return csrf();
      assert.equal(path, `/api/branches/7/inspection-checklists/42/${action}`);
      assert.equal(options.headers["If-Match"], tag);
      assert.deepEqual(JSON.parse(options.body), {
        reason: "Review",
        confirmation: true,
      });
      return ok(data());
    });
    await api.transition(7, 42, tag, action, "Review", true);
  });
test("transition without confirmation sends no request", async () => {
  let calls = 0;
  const api = createChecklistApi(async () => {
    calls++;
    return csrf();
  });
  await assert.rejects(api.transition(7, 42, tag, "retire", "Review", false));
  assert.equal(calls, 0);
});
test("published used version is copied to new input without historical identity or mutation", () => {
  const checklist = data();
  checklist.versions[0].status = "PUBLISHED";
  checklist.versions[0].usedByInspection = true;
  assert.equal(model.lifecycle(checklist).canEdit, true);
  assert.equal(model.lifecycle(checklist).canPublish, false);
  const copy = model.editableCriteria(checklist);
  copy[0].text = "New";
  assert.equal(checklist.versions[0].criteria[0].text, "Sạch");
  assert.equal("id" in copy[0], false);
});
test("used Draft and inactive aggregate cannot edit/publish", () => {
  const checklist = data();
  checklist.versions[0].usedByInspection = true;
  assert.equal(model.lifecycle(checklist).canEdit, false);
  assert.equal(model.lifecycle(checklist).canPublish, false);
  checklist.active = false;
  assert.equal(model.lifecycle(checklist).canRetire, false);
});
test("RoomType selection filters exact branch and active status", async () => {
  const api = createChecklistApi(async (path) => {
    assert.match(path, /branchId=7/);
    return ok({
      items: [
        { details: { id: 11, branchId: 7, isActive: true } },
        { details: { id: 12, branchId: null, isActive: true } },
        { details: { id: 13, branchId: 8, isActive: true } },
      ],
      total: 3,
    });
  });
  assert.deepEqual(
    (await api.roomTypes(7)).map((t) => t.id),
    [11],
  );
});
test("validation rejects invalid code, duplicate IDs and incomplete criteria", () => {
  assert.throws(() =>
    model.createInput("bad code", "Name", "", "11", [criterion()]),
  );
  assert.throws(() => model.roomTypeIds("11,11"));
  assert.throws(() => model.criteriaInput([]));
  assert.throws(() => model.criteriaInput([{ ...criterion(), text: " " }]));
  assert.throws(() =>
    model.criteriaInput([{ ...criterion(), mandatory: undefined }]),
  );
  assert.throws(() => model.reasonInput(" ".repeat(3)));
  assert.throws(() =>
    model.criteriaInput([{ ...criterion(), group: "x".repeat(101) }]),
  );
  assert.throws(() => model.reasonInput("x".repeat(501)));
});
test("criteria and reason maximum boundaries are accepted", () => {
  assert.equal(
    model.criteriaInput([
      { ...criterion(), text: "x".repeat(250), group: "g".repeat(100) },
    ]).length,
    1,
  );
  assert.equal(model.reasonInput("x".repeat(500)).length, 500);
});
for (const status of [400, 401, 403, 404, 409, 412, 428, 503])
  test(`HTTP ${status} is explicit and never retried`, async () => {
    let calls = 0;
    const api = createChecklistApi(async (path) => {
      calls++;
      return path === "/api/auth/csrf"
        ? csrf()
        : new Response("{}", { status });
    });
    await assert.rejects(
      api.update(7, 42, tag, [criterion()], "Review"),
      (error) => error instanceof ChecklistApiError && error.status === status,
    );
    assert.equal(calls, 2);
  });
test("uncertain mutation is not replayed automatically", async () => {
  let calls = 0;
  const api = createChecklistApi(async (path) => {
    calls++;
    if (path === "/api/auth/csrf") return csrf();
    throw new Error("Network lost");
  });
  await assert.rejects(
    api.create(7, model.createInput("CL1", "Name", "", "11", [criterion()])),
    (error) => error.uncertain === true,
  );
  assert.equal(calls, 2);
});
test("invalid ETag blocks writes before CSRF or mutation", async () => {
  let calls = 0;
  const api = createChecklistApi(async () => {
    calls++;
    return csrf();
  });
  await assert.rejects(
    api.update(7, 42, "*", [criterion()], "Review"),
    (error) => error.status === 428,
  );
  assert.equal(calls, 0);
});
test("missing historical-used flag fails closed", async () => {
  const body = data();
  delete body.versions[0].usedByInspection;
  await assert.rejects(
    createChecklistApi(async () => ok(body)).read(7, 42),
    (error) => error.status === 503,
  );
});
test("cross-branch response is rejected", async () => {
  const body = data();
  body.branchId = 8;
  await assert.rejects(createChecklistApi(async () => ok(body)).read(7, 42));
});
test("SPA HTML response cannot become checklist data", async () => {
  await assert.rejects(
    createChecklistApi(async () => new Response("<html></html>")).read(7, 42),
    (error) => error.status === 503,
  );
});
