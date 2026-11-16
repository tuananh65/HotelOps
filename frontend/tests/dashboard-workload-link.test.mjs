import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const code = ts.transpileModule(readFileSync(new URL("../src/insights/dashboardWorkloadLink.ts", import.meta.url), "utf8"), {
  compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { dashboardWorkloadLink: link, dashboardWorkloadFilters: parse, dashboardWorkloadBranches: branches } = await import("data:text/javascript;base64," + Buffer.from(code).toString("base64"));
const filters = { branchIds: [7], departmentId: 8, from: "2026-11-15", to: "2026-11-16", calendar: "UTC" };
test("dashboard workload navigation retains exact scope and explicit UTC period", () => {
  for (const key of ["openTasks", "overdueTasks", "closedTasksInPeriod", "taskEffortHours", "taskProvisionalHours", "taskOpenIntervals"]) {
    const url = new URL(link(filters, key), "https://example.invalid");
    assert.equal(url.pathname, "/operational-tasks");
    assert.equal(url.searchParams.get("branchId"), "7");
    assert.deepEqual(parse(url.searchParams), { from: filters.from, to: filters.to, calendar: "UTC", departmentId: 8,
      ...({ openTasks: "OPEN", overdueTasks: "OVERDUE", closedTasksInPeriod: "CLOSED_IN_PERIOD" }[key] ? { taskSet: { openTasks: "OPEN", overdueTasks: "OVERDUE", closedTasksInPeriod: "CLOSED_IN_PERIOD" }[key] } : {}),
      ...(key === "overdueTasks" ? { overdueOnly: true } : {}), ...(key === "closedTasksInPeriod" ? { status: "CLOSED" } : {}) });
  }
});
test("navigation never chooses an arbitrary branch or accepts invalid periods", () => {
  assert.equal(link(filters, "averageTaskCompletionHours"), link(filters, "closedTasksInPeriod"));
  for (const invalid of [{ branchIds: [7, 7] }, { branchIds: [] }, { branchIds: [0] }, { calendar: "LOCAL" },
    { from: "2026-02-30" }, { from: "2027-01-01" }, { from: "0000-01-01" }, { to: "9999-12-31" }, { departmentId: -1 }])
    assert.equal(link({ ...filters, ...invalid }, "openTasks"), null);
  assert.equal(link(filters, "readyRooms"), null);
});

test("multi-branch navigation preserves every branch and the exact long UTC period", () => {
  const url = new URL(link({ ...filters, branchIds: [7, 8], from: "2020-01-01" }, "closedTasksInPeriod"), "https://example.invalid");
  assert.equal(url.searchParams.has("branchId"), false);
  assert.deepEqual(branches(url.searchParams), [7, 8]);
  assert.equal(parse(url.searchParams).from, "2020-01-01");
  for (const value of ["7,7", "7,0", "7,8x", "7", "", "7,9007199254740992"]) {
    const invalid = new URLSearchParams(url.searchParams); invalid.set("workloadBranches", value);
    assert.equal(branches(invalid), undefined);
  }
  url.searchParams.set("branchId", "7");
  assert.equal(branches(url.searchParams), undefined);
});
test("malformed deep links cannot auto-load different filters", () => {
  const valid = new URL(link(filters, "openTasks"), "https://example.invalid").searchParams;
  for (const [key, value] of [["calendar", "LOCAL"], ["departmentId", "-1"], ["status", "PAUSED"], ["from", "2026-02-30"], ["overdueOnly", "false"]]) {
    const params = new URLSearchParams(valid); params.set(key, value);
    assert.equal(parse(params), undefined);
  }
});
