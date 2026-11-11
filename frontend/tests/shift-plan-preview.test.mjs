import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const bundle = await build({
  entryPoints: ["src/workday/shiftPlanPreview.ts"],
  bundle: true,
  write: false,
  format: "esm",
});
const { calendarDate, draftSnapshot, decodePreview } = await import(
  "data:text/javascript;base64," +
    Buffer.from(bundle.outputFiles[0].text).toString("base64")
);
const draft = {
  employeeId: 7,
  branchId: 1,
  departmentId: 2,
  templateId: 3,
  shiftDate: "2028-02-29",
};
const row = {
  index: 0,
  startUtc: "2028-02-29T16:00:00+00:00",
  endUtc: "2028-03-01T00:00:00+00:00",
  issues: [],
};
const preview = (rows) => ({ rows, requiresServerRevalidation: true });
test("exact immutable draft allowlist detached from caller", () => {
  const input = { ...draft, role: "ADMIN" };
  const s = draftSnapshot([input]);
  input.employeeId = 9;
  assert.deepEqual(s, [draft]);
  assert.ok(Object.isFrozen(s) && Object.isFrozen(s[0]));
});
for (const field of ["employeeId", "branchId", "departmentId", "templateId"])
  for (const bad of [0, -1, 1.5, Number.MAX_SAFE_INTEGER + 1])
    test("reject invalid " + field + bad, () =>
      assert.equal(draftSnapshot([{ ...draft, [field]: bad }]), undefined),
    );
for (const date of [
  "0000-01-01",
  "2027-02-29",
  "2028-02-30",
  "2026-13-01",
  "2026-11-31",
  "2026-1-01",
])
  test("reject impossible date " + date, () =>
    assert.equal(calendarDate(date), false),
  );
test("batch bound and leap year", () => {
  assert.equal(draftSnapshot([]), undefined);
  assert.equal(draftSnapshot(Array(501).fill(draft)), undefined);
  assert.equal(draftSnapshot(Array(500).fill(draft)).length, 500);
  assert.equal(calendarDate("2028-02-29"), true);
});
test("clear overnight server interval still requires revalidation and strips raw fields", () => {
  const r = decodePreview(
    { ...preview([{ ...row, token: "SECRET" }]), canSave: true },
    1,
  );
  assert.deepEqual(r, preview([row]));
  assert.ok(Object.isFrozen(r.rows[0].issues));
});
for (let issue = 0; issue < 10; issue++)
  test("existing internal numeric issue " + issue, () =>
    assert.ok(
      decodePreview(
        preview([{ ...row, startUtc: null, endUtc: null, issues: [issue] }]),
        1,
      ),
    ),
  );
for (const bad of [
  null,
  {},
  preview([]),
  { rows: [row], requiresServerRevalidation: false },
  preview([{ ...row, index: 1 }]),
  preview([{ ...row, issues: [10] }]),
  preview([{ ...row, issues: ["ExistingOverlap"] }]),
  preview([{ ...row, issues: [7, 7] }]),
  preview([{ ...row, startUtc: null }]),
  preview([{ ...row, startUtc: null, endUtc: null }]),
  preview([{ ...row, endUtc: row.startUtc }]),
  preview([{ ...row, startUtc: "2028-02-30T00:00:00Z" }]),
  preview([{ ...row, startUtc: "2028-02-29T24:00:00Z" }]),
])
  test("reject malformed preview " + JSON.stringify(bad), () =>
    assert.equal(decodePreview(bad, 1), undefined),
  );
test("production entry does not import preview or prototype ShiftManagement", async () => {
  const b = await build({
    entryPoints: ["src/hotel/main.tsx"],
    bundle: true,
    write: false,
    metafile: true,
    loader: { ".css": "empty" },
    define: { "import.meta.env": "{}" },
  });
  assert.ok(
    !Object.keys(b.metafile.inputs).some((k) =>
      /ShiftPlanPreviewPanel|shiftPlanPreview.ts|ShiftManagementScreen/.test(k),
    ),
  );
});
