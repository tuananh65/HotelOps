import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/insights/reportApi.ts", "src/shared-host/returnUrl.ts"],
  outdir: ".test-output/reports",
  bundle: true,
  platform: "node",
  format: "esm",
  outExtension: { ".js": ".mjs" },
});
const { createReportApi, decodeSnapshot, decodeOptions } =
  await import("../.test-output/reports/insights/reportApi.mjs");
const { approvedReturnDestination } =
  await import("../.test-output/reports/shared-host/returnUrl.mjs");
const query = { branchId: 1, from: "2026-11-01", to: "2026-11-15" };
const snapshot = () => ({
  snapshotId: "a".repeat(32),
  key: "tasks",
  snapshot: "2026-11-15T00:00:00Z",
  filters: {
    branchIds: [1],
    departmentId: null,
    from: query.from,
    to: query.to,
    calendar: "UTC",
  },
  sections: [
    {
      key: "tasks",
      columns: ["id", "title"],
      rows: [{ id: 1, title: "=private formula" }],
    },
  ],
  totalCount: 1,
  missingSources: ["Partial report"],
  exportFormats: ["csv"],
});

test("inspection snapshots must confirm each requested actor filter", () => {
  const data = { ...snapshot(), key: "inspections", appliedFilters: { housekeeperId: "42", inspectorId: "43" } };
  const filters = { ...query, housekeeperId: 42, inspectorId: 43 };
  assert.equal(decodeSnapshot(data, "inspections", filters).key, "inspections");
  assert.throws(() => decodeSnapshot({ ...data, appliedFilters: null }, "inspections", filters), /Unconfirmed report filter/);
  assert.throws(() => decodeSnapshot({ ...data, appliedFilters: { housekeeperId: "43", inspectorId: "43" } }, "inspections", filters), /Unconfirmed report filter/);
});
test("XLSX requires advertised capability and preserves format and MIME", async () => {
  const report = snapshot();
  report.exportFormats = ["csv", "xlsx"];
  assert.deepEqual(decodeSnapshot(report, "tasks", query).exportFormats, ["csv", "xlsx"]);
  const calls = [];
  const api = createReportApi(new AbortController().signal, async (path, init) => {
    calls.push({ path, init });
    if (path === "/api/auth/csrf") return Response.json({ csrfToken: "token" });
    return new Response(new Uint8Array([80, 75, 3, 4]), { headers: {
      "Content-Type": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    } });
  });
  const file = await api.export(report, "xlsx");
  assert.equal(file.name, `tasks-${report.snapshotId}.xlsx`);
  assert.deepEqual(JSON.parse(calls[1].init.body), { format: "xlsx" });
  assert.equal(calls[1].init.headers["X-CSRF-TOKEN"], "token");
  await assert.rejects(api.export(snapshot(), "xlsx"), /Invalid export identity/);
});
test("report return URL requires revalidation and rejects query injection", () => {
  assert.equal(approvedReturnDestination("/reports", true), "/reports");
  assert.equal(approvedReturnDestination("/reports", false), null);
  assert.equal(approvedReturnDestination("/reports?branchId=1", true), null);
});
test("snapshot preserves scalar strings without interpreting formulas", () =>
  assert.equal(
    decodeSnapshot(snapshot(), "tasks", query).sections[0].rows[0].title,
    "=private formula",
  ));
for (const [name, change] of [
  ["foreign branch", (s) => (s.filters.branchIds = [2])],
  ["foreign department", (s) => (s.filters.departmentId = 3)],
  ["changed period", (s) => (s.filters.from = "2026-10-01")],
  ["unsafe identity", (s) => (s.snapshotId = "../export")],
  ["row count mismatch", (s) => (s.totalCount = 2)],
  ["missing cell", (s) => delete s.sections[0].rows[0].title],
  ["nested cell", (s) => (s.sections[0].rows[0].title = {})],
  ["duplicate columns", (s) => s.sections[0].columns.push("id")],
  ["unsupported format", (s) => (s.exportFormats = ["html"])],
  ["nested filter", (s) => (s.appliedFilters = { branch: {} })],
])
  test(`reject ${name}`, () => {
    const s = snapshot();
    change(s);
    assert.throws(() => decodeSnapshot(s, "tasks", query));
  });
test("options reject departments outside admitted branches", () =>
  assert.throws(() =>
    decodeOptions({
      branches: [{ id: 1, name: "B", code: "B" }],
      departments: [{ id: 2, branchId: 3, name: "D" }],
      calendar: "UTC",
      defaultPeriodDays: 30,
      exportRowLimit: 50000,
    }),
  ));
test("preview and CSV use fresh CSRF and exact server snapshot", async () => {
  const calls = [];
  let token = 0;
  const api = createReportApi(
    new AbortController().signal,
    async (path, init) => {
      calls.push({ path, init });
      if (path === "/api/auth/csrf")
        return Response.json({ csrfToken: `token${++token}` });
      if (path.endsWith("/preview")) return Response.json(snapshot());
      return new Response("frozen,csv", {
        headers: { "Content-Type": "text/csv; charset=utf-8" },
      });
    },
  );
  const result = await api.preview("tasks", query);
  const file = await api.export(result);
  assert.equal(await file.blob.text(), "frozen,csv");
  assert.equal(calls[1].init.headers["X-CSRF-TOKEN"], "token1");
  assert.equal(calls[3].init.headers["X-CSRF-TOKEN"], "token2");
  assert.equal(
    calls[3].path,
    `/api/reports/snapshots/${result.snapshotId}/export`,
  );
  assert.deepEqual(JSON.parse(calls[3].init.body), { format: "csv" });
  assert.ok(
    calls.every(
      (c) =>
        c.init.credentials === "same-origin" &&
        c.init.cache === "no-store" &&
        c.init.redirect === "error",
    ),
  );
});
test("invalid date rejected before any request", async () => {
  const api = createReportApi(new AbortController().signal, () => {
    throw new Error("network must not run");
  });
  await assert.rejects(
    api.preview("tasks", { ...query, from: "2026-02-30" }),
    /Invalid report filters/,
  );
});
test("aborted export cannot return bytes even when transport ignores signal", async () => {
  const controller = new AbortController();
  const api = createReportApi(controller.signal, async (path) => {
    if (path === "/api/auth/csrf") return Response.json({ csrfToken: "token" });
    controller.abort();
    return new Response("csv", { headers: { "Content-Type": "text/csv" } });
  });
  await assert.rejects(api.export(snapshot()), { name: "AbortError" });
});
test("HTML export body is never offered as CSV", async () => {
  const api = createReportApi(new AbortController().signal, async (path) =>
    path === "/api/auth/csrf"
      ? Response.json({ csrfToken: "token" })
      : new Response("<html>login</html>", {
          headers: { "Content-Type": "text/html" },
        }),
  );
  await assert.rejects(api.export(snapshot()), /Invalid CSV response/);
});
test("permission denial is preserved without preview retry", async () => {
  let calls = 0;
  const api = createReportApi(new AbortController().signal, async () => {
    calls++;
    return new Response(null, { status: 403 });
  });
  await assert.rejects(api.options(), (e) => e.status === 403);
  assert.equal(calls, 1);
});
