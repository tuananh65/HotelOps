import { domains, roles, useCases, type RecordData } from "../domain";
import type { Database } from "../services";
export function createDatabase(
  balance: (db: Database, warehouse: string, item: string) => number,
  calculateRooms: (db: Database) => void,
): Database {
  const sequence = new Map<string, number>();
  const record = (
    code: string,
    name: string,
    extra: Partial<RecordData> = {},
  ): RecordData => {
    const prefix =
      (String(extra.branch || "Bui Vien") === "Cao Thang" ? "CT" : "BV") +
      "-" +
      code.replace(/[^A-Za-z0-9-]/g, "").toUpperCase();
    const n = (sequence.get(prefix) || 0) + 1;
    sequence.set(prefix, n);
    return {
      id: prefix + "-" + n,
      code,
      name,
      branch: "Bui Vien",
      department: "Operations",
      status: "Active",
      version: 1,
      createdAt: new Date().toISOString(),
      ...extra,
    };
  };
  const now = () => new Date().toISOString();
  const db: Database = Object.fromEntries(domains.map((d) => [d.key, []]));
  db.intervals = [];
  db.workdays = [];
  db.ledger = [];
  db.blocks = [];
  db.corrections = [];
  db.occurrences = [];
  db.alertEpisodes = [];
  db.branches = [
    record("BV", "Bui Vien", {
      address: "195 Bui Vien, District 1, Ho Chi Minh City",
      phone: "+84 28 3920 6991",
    }),
    record("CT", "Cao Thang", {
      branch: "Cao Thang",
      address: "348 Cao Thang, District 10, Ho Chi Minh City",
    }),
  ];
  db.departments = [
    "Housekeeping",
    "Maintenance",
    "Warehouse",
    "Operations",
  ].flatMap((dep) =>
    ["Bui Vien", "Cao Thang"].map((branch) =>
      record(dep.slice(0, 3).toUpperCase(), dep, { branch, department: dep }),
    ),
  );
  db.roles = roles.map((name) =>
    record(name, name, {
      permissions: Object.values(useCases)
        .filter((uc) => uc.actors.includes(name))
        .map((uc) => uc.id),
      system: true,
    }),
  );
  db.users = roles.flatMap((role, i) =>
    ["Bui Vien", "Cao Thang"].map((branch, b) =>
      record(`U${i}${b}`, role + (b ? " · Cao Thang" : ""), {
        username:
          [
            "admin",
            "manager",
            "owner",
            "hksupervisor",
            "housekeeper",
            "maintenancelead",
            "technician",
            "storekeeper",
            "head",
          ][i] + (b ? "ct" : ""),
        email: `staff${i}${b}@ducvuong.example`,
        roles: [role],
        branch,
        department: role.includes("Housekeeping")
          ? "Housekeeping"
          : role.includes("Maintenance")
            ? "Maintenance"
            : role === "Storekeeper"
              ? "Warehouse"
              : "Operations",
        password: "HotelOps@2026",
        failed: 0,
        lockedUntil: 0,
        temporary: false,
      }),
    ),
  );
  db.locations = ["Bui Vien", "Cao Thang"].flatMap((branch) =>
    [1, 2, 3].map((n) =>
      record(`F${n}`, `Floor ${n}`, { branch, kind: "Floor" }),
    ),
  );
  for (const user of db.users.filter((u) =>
    u.roles.some((r: string) =>
      ["Administrator", "Operations Manager", "Owner"].includes(r),
    ),
  ))
    user.branches = ["Bui Vien", "Cao Thang"];
  for (const branch of ["Bui Vien", "Cao Thang"])
    for (const dep of ["Housekeeping", "Maintenance", "Warehouse"])
      db.users.push(
        record("DH-" + dep, `${dep} Department Head`, {
          branch,
          department: dep,
          roles: ["Department Head"],
          username: `head${dep.toLowerCase()}${branch === "Cao Thang" ? "ct" : ""}`,
          email: `head${dep.toLowerCase()}${branch === "Cao Thang" ? "ct" : ""}@ducvuong.example`,
          password: "HotelOps@2026",
          failed: 0,
          lockedUntil: 0,
        }),
      );
  db.users.push(
    record("ML2", "Maintenance verifier", {
      roles: ["Maintenance Lead"],
      department: "Maintenance",
      username: "maintenancelead2",
      email: "maintenancelead2@ducvuong.example",
      password: "HotelOps@2026",
      failed: 0,
      lockedUntil: 0,
    }),
  );
  db.checklists = [
    record("CL01", "Boutique room quality standard", {
      status: "Published",
      criteria:
        "Bed linen clean\nBathroom sanitised\nAmenities replenished\nEquipment functional",
      checklistVersion: 1,
    }),
  ];
  db.roomTypes = ["Bui Vien", "Cao Thang"].map((branch) =>
    record("DLX", "Deluxe", {
      branch,
      minutes: 35,
      checklist: db.checklists[0].id,
    }),
  );
  db.rooms = db.locations.flatMap((f) =>
    [1, 2, 3, 4].map((n) =>
      record(`${f.name.slice(-1)}0${n}`, `${f.name.slice(-1)}0${n}`, {
        branch: f.branch,
        floor: f.id,
        roomType: db.roomTypes.find((t) => t.branch === f.branch)!.id,
        status: n === 1 ? "Ready" : "Pending Cleaning",
        active: true,
        passed: n === 1,
      }),
    ),
  );
  const hk = db.users.find((u) => u.username === "housekeeper")!;
  const tech = db.users.find((u) => u.username === "technician")!;
  const sup = db.users.find((u) => u.username === "hksupervisor")!;
  const deadline = new Date(Date.now() + 4 * 3600000).toISOString();
  db.cleaning = db.rooms
    .filter((r) => r.branch === "Bui Vien" && !r.passed)
    .slice(0, 5)
    .map((r, i) =>
      record(`CLN-${i + 1}`, `Turnover · Room ${r.name}`, {
        room: r.id,
        branch: r.branch,
        department: "Housekeeping",
        status:
          i === 0
            ? "Pending Inspection"
            : i === 1
              ? "Assigned"
              : "Pending Assignment",
        assignee: i < 2 ? hk.id : "",
        performedBy: i === 0 ? hk.id : undefined,
        criteria: db.checklists[0].criteria,
        completedItems: i === 0 ? db.checklists[0].criteria.split("\n") : [],
        due: deadline,
        priority: i === 2 ? "High" : "Normal",
        cleaningType: "Turnover",
        description: "Prepare room to Duc Vuong quality standard.",
      }),
    );
  db.inspections = [
    record("INS-001", "Room quality inspection", {
      department: "Housekeeping",
      room: db.cleaning[0].room,
      cleaning: db.cleaning[0].id,
      assignee: sup.id,
      checklist: db.checklists[0].id,
      criteria: db.checklists[0].criteria,
      checklistVersion: 1,
      status: "Assigned",
      due: deadline,
    }),
  ];
  db.incidents = [
    record("INC-001", "Air conditioner not cooling", {
      department: "Maintenance",
      room: db.rooms[2].id,
      status: "Assigned",
      assignee: tech.id,
      severity: "High",
      category: "Air conditioning",
      priority: "High",
      due: deadline,
      detectedAt: now(),
      description: "Room temperature remains high after 20 minutes.",
    }),
  ];
  db.assets = [
    record("AC-101", "Daikin air conditioner", {
      room: db.rooms[0].id,
      department: "Maintenance",
      condition: "In Use",
      category: "HVAC",
      model: "Daikin inverter",
      serial: "DV-AC-101",
    }),
  ];
  db.warehouses = ["Bui Vien", "Cao Thang"].map((branch) =>
    record("MAIN", "Main warehouse", { branch, department: "Warehouse" }),
  );
  db.items = [
    record("TOWEL", "Bath towel", {
      unit: "Piece",
      category: "Linen",
      minimum: 30,
    }),
    record("SHAMPOO", "Guest shampoo", {
      unit: "Bottle",
      category: "Amenities",
      minimum: 50,
    }),
    record("CLEANER", "Bathroom cleaner", {
      unit: "Litre",
      category: "Cleaning",
      minimum: 10,
    }),
  ];
  for (const item of db.items)
    item.minimums = Object.fromEntries(
      db.warehouses.map((w) => [w.id, item.minimum]),
    );
  db.units = ["Piece", "Bottle", "Litre", "Kilogram", "Box"].map((n) =>
    record(n.toUpperCase(), n, { factor: 1 }),
  );
  for (const wh of db.warehouses)
    for (const [i, item] of db.items.entries())
      db.ledger.push(
        record("OPEN", "Opening receipt", {
          warehouse: wh.id,
          item: item.id,
          branch: wh.branch,
          delta: [70, 20, 6][i],
          document: "opening",
        }),
      );
  db.materials = [
    record("MR-001", "Replenish room amenities", {
      department: "Housekeeping",
      warehouse: db.warehouses[0].id,
      creator: hk.id,
      approver: db.users.find((u) => u.username === "headhousekeeping")!.id,
      status: "Pending Approval",
      lines: [
        {
          item: db.items[1].id,
          quantity: 15,
          approved: 0,
          issued: 0,
          returned: 0,
        },
      ],
      due: deadline,
    }),
  ];
  db.tasks = [
    record("TSK-001", "Prepare rooftop event area", {
      department: "Warehouse",
      status: "Assigned",
      assignee: db.users.find((u) => u.username === "storekeeper")!.id,
      creator: db.users.find((u) => u.username === "head")!.id,
      criteria:
        "Equipment counted\nFurniture arranged\nSafety checks completed",
      priority: "Normal",
      due: deadline,
      description: "Prepare the venue for the evening service.",
    }),
  ];
  const failedCleaning = db.cleaning[2];
  failedCleaning.status = "Rework Required";
  failedCleaning.performedBy = hk.id;
  const failedInspection = record("INS-002", "Bathroom quality follow-up", {
    department: "Housekeeping",
    room: failedCleaning.room,
    cleaning: failedCleaning.id,
    assignee: sup.id,
    checklist: db.checklists[0].id,
    checklistVersion: 1,
    criteria: db.checklists[0].criteria,
    status: "Failed",
    results: db.checklists[0].criteria
      .split("\n")
      .map((text: string, i: number) => ({
        text,
        result: i === 1 ? "Fail" : "Pass",
        defect: i === 1 ? "Water marks on bathroom mirror" : "",
      })),
  });
  db.inspections.push(failedInspection);
  db.rework = [
    record("RW-001", "Fix bathroom mirror", {
      department: "Housekeeping",
      room: failedCleaning.room,
      cleaning: failedCleaning.id,
      inspection: failedInspection.id,
      status: "Pending Acceptance",
      assignee: hk.id,
      defects: "Water marks on bathroom mirror",
      due: deadline,
      description: "Remediate the failed quality criterion.",
    }),
  ];
  db.reference = ["Low", "Normal", "High", "Critical"].map((priority, i) =>
    record(priority.toUpperCase(), priority, {
      group: "Priority",
      responseMinutes: [120, 60, 30, 10][i],
      resolutionMinutes: [1440, 480, 240, 60][i],
    }),
  );
  db.schedules = [
    record("PM-001", "Monthly HVAC inspection", {
      department: "Maintenance",
      room: db.rooms[0].id,
      asset: db.assets[0].id,
      interval: 1,
      intervalUnit: "Months",
      due: new Date(Date.now() + 30 * 86400000).toISOString(),
      nextDue: new Date(Date.now() + 30 * 86400000).toISOString(),
      assignee: tech.id,
      criteria:
        "Filter clean\nDrain unobstructed\nCooling performance measured",
      description: "Preventive maintenance standard for room air conditioning.",
    }),
  ];
  db.transactions = [
    record("REC-001", "Amenities supplier receipt", {
      department: "Warehouse",
      type: "Receipt",
      warehouse: db.warehouses[0].id,
      source: "Hospitality amenities supplier",
      status: "Draft",
      lines: [
        { item: db.items[1].id, quantity: 30, unit: "Bottle", factor: 1 },
      ],
    }),
  ];
  db.stocktakes = [
    record("ST-001", "Main warehouse cycle count", {
      department: "Warehouse",
      warehouse: db.warehouses[0].id,
      status: "In Progress",
      cutoff: now(),
      creator: db.users.find((u) => u.username === "storekeeper")!.id,
      lines: db.items.map((item) => ({
        item: item.id,
        system: balance(db, db.warehouses[0].id, item.id),
        quantity: balance(db, db.warehouses[0].id, item.id),
      })),
    }),
  ];
  db.replenishments = [
    record("REP-001", "Guest shampoo low-stock replenishment", {
      department: "Warehouse",
      warehouse: db.warehouses[0].id,
      status: "Requested",
      supplier: "Hospitality amenities supplier",
      due: deadline,
      lines: [
        {
          item: db.items[1].id,
          quantity: 30,
          received: 0,
          unit: "Bottle",
          factor: 1,
        },
      ],
    }),
  ];
  db.shifts = [
    record("HK-DAY", "Housekeeping day shift", {
      department: "Housekeeping",
      date: new Date().toLocaleDateString("en-CA", {
        timeZone: "Asia/Ho_Chi_Minh",
      }),
      start: "08:00",
      end: "16:00",
      assignees: [hk.id, sup.id],
      status: "Scheduled",
    }),
  ];
  db.handovers = [
    record("HO-001", "Housekeeping afternoon handover", {
      department: "Housekeeping",
      sender: hk.id,
      receiver: sup.id,
      status: "Pending Acknowledgment",
      description:
        "Review the mirror rework and inspect completed turnover rooms.",
      snapshot: [
        {
          entity: "rework",
          id: db.rework[0].id,
          name: db.rework[0].name,
          status: db.rework[0].status,
        },
      ],
    }),
  ];
  db.requests = [
    record("IR-001", "Move rooftop event furniture", {
      department: "Housekeeping",
      receivingDepartment: "Warehouse",
      requestType: "Operational support",
      creator: hk.id,
      status: "Pending Acceptance",
      priority: "Normal",
      due: deadline,
      description:
        "Move and arrange spare furniture for the evening rooftop event.",
    }),
  ];
  db.audit = [
    record("AUD-001", "Initial room directory registration", {
      actor: "Administrator",
      actorId: db.users[0].id,
      entity: "rooms",
      recordId: db.rooms[0].id,
      action: "Create",
      before: null,
      after: {
        id: db.rooms[0].id,
        name: db.rooms[0].name,
        branch: db.rooms[0].branch,
        status: db.rooms[0].status,
      },
      status: "Recorded",
    }),
  ];
  db.notifications = db.users.map((u) =>
    record("WELCOME", "Welcome to HotelOps", {
      recipient: u.id,
      status: "Unread",
      priority: "Normal",
      type: "System",
      description: "Your assigned work and operational updates appear here.",
    }),
  );
  calculateRooms(db);
  return db;
}
