import type { RecordData } from "../domain";
import type {
  EntityDto,
  EntityWriteDto,
  IncidentDetailDto,
  IncidentListDto,
  MaterialLineDto,
  LineCommandDto,
} from "./types";
import { ApiError } from "./http";

/** Explicit SRS labels; legacy SQL values such as AVAILABLE/DIRTY are not guessed. */
const labels = [
  "Active",
  "Inactive",
  "Ready",
  "Pending Cleaning",
  "Cleaning In Progress",
  "Pending Inspection",
  "Rework Required",
  "Blocked",
  "Pending Assignment",
  "Assigned",
  "In Progress",
  "On Hold",
  "Completed",
  "Cancelled",
  "Draft",
  "Published",
  "Retired",
  "Inspecting",
  "Pending Conclusion",
  "Passed",
  "Failed",
  "Pending Acceptance",
  "Accepted",
  "Pending Reinspection",
  "Pending Triage",
  "Pending Verification",
  "Closed",
  "Effective",
  "Removed",
  "Open",
  "In Use",
  "Damaged",
  "Under Repair",
  "Pending Acknowledgment",
  "Acknowledged",
  "Rejected",
  "Posted",
  "In Transit",
  "Partially Received",
  "Received",
  "Reversed",
  "Counted",
  "Pending Adjustment",
  "Reconciled",
  "Pending Approval",
  "Needs Information",
  "Approved",
  "Partially Issued",
  "Fully Issued",
  "Remainder Closed",
  "Requested",
  "Fully Received",
  "Paused",
  "Working",
  "Ended",
  "Scheduled",
  "Pending",
  "Applied",
  "Unread",
  "Read",
  "Low",
  "Normal",
  "High",
  "Critical",
  "Receipt",
  "Issue",
  "Return",
  "Transfer",
  "Adjustment",
  "Reversal",
  "Pass",
  "Fail",
  "Fixed",
  "Not Fixed",
  "Floor",
  "Area",
  "Daily",
  "Weekly",
  "Monthly",
  "None",
  "Days",
  "Weeks",
  "Months",
  "Turnover",
  "Deep cleaning",
  "Recorded",
];
export const toCode = (label: string) =>
  label.trim().replaceAll(" ", "_").toUpperCase();
const labelMap = Object.fromEntries(
  labels.map((label) => [toCode(label), label]),
);
export const toLabel = (code: string) => labelMap[code] ?? code;
export function asId(value: unknown): string {
  if (typeof value === "string" && value.length) return value;
  if (typeof value === "number" && Number.isSafeInteger(value))
    return String(value);
  throw new ApiError(
    "The API returned a missing or unsafe identifier. Return SQL BIGINT identifiers as JSON strings.",
    0,
    "server",
  );
}
const optionalId = (value: unknown) =>
  value == null ? undefined : asId(value);
