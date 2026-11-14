import type { Database } from "../services";

/** Synthetic, deterministic public examples. No customer or employee data. */
export function populatePrototype(db: Database): Database {
  // Repair old demo seeds without resetting locally edited records.
  for (const room of db.rooms) {
    const area = db.locations.find((location) => location.id === room.area);
    if (!area || (area.kind || area.type) !== "Area")
      room.area = db.locations.find(
        (location) =>
          location.branch === room.branch &&
          (location.kind || location.type) === "Area",
      )?.id;
  }
  if (db.mockMetadata.some((r) => r.id === "public-prototype-v3")) return db;
  const targets: Record<string, number> = {
    users: 40,
    rooms: 64,
    cleaning: 40,
    inspections: 30,
    incidents: 32,
    occurrences: 24,
    assets: 50,
    items: 48,
    materials: 24,
    tasks: 40,
    audit: 65,
    checklists: 5,
    reference: 12,
  };
  for (const [key, target] of Object.entries(targets)) {
    const originals = [...db[key]];
    if (!originals.length) continue;
    while (db[key].length < target) {
      const index = db[key].length;
      const row = structuredClone(originals[index % originals.length]);
      row.id = `DEMO-${key.toUpperCase()}-${index + 1}`;
      row.code = `${key.toUpperCase().slice(0, 3)}-${String(index + 1).padStart(3, "0")}`;
      row.name =
        key === "rooms" ? `Room ${index + 201}` : `${row.name} · ${index + 1}`;
      if (key === "rooms") {
        row.number = String(index + 501);
        row.name = `Room ${row.number}`;
        row.passed = index % 3 !== 0;
        row.status = row.passed ? "Ready" : "Pending Cleaning";
      }
      if (key === "users") {
        row.username = `demo.employee.${index + 1}`;
        row.email = `employee${index + 1}@hotelops.example`;
        row.name = `Demo colleague ${index + 1}`;
      }
      if (key === "checklists") {
        row.name = [
          "Guestroom standard",
          "Bathroom hygiene",
          "Public area opening",
          "Rooftop closing",
          "Deep cleaning",
        ][index % 5];
        row.status = index % 2 ? "Published" : "Draft";
      }
      db[key].push(row);
    }
  }
  for (const room of db.rooms) {
    room.area ||=
      db.locations.find(
        (r) => r.branch === room.branch && (r.kind || r.type) === "Area",
      )?.id || db.locations.find((r) => r.branch === room.branch)?.id;
  }
  const extraNames = [
    "Nguyen Gia Phuc",
    "Tran Hoai An",
    "Le Bao Chau",
    "Pham Minh Quan",
    "Vo Thanh Truc",
  ];
  db.users
    .filter((user) => user.id.startsWith("DEMO-USERS-"))
    .forEach((user, index) => {
      user.name = extraNames[index % extraNames.length];
    });
  db.cleaning.slice(-5).forEach((task, index) => {
    const room = db.rooms[db.rooms.length - 5 + index];
    task.room = room.id;
    task.branch = room.branch;
    task.name = `Turnover cleaning · ${room.name}`;
    task.status = "In Progress";
    task.assignee =
      db.users.find(
        (user) =>
          user.branch === room.branch &&
          user.roles?.includes("Housekeeping Staff"),
      )?.id || task.assignee;
    task.actualStart = new Date(Date.now() - 3600000).toISOString();
  });
  db.rooms.slice(-3).forEach((room, index) => {
    const incident = db.incidents[db.incidents.length - 3 + index];
    incident.room = room.id;
    incident.branch = room.branch;
    incident.severity = "Critical";
    incident.name = `Room safety check · ${room.name}`;
    incident.status = "In Progress";
    if (!db.blocks.some((block) => block.id === `DEMO-BLOCK-${room.id}`))
      db.blocks.push({
        id: `DEMO-BLOCK-${room.id}`,
        name: `Safety inspection · ${room.name}`,
        room: room.id,
        incident: incident.id,
        reason:
          "Room unavailable while maintenance verifies electrical safety.",
        status: "Effective",
        branch: room.branch,
        department: "Maintenance",
        version: 1,
        createdAt: new Date().toISOString(),
      });
  });
  for (const key of ["branches", "departments", "warehouses"])
    for (const row of db[key]) {
      row.description ||=
        key === "departments"
          ? `${row.name} · daily service delivery, coordination and quality checks`
          : key === "warehouses"
            ? "Guest amenities, housekeeping consumables and maintenance supplies"
            : "Hotel operations and guest services";
      if (key === "branches") row.phone ||= "+84 28 0000 0000";
    }
  for (const key of ["cleaning", "rework", "inspections", "tasks"])
    for (const row of db[key]) {
      row.priority ||= "Normal";
      row.due ||= new Date(Date.now() + 86400000).toISOString();
      if (key === "cleaning") row.checklistVersion ||= 1;
      if (key === "rework")
        row.room ||=
          db.cleaning.find((r) => r.id === row.cleaning)?.room ||
          db.rooms[0].id;
    }
  for (const item of db.items)
    for (const warehouse of db.warehouses) {
      if (
        db.ledger.some(
          (r) => r.item === item.id && r.warehouse === warehouse.id,
        )
      )
        continue;
      const index = db.items.indexOf(item);
      db.ledger.push({
        id: `DEMO-OPENING-${warehouse.id}-${item.id}`,
        name: `Opening count · ${item.name}`,
        item: item.id,
        warehouse: warehouse.id,
        delta: index % 5 === 0 ? 5 : 80 + index,
        document: "opening",
        branch: warehouse.branch,
        department: "Warehouse",
        status: "Posted",
        version: 1,
        createdAt: new Date().toISOString(),
      });
    }
  // Connect the first asset's history so its direct review link contains records.
  db.incidents[0].asset ||= db.assets[0].id;
  for (const key of [
    "transactions",
    "stocktakes",
    "materials",
    "replenishments",
  ])
    for (const row of db[key])
      for (const line of row.lines || []) {
        line.approved ??= 0;
        line.issued ??= 0;
        line.received ??= 0;
      }
  const rework =
    db.rework.find((r) => r.status === "Pending Reinspection") || db.rework[0];
  if (rework && !db.inspections.some((r) => r.rework)) {
    rework.status = "Pending Reinspection";
    const example = structuredClone(db.inspections[0]);
    db.inspections.push({
      ...example,
      id: "DEMO-REINSPECTION-001",
      name: "Room recovery · final reinspection",
      rework: rework.id,
      room: rework.room,
      status: "Pending",
      results: example.results || [],
    });
  }
  if (!db.corrections.length)
    for (const interval of db.intervals.slice(0, 4)) {
      db.corrections.push({
        ...interval,
        id: `DEMO-CORRECTION-${interval.id}`,
        interval: interval.id,
        name: "Time review · shift handover",
        reason: "Shift handover time reviewed against the daily schedule.",
        status: "Pending",
      });
    }
  // Include readable activity on every detail page, including newly created examples.
  for (const key of [
    "users",
    "roles",
    "branches",
    "locations",
    "roomTypes",
    "rooms",
    "cleaning",
    "rework",
    "checklists",
    "inspections",
    "incidents",
    "schedules",
    "assets",
    "warehouses",
    "items",
    "transactions",
    "stocktakes",
    "materials",
    "replenishments",
    "tasks",
    "shifts",
    "handovers",
    "requests",
  ]) {
    for (const row of db[key] || []) {
      if (db.audit.some((a) => a.entity === key && a.recordId === row.id))
        continue;
      db.audit.push({
        id: `DEMO-AUDIT-${row.id}`,
        name: `Reviewed · ${row.name}`,
        code: "AUD",
        entity: key,
        recordId: row.id,
        action: "Reviewed",
        actor: "Demo operations team",
        actorId: db.users[0].id,
        branch: row.branch || "Bui Vien",
        department: row.department || "Operations",
        status: "Recorded",
        version: 1,
        createdAt: row.createdAt || new Date().toISOString(),
        before: { status: "Draft" },
        after: { name: row.name, status: row.status, branch: row.branch },
      });
    }
  }
  db.mockMetadata.push({
    id: "public-prototype-v3",
    name: "Public synthetic examples",
    status: "Applied",
    branch: "Bui Vien",
    department: "Operations",
    version: 1,
    createdAt: new Date().toISOString(),
  });
  return db;
}
