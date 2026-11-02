import catalog from "./catalog.json";
export type RecordData = {
  id: string;
  name: string;
  status: string;
  branch: string;
  department: string;
  version: number;
  createdAt: string;
  [key: string]: any;
};
export type Field = {
  key: string;
  label: string;
  type?: string;
  options?: string[];
  required?: boolean;
  min?: number;
  useIds?: boolean;
};
export type Domain = {
  key: string;
  list: number;
  detail: number;
  createUC?: string;
  updateUC?: string;
  fields: Field[];
  initial: string;
};
const field = (
  key: string,
  label: string,
  type = "text",
  required = true,
  options?: string[],
): Field => ({ key, label, type, required, options });
export const roles = [
  "Administrator",
  "Operations Manager",
  "Owner",
  "Housekeeping Supervisor",
  "Housekeeping Staff",
  "Maintenance Lead",
  "Maintenance Staff",
  "Storekeeper",
  "Department Head",
];
export const branchNames = ["Bui Vien", "Cao Thang"];
export const departments = [
  "Housekeeping",
  "Maintenance",
  "Warehouse",
  "Operations",
];
const base = [
  field("code", "Code"),
  field("name", "Name"),
  field("description", "Description", "textarea", false),
];
const location = [
  field("branch", "Branch", "select", true, branchNames),
  field("department", "Department", "select", true, departments),
];
const work = [
  field("name", "Title"),
  ...location,
  field("room", "Room", "room", false),
  field("priority", "Priority", "select", true, [
    "Low",
    "Normal",
    "High",
    "Critical",
  ]),
  field("due", "Deadline", "datetime-local"),
  field("description", "Instructions / expected outcome", "textarea"),
  field("criteria", "Mandatory criteria (one per line)", "textarea"),
];
const lines = [
  field("warehouse", "Warehouse", "warehouse"),
  field("lines", "Materials", "lines"),
];
export const domains: Domain[] = [
  {
    key: "users",
    list: 8,
    detail: 9,
    createUC: "UC-004",
    updateUC: "UC-005",
    initial: "Active",
    fields: [
      field("name", "Full name"),
      field("username", "Username"),
      field("email", "Email", "email"),
      field("phone", "Phone", "tel", false),
      ...location,
      field("roles", "Roles", "multiselect", true, roles),
    ],
  },
  {
    key: "roles",
    list: 10,
    detail: 11,
    createUC: "UC-008",
    updateUC: "UC-008",
    initial: "Active",
    fields: base,
  },
  {
    key: "branches",
    list: 13,
    detail: 14,
    createUC: "UC-012",
    updateUC: "UC-012",
    initial: "Active",
    fields: [
      ...base,
      field("address", "Address"),
      field("phone", "Contact phone", "tel", false),
      field("email", "Contact email", "email", false),
    ],
  },
  {
    key: "locations",
    list: 15,
    detail: 16,
    createUC: "UC-014",
    updateUC: "UC-015",
    initial: "Active",
    fields: [
      ...base,
      field("branch", "Branch", "select", true, branchNames),
      field("kind", "Location type", "select", true, ["Floor", "Area"]),
      field("parent", "Parent floor", "location", false),
    ],
  },
  {
    key: "roomTypes",
    list: 17,
    detail: 18,
    createUC: "UC-017",
    updateUC: "UC-018",
    initial: "Active",
    fields: [
      ...base,
      field("branch", "Branch", "select", true, branchNames),
      field("minutes", "Standard cleaning minutes", "number"),
      field("checklist", "Default published checklist", "checklist"),
    ],
  },
  {
    key: "rooms",
    list: 19,
    detail: 20,
    createUC: "UC-020",
    updateUC: "UC-021",
    initial: "Pending Cleaning",
    fields: [
      field("name", "Room number"),
      field("branch", "Branch", "select", true, branchNames),
      field("floor", "Floor", "location"),
      field("roomType", "Room type", "roomType"),
      field("description", "Notes", "textarea", false),
    ],
  },
  {
    key: "departments",
    list: 21,
    detail: 22,
    createUC: "UC-024",
    updateUC: "UC-025",
    initial: "Active",
    fields: [
      ...base,
      field("branch", "Branch", "select", true, branchNames),
      field("head", "Department Head", "user", false),
    ],
  },
  {
    key: "cleaning",
    list: 24,
    detail: 26,
    createUC: "UC-028",
    updateUC: "UC-032",
    initial: "Pending Assignment",
    fields: [
      ...work.map((f) => (f.key === "room" ? { ...f, required: true } : f)),
      field("cleaningType", "Cleaning type", "select", true, [
        "Turnover",
        "Daily",
        "Deep cleaning",
      ]),
    ],
  },
  {
    key: "rework",
    list: 28,
    detail: 29,
    createUC: "UC-046",
    initial: "Pending Acceptance",
    fields: [
      ...work,
      field("inspection", "Failed inspection", "inspection"),
      field("assignee", "Housekeeper", "housekeeper"),
      field("defects", "Defects / remediation notes", "textarea"),
    ],
  },
  {
    key: "checklists",
    list: 31,
    detail: 32,
    createUC: "UC-039",
    updateUC: "UC-040",
    initial: "Draft",
    fields: [
      ...base,
      field(
        "criteria",
        "Criteria (one mandatory criterion per line)",
        "textarea",
      ),
    ],
  },
  {
    key: "inspections",
    list: 33,
    detail: 35,
    createUC: "UC-041",
    initial: "Assigned",
    fields: [
      field("name", "Inspection name"),
      field("room", "Room", "room"),
      field("cleaning", "Cleaning cycle", "cleaning"),
      field("checklist", "Published checklist", "checklist"),
      field("assignee", "Inspector", "inspector"),
      field("due", "Due time", "datetime-local"),
    ],
  },
  {
    key: "incidents",
    list: 39,
    detail: 41,
    createUC: "UC-049",
    initial: "Pending Triage",
    fields: [
      field("name", "Title"),
      field("branch", "Branch", "select", true, branchNames),
      field("room", "Room", "room", false),
      field("area", "Area", "location", false),
      field("asset", "Asset", "asset", false),
      field("description", "Description", "textarea"),
      field("detectedAt", "Detected time", "datetime-local"),
      field("evidence", "Evidence", "file", false),
    ],
  },
  {
    key: "schedules",
    list: 45,
    detail: 46,
    createUC: "UC-061",
    updateUC: "UC-061",
    initial: "Active",
    fields: [
      ...work,
      field("area", "Area", "location", false),
      field("asset", "Asset", "asset", false),
      field("interval", "Interval", "number"),
      field("intervalUnit", "Interval unit", "select", true, [
        "Days",
        "Weeks",
        "Months",
      ]),
      field("assignee", "Default technician", "technician", false),
    ],
  },
  {
    key: "reference",
    list: 47,
    detail: 47,
    createUC: "UC-064",
    updateUC: "UC-064",
    initial: "Active",
    fields: [
      ...base,
      field("group", "Group", "select", true, [
        "Incident category",
        "Work category",
        "Priority",
        "Interruption reason",
        "Request type",
        "Cleaning type",
      ]),
      field("responseMinutes", "Response target (minutes)", "number", false),
      field(
        "resolutionMinutes",
        "Resolution target (minutes)",
        "number",
        false,
      ),
    ],
  },
  {
    key: "assets",
    list: 48,
    detail: 49,
    createUC: "UC-065",
    updateUC: "UC-066",
    initial: "Active",
    fields: [
      ...base,
      ...location,
      field("category", "Category"),
      field("model", "Brand / model"),
      field("serial", "Serial number", "text", false),
      field("purchaseDate", "Purchase date", "date", false),
      field("warrantyEnd", "Warranty end", "date", false),
    ],
  },
  {
    key: "warehouses",
    list: 51,
    detail: 52,
    createUC: "UC-075",
    updateUC: "UC-075",
    initial: "Active",
    fields: [...base, ...location],
  },
  {
    key: "items",
    list: 54,
    detail: 55,
    createUC: "UC-076",
    updateUC: "UC-076",
    initial: "Active",
    fields: [
      ...base,
      field("category", "Type"),
      field("unit", "Base unit", "select", true, [
        "Piece",
        "Bottle",
        "Litre",
        "Kilogram",
        "Box",
      ]),
      field("minimum", "Minimum stock", "number"),
      field("warehouse", "Warehouse", "warehouse"),
    ],
  },
  {
    key: "units",
    list: 56,
    detail: 56,
    createUC: "UC-077",
    updateUC: "UC-077",
    initial: "Active",
    fields: [
      ...base,
      field("factor", "Conversion factor to base unit", "number"),
    ],
  },
  {
    key: "transactions",
    list: 57,
    detail: 58,
    createUC: "UC-079",
    initial: "Draft",
    fields: [
      field("name", "Reference document"),
      field("type", "Transaction type", "select", true, [
        "Receipt",
        "Transfer",
        "Return",
      ]),
      ...location,
      ...lines,
      field("destination", "Destination warehouse", "warehouse", false),
      field("source", "Supplier / source"),
      field("replenishment", "Linked replenishment", "replenishment", false),
      field("issue", "Original issue", "transaction", false),
    ],
  },
  {
    key: "stocktakes",
    list: 59,
    detail: 60,
    createUC: "UC-085",
    initial: "In Progress",
    fields: [
      field("name", "Stocktake reference"),
      ...location,
      field("warehouse", "Warehouse", "warehouse"),
    ],
  },
  {
    key: "materials",
    list: 62,
    detail: 63,
    createUC: "UC-091",
    updateUC: "UC-093",
    initial: "Pending Approval",
    fields: [
      field("name", "Purpose"),
      ...location,
      ...lines,
      field("due", "Needed by", "datetime-local"),
      field("linkedWork", "Linked work", "work", false),
      field("submission", "Save as", "select", true, ["Submit", "Save draft"]),
    ],
  },
  {
    key: "replenishments",
    list: 64,
    detail: 65,
    createUC: "UC-100",
    initial: "Requested",
    fields: [
      field("name", "Replenishment reference"),
      ...location,
      ...lines,
      field("supplier", "Supplier", "text", false),
      field("due", "Expected date", "datetime-local", false),
    ],
  },
  {
    key: "tasks",
    list: 66,
    detail: 67,
    createUC: "UC-102",
    updateUC: "UC-103",
    initial: "Pending Assignment",
    fields: [
      ...work,
      field("category", "Work category"),
      field("plannedStart", "Planned start", "datetime-local"),
      field("recurrence", "Repeat", "select", false, [
        "None",
        "Daily",
        "Weekly",
        "Monthly",
      ]),
      field("weekdays", "Weekdays", "multiselect", false, [
        "Sunday",
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday",
        "Saturday",
      ]),
      field("monthDay", "Day of month", "number", false),
      field("seriesEnd", "Repeat until", "date", false),
    ],
  },
  {
    key: "shifts",
    list: 71,
    detail: 72,
    createUC: "UC-138",
    updateUC: "UC-138",
    initial: "Scheduled",
    fields: [
      ...base,
      ...location,
      field("date", "Date", "date"),
      field("start", "Start", "time"),
      field("end", "End", "time"),
      field("assignees", "Employees", "employees"),
    ],
  },
  {
    key: "handovers",
    list: 74,
    detail: 75,
    createUC: "UC-140",
    initial: "Pending Acknowledgment",
    fields: [
      field("name", "Shift handover"),
      ...location,
      field("receiver", "Next shift receiver", "user"),
      field("description", "Handover notes", "textarea"),
    ],
  },
  {
    key: "requests",
    list: 76,
    detail: 77,
    createUC: "UC-114",
    initial: "Pending Acceptance",
    fields: [
      ...work,
      field(
        "receivingDepartment",
        "Receiving department",
        "select",
        true,
        departments,
      ),
      field("requestType", "Request type", "select", true, [
        "Operational support",
        "Guest service support",
        "Facility support",
      ]),
    ],
  },
  { key: "notifications", list: 6, detail: 7, initial: "Unread", fields: [] },
  {
    key: "maintenance",
    list: 43,
    detail: 44,
    initial: "Assigned",
    fields: [
      ...work,
      field("asset", "Asset", "asset", false),
      field("incident", "Source incident", "text", false),
      field("schedule", "Preventive schedule", "text", false),
    ],
  },
  { key: "audit", list: 79, detail: 80, initial: "Recorded", fields: [] },
];
export const screens = catalog.screens;
export const useCases = catalog.ucs as Record<
  string,
  {
    id: string;
    name: string;
    actors: string[];
    spec?: Record<string, string>;
    rules: string[];
  }
