// Only the executable demo requires a submit-ready draft; product DRAFT creation stays optional.
export function materialRequestDemoInput(input, incidentId, now = new Date()) {
  const future = new Date(now.getTime() + 24 * 60 * 60 * 1000);
  const pad = (value) => String(value).padStart(2, "0");
  const neededAt = `${future.getFullYear()}-${pad(future.getMonth() + 1)}-${pad(future.getDate())}T${pad(future.getHours())}:${pad(future.getMinutes())}`;
  return {
    branchId: input.branchId,
    departmentId: input.departmentId,
    sourceWarehouseId: input.warehouseId,
    neededAt,
    incidentId,
    purpose: input.marker + " Browser request",
    lines: [{ lineNo: 1, itemId: input.itemId, uomId: input.uomId, requestedQty: 10 }],
  };
}

export function submitReadyRequest(request, warehouseId, now = new Date()) {
  return (
    Number.isSafeInteger(warehouseId) &&
    warehouseId > 0 &&
    request?.sourceWarehouseId === warehouseId &&
    typeof request.neededAt === "string" &&
    Number.isFinite(Date.parse(request.neededAt)) &&
    Date.parse(request.neededAt) > now.getTime() &&
    typeof request.rowVersion === "string" &&
    request.rowVersion.length > 0
  );
}

// Exact static backend messages only. Never retain arbitrary ProblemDetails, headers or payloads.
const validations = new Map([
  [
    "Submit requires SourceWarehouseId and NeededAt.",
    "SUBMIT_CONTENT_INCOMPLETE",
  ],
  ["Current independent assigned approver is required.", "APPROVER_SEPARATION"],
  ["Supply exactly one approval quantity per active line.", "APPROVAL_LINES"],
  ["ApprovedQty exceeds requested.", "APPROVAL_QUANTITY"],
  [
    "Issuer must differ from approving actor of approved cycle.",
    "ISSUER_SEPARATION",
  ],
  ["Unsafe current quantities.", "FULFILLMENT_QUANTITIES"],
  [
    "Use FullRemaining without lines or Partial with explicit lines.",
    "FULFILLMENT_MODE",
  ],
  [
    "A nonempty batch of distinct request lines is required.",
    "FULFILLMENT_LINES",
  ],
  [
    "Issue quantity exceeds remaining or is nonpositive.",
    "FULFILLMENT_QUANTITY",
  ],
  ["Command is not allowed in the current state.", "WORKFLOW_STATE"],
]);
export function sanitizedHttpFailure(status, problem) {
  const category =
    status === 400 ? validations.get(problem?.detail) : undefined;
  return {
    category:
      category ?? (status === 400 ? "VALIDATION_UNCLASSIFIED" : "HTTP_STATUS"),
    message: category
      ? problem.detail
      : "HTTP response did not match the expected status; raw details omitted.",
    rawPayloadRecorded: false,
  };
}
