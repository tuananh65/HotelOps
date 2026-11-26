import test from "node:test";
import assert from "node:assert/strict";
import { nextCommands, permission } from "../src/compact/workflow.ts";
test("cleaning requires acceptance before start and inspection after completion", () => {
  assert.ok(
    nextCommands("CLEANING", "ASSIGNED", true, false, false).includes("accept"),
  );
  assert.ok(
    !nextCommands("CLEANING", "ASSIGNED", true, false, false).includes("start"),
  );
  assert.ok(
    nextCommands("CLEANING", "ASSIGNED", true, true, false).includes("start"),
  );
  assert.ok(
    !nextCommands("CLEANING", "PENDING_INSPECTION", true, true, false).includes(
      "verify",
    ),
  );
});
test("unassigned staff cannot start, pause, complete or conclude inspection", () => {
  for (const state of [
    "ASSIGNED",
    "IN_PROGRESS",
    "INSPECTING",
    "PENDING_CONCLUSION",
  ])
    assert.ok(
      !nextCommands("INSPECTION", state, false, false, false).some((a) =>
        ["start", "pause", "complete", "pass", "fail"].includes(a),
      ),
    );
});
test("review permissions stay independent from execution", () => {
  assert.equal(permission("MAINTENANCE", "verify"), "MAINTENANCE_REVIEW");
  assert.equal(permission("MAINTENANCE", "start"), "MAINTENANCE_EXECUTE");
  assert.equal(permission("OPERATIONAL", "verify"), "TASK_MANAGE");
  assert.equal(permission("INTERNAL_REQUEST", "verify"), "IR_CREATE");
});
test("completed maintenance has no extra lead verification step", () => {
  const completed = nextCommands(
    "MAINTENANCE",
    "COMPLETED",
    false,
    false,
    false,
  );
  assert.ok(!completed.includes("verify"));
  assert.ok(!completed.includes("assign") && !completed.includes("start"));
  assert.ok(
    !nextCommands("MAINTENANCE", "IN_PROGRESS", false, false, false).includes(
      "verify",
    ),
  );
  assert.equal(permission("MAINTENANCE", "reopen"), "MAINTENANCE_REVIEW");
});
test("internal requests verify only for creator", () => {
  assert.ok(
    !nextCommands("INTERNAL_REQUEST", "COMPLETED", false, true, false).includes(
      "verify",
    ),
  );
  assert.ok(
    nextCommands("INTERNAL_REQUEST", "COMPLETED", false, true, true).includes(
      "verify",
    ),
  );
});