>;
export const businessRules = catalog.rules as Record<string, string>;
export const screenById = (id: number) => screens.find((s) => s.id === id)!;
export const pathFor = (id: number, record = "") =>
  screenById(id).route.replace(":id", encodeURIComponent(record));
export const domainFor = (id: number): Domain => {
  const direct = domains.find((d) => d.list === id || d.detail === id);
  if (direct) return direct;
  const pairs: Record<number, string> = {
    23: "rooms",
    25: "cleaning",
    27: "cleaning",
    30: "cleaning",
    34: "inspections",
    36: "rework",
    37: "inspections",
    38: "inspections",
    40: "incidents",
    42: "incidents",
    43: "incidents",
    44: "incidents",
    50: "incidents",
    53: "items",
    61: "transactions",
    68: "tasks",
    69: "tasks",
    70: "tasks",
    73: "shifts",
    78: "tasks",
    12: "roles",
    3: "users",
    4: "users",
    5: "rooms",
  };
  return domains.find((d) => d.key === pairs[id]) || domains[0];
};
export type Action = {
  label: string;
  uc: string;
  from?: string[];
  to?: string;
  fields?: Field[];
  reason?: boolean;
  own?: boolean;
};
const action = (
  label: string,
  uc: string,
  from?: string[],
  to?: string,
  fields?: Field[],
  reason = false,
  own = false,
): Action => ({ label, uc, from, to, fields, reason, own });
const note = [field("note", "Result / notes", "textarea")];
const progress = [field("progress", "Progress (%)", "number"), ...note];
export const actions: Record<string, Action[]> = {
  maintenance: [
    action(
      "Assign scheduled work",
      "UC-062",
      ["Pending Assignment", "Assigned"],
      "Assigned",
      [field("assignee", "Technician", "technician"), ...note],
    ),
    action(
      "Start scheduled maintenance",
      "UC-062",
      ["Assigned"],
      "In Progress",
      [],
      false,
      true,
    ),
    action(
      "Complete scheduled maintenance",
      "UC-062",
      ["In Progress"],
      "Completed",
      [field("results", "Check item results", "results"), ...note],
      false,
      true,
    ),
    action("Report discovered problem", "UC-049", undefined, undefined, [
      field("name", "Incident title"),
      field("description", "Description", "textarea"),
    ]),
  ],
  users: [
    action("Deactivate", "UC-006", ["Active"], "Inactive", [], true),
    action("Activate", "UC-006", ["Inactive"], "Active", [], true),
    action("Reset password", "UC-005"),
    action("Assign scope", "UC-007", undefined, undefined, location),
    action("Assign roles", "UC-010", undefined, undefined, [
      field("roles", "Roles", "multiselect", true, roles),
    ]),
  ],
  roles: [
    action("Configure permissions", "UC-009", undefined, undefined, [
      field("permissions", "Permissions", "permissions"),
    ]),
    action("Deactivate", "UC-008", ["Active"], "Inactive", [], true),
  ],
  branches: [action("Deactivate", "UC-012", ["Active"], "Inactive", [], true)],
  locations: [action("Deactivate", "UC-016", ["Active"], "Inactive", [], true)],
  roomTypes: [action("Deactivate", "UC-019", ["Active"], "Inactive", [], true)],
  rooms: [
    action("Deactivate", "UC-022", undefined, "Inactive", [], true),
    action("Reactivate", "UC-022", ["Inactive"], "Pending Cleaning", [], true),
  ],
  departments: [
    action("Deactivate", "UC-026", ["Active"], "Inactive", [], true),
  ],
  cleaning: [
    action(
      "Start / resume",
      "UC-031",
      ["Assigned", "On Hold"],
      "In Progress",
      note,
      false,
      true,
    ),
    action(
      "Update progress",
      "UC-032",
      ["In Progress"],
      undefined,
      progress,
      false,
      true,
    ),
    action(
      "Report blocker",
      "UC-033",
      ["In Progress"],
      "On Hold",
      note,
      false,
      true,
    ),
    action(
      "Submit for inspection",
      "UC-034",
      ["In Progress"],
      "Pending Inspection",
      note,
      false,
      true,
    ),
    action(
      "Cancel",
      "UC-037",
      ["Pending Assignment", "Assigned"],
      "Cancelled",
      [],
      true,
    ),
  ],
  rework: [
    action(
      "Accept",
      "UC-035",
      ["Pending Acceptance"],
      "Accepted",
      [],
      false,
      true,
    ),
    action("Start", "UC-035", ["Accepted"], "In Progress", [], false, true),
    action(
      "Submit rework",
      "UC-036",
      ["In Progress"],
      "Pending Reinspection",
      [
        field(
          "remediation",
          "Remediation for every defect (one per line)",
          "textarea",
        ),
      ],
      false,
      true,
    ),
  ],
  checklists: [
    action("Publish", "UC-039", ["Draft"], "Published"),
    action("Retire", "UC-040", ["Published"], "Retired", [], true),
  ],
  inspections: [
    action(
      "Start inspection",
      "UC-042",
      ["Assigned"],
      "Inspecting",
      [],
      false,
      true,
    ),
    action(
      "Record results",
      "UC-042",
      ["Inspecting"],
      undefined,
      [field("results", "Criterion results", "results")],
      false,
      true,
    ),
    action(
      "Finish inspection",
      "UC-042",
      ["Inspecting"],
      "Pending Conclusion",
      [],
      false,
      true,
    ),
    action(
      "Approve",
      "UC-044",
      ["Pending Conclusion"],
      "Passed",
      note,
      false,
      true,
    ),
    action(
      "Reject",
      "UC-045",
      ["Pending Conclusion"],
      "Failed",
      [],
      true,
      true,
    ),
    action(
      "Conclude reinspection",
      "UC-047",
      ["Assigned"],
      undefined,
      [field("results", "Defect results", "results")],
      false,
      true,
    ),
  ],
  incidents: [
    action("Classify", "UC-050", ["Pending Triage"], undefined, [
      field("category", "Category"),
      field("severity", "Severity", "select", true, [
        "Low",
        "Normal",
        "High",
        "Critical",
      ]),
    ]),
    action("Add evidence", "UC-051", undefined, undefined, [
      ...note,
      field("evidence", "Files", "file", false),
    ]),
    action("Triage", "UC-052", ["Pending Triage"], "Pending Assignment", [
      field("priority", "Priority", "select", true, [
        "Low",
        "Normal",
        "High",
        "Critical",
      ]),
      field("responseDue", "Response deadline", "datetime-local"),
      field("due", "Resolution deadline", "datetime-local"),
      ...note,
    ]),
    action(
      "Accept and start",
      "UC-054",
      ["Assigned"],
      "In Progress",
      [],
      false,
      true,
    ),
    action(
      "Update progress",
      "UC-055",
      ["In Progress"],
      undefined,
      progress,
      false,
      true,
    ),
    action("Put on hold", "UC-055", ["In Progress"], "On Hold", [], true, true),
    action("Resume", "UC-055", ["On Hold"], "In Progress", note, false, true),
    action(
      "Block room",
      "UC-056",
      undefined,
      undefined,
      [field("room", "Room", "room")],
      true,
    ),
    action("Remove block", "UC-056", undefined, undefined, [], true),
    action("Escalate", "UC-057", undefined, undefined, note, true),
    action(
      "Submit resolution",
      "UC-058",
      ["In Progress"],
      "Pending Verification",
      [
        field("diagnosis", "Diagnosis", "textarea"),
        field("actionTaken", "Action taken", "textarea"),
      ],
      false,
      true,
    ),
    action(
      "Verify and close",
      "UC-059",
      ["Pending Verification"],
      "Closed",
      note,
    ),
    action(
      "Return result",
      "UC-059",
      ["Pending Verification"],
      "In Progress",
      [],
      true,
    ),
    action("Reopen", "UC-060", ["Closed"], "Pending Triage", note, true),
  ],
  schedules: [action("Deactivate", "UC-061", ["Active"], "Inactive", [], true)],
  assets: [
    action("Assign location", "UC-067", undefined, undefined, [
      field("room", "Room", "room"),
    ]),
    action(
      "Transfer location",
      "UC-068",
      undefined,
      undefined,
      [field("room", "New room", "room")],
      true,
    ),
    action("Hand over", "UC-069", undefined, undefined, [
      field(
        "receivingDepartment",
        "Receiving department",
        "select",
        true,
        departments,
      ),
      field("receiver", "Receiving Department Head", "user"),
      ...note,
    ]),
    action("Acknowledge handover", "UC-069", undefined, undefined),
    action("Update condition", "UC-070", undefined, undefined, [
      field("condition", "Condition", "select", true, [
        "In Use",
        "Damaged",
        "Under Repair",
      ]),
      ...note,
    ]),
    action("Retire", "UC-074", ["Active"], "Retired", [], true),
  ],
  warehouses: [
    action("Deactivate", "UC-075", ["Active"], "Inactive", [], true),
  ],
  items: [
    action("Stock minimum", "UC-078", undefined, undefined, [
      field("warehouse", "Warehouse", "warehouse"),
      field("minimum", "Minimum stock", "number"),
    ]),
    action("Add conversion", "UC-077", undefined, undefined, [
      field("unit", "Additional unit"),
      field("factor", "Positive conversion factor", "number"),
    ]),
  ],
  transactions: [
    action("Post receipt", "UC-080", ["Draft"], "Posted", [
      field("lines", "Actual received quantities", "lines"),
    ]),
    action("Post return", "UC-082", ["Draft"], "Posted", [
      field("lines", "Accepted usable quantities", "lines"),
    ]),
    action("Dispatch transfer", "UC-083", ["Draft"], "In Transit"),
    action(
      "Confirm receipt",
      "UC-084",
      ["In Transit", "Partially Received"],
      undefined,
      [field("lines", "Received quantities", "lines")],
    ),
    action("Reverse", "UC-088", ["Posted"], "Reversed", [], true),
  ],
  stocktakes: [
    action("Record counts", "UC-085", ["In Progress"], undefined, [
      field("lines", "Counted quantities", "lines"),
    ]),
    action("Submit counts", "UC-085", ["In Progress"], "Counted"),
    action("Reconcile", "UC-086", ["Counted"], "Pending Adjustment", note),
    action(
      "Approve adjustment",
      "UC-087",
      ["Pending Adjustment"],
      "Reconciled",
      note,
    ),
  ],
  materials: [
    action(
      "Close unissued remainder",
      "UC-095",
      ["Partially Issued"],
      "Remainder Closed",
      [],
      true,
    ),
    action("Link to work", "UC-092", undefined, undefined, [
      field("linkedWork", "Open work record", "work"),
    ]),
    action(
      "Cancel",
      "UC-093",
      ["Draft", "Pending Approval", "Needs Information", "Approved"],
      "Cancelled",
      [],
      true,
      true,
    ),
    action(
      "Request information",
      "UC-094",
      ["Pending Approval"],
      "Needs Information",
      note,
    ),
    action("Approve", "UC-095", ["Pending Approval"], "Approved", [
      field("lines", "Approved quantities", "lines"),
    ]),
    action("Reject", "UC-095", ["Pending Approval"], "Rejected", [], true),
    action(
      "Issue remaining",
      "UC-097",
      ["Approved", "Partially Issued"],
      undefined,
      [
        field("warehouse", "Warehouse", "warehouse"),
        field("receiver", "Receiver", "user"),
      ],
    ),
    action(
      "Partial issue",
      "UC-098",
      ["Approved", "Partially Issued"],
      undefined,
      [
        field("warehouse", "Warehouse", "warehouse"),
        field("lines", "Issue quantities", "lines"),
        field("receiver", "Receiver", "user"),
      ],
    ),
    action(
      "Return unused materials",
      "UC-090",
      ["Partially Issued", "Fully Issued"],
      undefined,
      [field("lines", "Return quantities", "lines")],
      true,
      true,
    ),
  ],
  replenishments: [
    action(
      "Close remainder",
      "UC-101",
      ["Requested", "Partially Received"],
      "Closed",
      [],
      true,
    ),
  ],
  tasks: [
    action("Stop future repetitions", "UC-102", undefined, undefined, [], true),
    action("Accept", "UC-107", ["Assigned"], "Accepted", [], false, true),
    action(
      "Decline",
      "UC-107",
      ["Assigned"],
      "Pending Assignment",
      [],
      true,
      true,
    ),
    action("Start", "UC-108", ["Accepted"], "In Progress", [], false, true),
    action(
      "Update progress",
      "UC-109",
      ["In Progress"],
      undefined,
      progress,
      false,
      true,
    ),
    action("Pause", "UC-110", ["In Progress"], "Paused", [], true, true),
    action("Resume", "UC-110", ["Paused"], "In Progress", [], false, true),
    action(
      "Submit result",
      "UC-112",
      ["In Progress"],
      "Pending Verification",
      note,
      false,
      true,
    ),
    action("Verify", "UC-105", ["Pending Verification"], "Closed", note),
    action(
      "Return result",
      "UC-105",
      ["Pending Verification"],
      "Accepted",
      [],
      true,
    ),
    action("Reopen", "UC-106", ["Closed"], "Assigned", note, true),
    action(
      "Cancel",
      "UC-103",
      ["Pending Assignment", "Assigned", "Accepted"],
      "Cancelled",
      [],
      true,
    ),
  ],
  handovers: [
    action("Acknowledge", "UC-140", ["Pending Acknowledgment"], "Acknowledged"),
  ],
  requests: [
    action("Link existing work", "UC-118", undefined, undefined, [
      field("linkedWork", "Work record", "work"),
      field("mandatory", "Required before completion", "select", true, [
        "Yes",
        "No",
      ]),
    ]),
    action("Accept", "UC-116", ["Pending Acceptance"], "Accepted", note),
    action("Reject", "UC-116", ["Pending Acceptance"], "Rejected", [], true),
    action(
      "Route",
      "UC-115",
      ["Pending Acceptance"],
      undefined,
      [
        field(
          "receivingDepartment",
          "Receiving department",
          "select",
          true,
          departments,
        ),
      ],
      true,
    ),
    action(
      "Assign handler",
      "UC-115",
      ["Accepted", "In Progress"],
      "In Progress",
      [field("assignee", "Handler", "user")],
    ),
    action("Update progress", "UC-117", ["In Progress"], undefined, progress),
    action("Hand over", "UC-119", ["In Progress"], undefined, [
      field("receiver", "New handler", "user"),
      ...note,
    ]),
    action("Acknowledge handover", "UC-119", ["In Progress"]),
    action("Complete", "UC-120", ["In Progress"], "Closed", note),
    action("Reopen", "UC-121", ["Closed"], "In Progress", [], true),
    action("Escalate", "UC-122", undefined, undefined, note, true),
    action(
      "Withdraw",
      "UC-123",
      ["Pending Acceptance"],
      "Cancelled",
      [],
      true,
      true,
    ),
  ],
  notifications: [
    action("Mark as read", "UC-130", ["Unread"], "Read"),
    action("Acknowledge", "UC-130", ["Unread", "Read"], "Acknowledged"),
  ],
};
