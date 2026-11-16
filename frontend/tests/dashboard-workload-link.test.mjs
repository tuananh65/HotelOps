import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const code = ts.transpileModule(readFileSync(new URL("../src/insights/dashboardWorkloadLink.ts", import.meta.url), "utf8"), {
  compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { dashboardWorkloadLink: link, dashboardWorkloadFilters: parse } = await import("data:text/javascript;base64," + Buffer.from(code).toString("base64"));
const filters = { branchIds: [7], departmentId: 8, from: "2026-11-15", to: "2026-11-16", calendar: "UTC" };
test("dashboard workload navigation retains exact scope and explicit UTC period", () => {
  for (const key of ["openTasks", "overdueTasks", "closedTasksInPeriod", "taskEffortHours", "taskProvisionalHours", "taskOpenIntervals"]) {
    const url = new URL(link(filters, key), "https://example.invalid");
    assert.equal(url.pathname, "/operational-tasks");
    assert.equal(url.searchParams.get("branchId"), "7");
    assert.deepEqual(parse(url.searchParams), { from: filters.from, to: filters.to, calendar: "UTC", departmentId: 8,
      ...(key === "overdueTasks" ? { overdueOnly: true } : {}), ...(key === "closedTasksInPeriod" ? { status: "CLOSED" } : {}) });
  }
});
test("navigation never chooses an arbitrary branch or silently truncates unsupported periods", () => {
  for (const invalid of [{ branchIds: [7, 8] }, { branchIds: [] }, { branchIds: [0] }, { calendar: "LOCAL" },
    { from: "2026-02-30" }, { from: "2027-01-01" }, { from: "1899-01-01" }, { from: "2020-01-01" }, { departmentId: -1 }])
    assert.equal(link({ ...filters, ...invalid }, "openTasks"), null);
  assert.equal(link(filters, "readyRooms"), null);
});
test("malformed deep links cannot auto-load different filters", () => {
  const valid = new URL(link(filters, "openTasks"), "https://example.invalid").searchParams;
  for (const [key, value] of [["calendar", "LOCAL"], ["departmentId", "-1"], ["status", "PAUSED"], ["from", "2026-02-30"], ["overdueOnly", "false"]]) {
    const params = new URLSearchParams(valid); params.set(key, value);
    assert.equal(parse(params), undefined);
  }
});
