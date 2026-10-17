import { test } from "node:test";
import assert from "node:assert/strict";
import { bundle, entity } from "./api-test-helpers.mjs";
const a = await bundle("src/api/adapters.ts", "adapters");
test("maps status codes without equating unsupported SQL room states with Ready", () => {
  assert.equal(a.toLabel("PENDING_INSPECTION"), "Pending Inspection");
  assert.equal(a.toLabel("AVAILABLE"), "AVAILABLE");
  assert.equal(a.toLabel("OUT_OF_SERVICE"), "OUT_OF_SERVICE");
  assert.equal(a.toCode("Rework Required"), "REWORK_REQUIRED");
});
test("SQL BIGINT string identities retain precision; unsafe JSON numbers fail visibly", () => {
  assert.equal(a.asId("9223372036854775807"), "9223372036854775807");
  assert.throws(() => a.asId(9223372036854775807), /unsafe identifier/);
});
test("write adapter whitelists form fields and excludes server-owned status and stock balances", () => {
  const body = a.entityToDto(
    {
      name: "101",
      branch: "Bui Vien",
      floor: "f1",
      status: "Ready",
      balance: 999,
      version: 3,
      rowVersion: "v1",
    },
    ["name", "branch", "floor", "status"],
    () => "1",
  );
  assert.deepEqual(body, {
    name: "101",
    code: undefined,
    branchId: "1",
    departmentId: undefined,
    rowVersion: "v1",
    attributes: { floorId: "f1" },
  });
});
test("room adapter renders server-confirmed readiness only", () => {
  const value = a.entityFromDto(
    entity("room-1", {
      statusCode: "BLOCKED",
      attributes: { roomTypeId: "rt1", floorId: "f1" },
    }),
  );
  assert.equal(value.status, "Blocked");
  assert.equal(value.floor, "f1");
  assert.equal(value.roomType, "rt1");
});
test("uses UTC+7 for date-time input independently of browser timezone", () => {
  assert.equal(
    a.localDateTimeToUtc("2026-10-16T15:30"),
    "2026-10-16T08:30:00.000Z",
  );
  assert.equal(
    a.localDateTimeToUtc("2026-10-16T15:30:15"),
    "2026-10-16T08:30:15.000Z",
  );
  assert.equal(
    a.localDateTimeToUtc("2026-10-16T08:30:00Z"),
    "2026-10-16T08:30:00.000Z",
  );
});
test("actual incident DTO is adapted independently of SQL and proposed EntityDto", () => {
  const value = a.incidentFromDto({
    incidentId: 42,
    incidentNo: "INC-42",
    incidentTitle: "Leaking tap",
    branchId: 1,
    branchName: "Branch",
    roomId: 2,
    roomNumber: "101",
    areaId: null,
    assetId: null,
    status: "PENDING_TRIAGE",
    reportedBy: 5,
    reportedAt: "2026-01-01T00:00:00Z",
    discoveredAt: "2026-01-01T00:00:00Z",
    resolvedAt: null,
    closedAt: null,
    severityName: null,
    priorityName: null,
  });
  assert.equal(value.id, "42");
  assert.equal(value.room, "2");
  assert.equal(value.name, "Leaking tap");
  assert.deepEqual(value.allowedActions, []);
  assert.equal(value.canEdit, false);
});
test("ASP.NET field paths map to existing form controls including line errors", () => {
  assert.equal(a.formFieldForApiPath("Attributes.RoomId"), "room");
  assert.equal(a.formFieldForApiPath("NewPassword"), "next");
  assert.equal(a.formFieldForApiPath("attributes.Lines[0].Quantity"), "lines");
});
test("uploaded attachments send server identifiers, never embedded data URLs", () => {
  const dto = a.entityToDto(
    { evidence: [{ id: "att1", data: "https://api.test/att1" }] },
    ["evidence"],
    () => "",
  );
  assert.deepEqual(dto.attributes.attachmentIds, ["att1"]);
});
test("quantity commands send only the action input and retain separate server balances", () => {
  const line = {
    id: "line1",
    item: "i1",
    unit: "7",
    quantity: 3,
    approved: 8,
    issued: 5,
    factor: 99,
  };
  assert.deepEqual(a.commandLines("materials.approve", [line]), [
    { lineId: "line1", approvedQuantity: 3 },
  ]);
  assert.deepEqual(a.commandLines("materials.partial-issue", [line]), [
    { lineId: "line1", issuedQuantity: 3 },
  ]);
  assert.deepEqual(
    a.commandLines("transactions.confirm-receipt", [
      { ...line, damaged: 1, missing: 2 },
    ]),
    [
      {
        lineId: "line1",
        receivedQuantity: 3,
        damagedQuantity: 1,
        missingQuantity: 2,
      },
    ],
  );
  assert.equal("issuedQuantity" in a.lineToDto(line), false);
  assert.equal("conversionFactor" in a.lineToDto(line), false);
});