export const attributeNames: Record<string, string> = {
  incident: "incidentId",
  schedule: "scheduleId",
  recipient: "recipientId",
  room: "roomId",
  floor: "floorId",
  area: "areaId",
  roomType: "roomTypeId",
  assignee: "assigneeId",
  receiver: "receiverId",
  creator: "requesterId",
  approver: "approverId",
  warehouse: "warehouseId",
  destination: "destinationWarehouseId",
  asset: "assetId",
  inspection: "inspectionId",
  cleaning: "cleaningTaskId",
  checklist: "checklistId",
  rework: "reworkTaskId",
  parent: "parentFloorId",
  head: "managerUserId",
  due: "dueAt",
  actualStart: "actualStartAt",
  actualEnd: "actualEndAt",
  priority: "priorityCode",
  severity: "severityCode",
  condition: "conditionCode",
  type: "transactionTypeCode",
  kind: "kindCode",
  roles: "roleNames",
  minutes: "standardCleaningMinutes",
  detectedAt: "discoveredAt",
  plannedStart: "plannedStartAt",
  evidence: "attachmentIds",
  unit: "unitId",
  issue: "issueDocumentId",
  replenishment: "replenishmentId",
  intervalUnit: "intervalUnitCode",
  recurrence: "recurrenceCode",
  receivingDepartment: "receivingDepartmentId",
  assignees: "assigneeIds",
  start: "startAt",
  end: "endAt",
  cutoff: "cutoffAt",
  delta: "quantityDelta",
  item: "itemId",
  document: "documentId",
  recordId: "sourceRecordId",
  entity: "sourceResource",
  username: "username",
  email: "email",
  phone: "phone",
};
const enumFields = new Set([
  "priority",
  "severity",
  "condition",
  "type",
  "kind",
  "intervalUnit",
  "recurrence",
]);
const lineNames: Record<string, keyof MaterialLineDto> = {
  item: "itemId",
  unit: "unitId",
  quantity: "quantity",
  approved: "approvedQuantity",
  issued: "issuedQuantity",
  received: "receivedQuantity",
  returned: "returnedQuantity",
  system: "systemQuantity",
  factor: "conversionFactor",
  damaged: "damagedQuantity",
  missing: "missingQuantity",
  reason: "reason",
  id: "id",
};
export function lineFromDto(dto: MaterialLineDto) {
  return Object.fromEntries(
    Object.entries(lineNames)
      .filter(([, key]) => dto[key] !== undefined)
      .map(([key, api]) => [key, dto[api]]),
  );
}
export function lineToDto(line: Record<string, unknown>): MaterialLineDto {
  return {
    id: line.id as string | undefined,
    itemId: String(line.item),
    unitId: String(line.unit),
    quantity: Number(line.quantity),
  };
}
/** Each operation names its own quantity; cumulative approved/issued balances are read-only. */
export function commandLines(
  operation: string,
  lines: Record<string, unknown>[],
): LineCommandDto[] {
  const quantities: Record<string, keyof LineCommandDto> = {
    "transactions.post-receipt": "receivedQuantity",
    "transactions.post-return": "acceptedQuantity",
    "transactions.confirm-receipt": "receivedQuantity",
    "stocktakes.counts": "countedQuantity",
    "materials.approve": "approvedQuantity",
    "materials.partial-issue": "issuedQuantity",
    "materials.unused-returns": "returnedQuantity",
  };
  const quantityField = quantities[operation];
  if (!quantityField)
    throw new ApiError(
      "No line DTO has been agreed for this operation.",
      0,
      "configuration",
    );
  return lines.map((line) => {
    if (!line.id)
      throw new ApiError(
        "The API must supply line identifiers before submitting quantities.",
        400,
        "validation",
        {
          errors: { lines: ["Reload the record to obtain line identifiers."] },
        },
      );
    const result: LineCommandDto = {
      lineId: asId(line.id),
      [quantityField]: Number(line.quantity),
    };
    if (operation === "transactions.confirm-receipt") {
      result.damagedQuantity = Number(line.damaged ?? 0);
      result.missingQuantity = Number(line.missing ?? 0);
    }
    if (line.reason) result.reason = String(line.reason);
    return result;
  });
}
export function entityFromDto(dto: EntityDto): RecordData {
  if (
    !dto ||
    typeof dto.name !== "string" ||
    typeof dto.statusCode !== "string" ||
    !dto.attributes
  )
    throw new ApiError(
      "The API response does not match EntityDto. Check the proposed contract.",
      0,
      "server",
    );
  const values: Record<string, unknown> = { ...dto.attributes };
  for (const [ui, api] of Object.entries(attributeNames))
    if (dto.attributes[api] !== undefined) {
      delete values[api];
      values[ui] = enumFields.has(ui)
        ? toLabel(String(dto.attributes[api]))
        : api.endsWith("Id") && dto.attributes[api] != null
          ? asId(dto.attributes[api])
          : api.endsWith("Ids") && Array.isArray(dto.attributes[api])
            ? (dto.attributes[api] as unknown[]).map(asId)
            : dto.attributes[api];
    }
  if (dto.attributes.lines)
    values.lines = dto.attributes.lines.map(lineFromDto);
  if (dto.attributes.results)
    values.results = dto.attributes.results.map((r) => ({
      ...r,
      result: toLabel(r.resultCode),
    }));
  if (Array.isArray(dto.attributes.attachments))
    values.evidence = dto.attributes.attachments.map((file) => {
      const item = file as {
        id: string;
        name: string;
        url: string;
        contentType: string;
      };
      return {
        id: item.id,
        name: item.name,
        data: item.url,
        type: item.contentType,
      };
    });
  return {
    ...values,
    id: asId(dto.id),
    code: dto.code,
    name: dto.name,
    status: toLabel(dto.statusCode),
    statusCode: dto.statusCode,
    branch: dto.branchName,
    branchId: dto.branchId == null ? null : asId(dto.branchId),
    department: dto.departmentName,
    departmentId: dto.departmentId == null ? null : asId(dto.departmentId),
    rowVersion: dto.rowVersion,
    version: 0,
    createdAt: dto.createdAt,
    allowedActions: dto.allowedActions ?? [],
    canEdit: dto.canEdit === true,
  };
}
export function incidentFromDto(
  dto: IncidentListDto | IncidentDetailDto,
): RecordData {
  return {
    id: asId(dto.incidentId),
    code: dto.incidentNo,
    name: dto.incidentTitle,
    branchId: asId(dto.branchId),
    branch: dto.branchName,
    department: "",
    departmentId: null,
    version: 0,
    createdAt: dto.reportedAt,
    status: toLabel(dto.status),
    statusCode: dto.status,
    room: optionalId(dto.roomId),
    roomNumber: dto.roomNumber,
    area: optionalId(dto.areaId),
    asset: optionalId(dto.assetId),
    severity: dto.severityName ?? undefined,
    priority: dto.priorityName ?? undefined,
    creator: asId(dto.reportedBy),
    detectedAt: dto.discoveredAt,
    actualEnd: dto.resolvedAt,
    closedAt: dto.closedAt,
    description: "description" in dto ? dto.description : "",
    events: "events" in dto ? dto.events : [],
    allowedActions: [],
    canEdit: false,
  };
}
/** Input whitelist belongs to the domain schema; status/stock/audit never enter this DTO. */
export function entityToDto(
  input: Record<string, unknown>,
  fields: string[],
  resolve: (resource: string, name: string) => string,
): EntityWriteDto {
  const attributes: Record<string, unknown> = {};
  for (const key of fields) {
    if (
      [
        "name",
        "code",
        "branch",
        "department",
        "status",
        "version",
        "rowVersion",
      ].includes(key) ||
      input[key] === undefined
    )
      continue;
    let value = input[key];
    if (value === "") {
      attributes[attributeNames[key] ?? key] = null;
      continue;
    }
    if (enumFields.has(key)) value = toCode(String(value));
    if (["due", "detectedAt", "plannedStart"].includes(key))
      value = localDateTimeToUtc(String(value));
    if (key === "receivingDepartment")
      value = resolve("departments", String(value));
    if (key === "lines")
      value = (value as Record<string, unknown>[]).map(lineToDto);
    if (key === "evidence")
      value = (value as Array<string | { id: string }>).map((file) =>
        typeof file === "string" ? file : file.id,
      );
    if (key === "results")
      value = (
        value as Array<{ text: string; result: string; defect: string }>
      ).map((r) => ({
        text: r.text,
        resultCode: toCode(r.result),
        defect: r.defect,
      }));
    attributes[attributeNames[key] ?? key] = value;
  }
  return {
    name: input.name as string,
    code: input.code as string | undefined,
    branchId: input.branch
      ? resolve("branches", String(input.branch))
      : undefined,
    departmentId: input.department
      ? resolve("departments", String(input.department))
      : undefined,
    rowVersion: input.rowVersion as string | undefined,
    attributes,
  };
}
export function localDateTimeToUtc(value: string) {
  if (!value) return value;
  return new Date(
    /(?:Z|[+-]\d{2}:\d{2})$/.test(value)
      ? value
      : `${value}${value.length === 16 ? ":00" : ""}+07:00`,
  ).toISOString();
}
export function formFieldForApiPath(path: string) {
  const stripped = path
    .replace(/^\$\./, "")
    .replace(/^attributes\./i, "")
    .replace(/^[A-Z]/, (c) => c.toLowerCase());
  if (/\b(?:lines|Lines)\[/.test(stripped)) return "lines";
  if (/\b(?:results|Results)\[/.test(stripped)) return "results";
  const special: Record<string, string> = {
    branchId: "branch",
    departmentId: "department",
    currentPassword: "current",
    newPassword: "next",
    attachmentIds: "evidence",
  };
  return (
    special[stripped] ??
    Object.entries(attributeNames).find(([, api]) => api === stripped)?.[0] ??
    stripped
  );
}
