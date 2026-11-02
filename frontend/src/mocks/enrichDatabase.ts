import type { RecordData } from "../domain";
import type { Database } from "../services";

export const mockVersion = "rich-data-v6";
const names = [
  "Nguyen Minh Anh",
  "Tran Quoc Bao",
  "Le Thanh Ha",
  "Pham Gia Huy",
  "Vo Ngoc Lan",
  "Dang Thanh Tung",
  "Bui Thu Trang",
  "Hoang Minh Duc",
  "Do Kim Ngan",
  "Phan Tuan Kiet",
];
const supplies = [
  "Bath towel",
  "Guest shampoo",
  "Bathroom cleaner",
  "Toilet paper",
  "Queen bed sheet",
  "Pillow case",
  "Hand towel",
  "Bath mat",
  "Shower gel 30ml",
  "Dental kit",
  "Shaving kit",
  "Vanity kit",
  "Slippers",
  "Laundry bag",
  "Coffee sachet",
  "Tea sachet",
  "Sugar sachet",
  "Mineral water",
  "Tissue box",
  "Disinfectant",
  "Glass cleaner",
  "Floor detergent",
  "Latex gloves",
  "Refuse bag",
  "LED bulb",
  "AA battery",
  "Door lock battery",
  "AC filter",
  "Plumbing seal",
  "Electrical tape",
  "Microfiber cloth",
  "Mop head",
];

/** Additive, idempotent migration: never replace an existing user edit or reference. */
export function enrichDatabase(db: Database): Database {
  if (db.mockMetadata?.some((r) => r.id === mockVersion)) return db;
  const anchor = new Date();
  anchor.setMinutes(0, 0, 0);
  const at = (hours: number) =>
    new Date(anchor.getTime() + hours * 3600000).toISOString();
  const add = (
    key: string,
    id: string,
    name: string,
    extra: Partial<RecordData> = {},
  ) => {
    db[key] ||= [];
    const existing = db[key].find((r) => r.id === id);
    if (existing) return existing;
    const r: RecordData = {
      id,
      code: id,
      name,
      branch: "Bui Vien",
      department: "Operations",
      status: "Active",
      version: 1,
      createdAt: at(-24),
      ...extra,
    };
    db[key].push(r);
    return r;
  };
  const baseUser = (username: string) =>
    db.users.find((u) => u.username === username)!;
  for (let i = 0; i < 10; i++) {
    const branch = i < 6 ? "Bui Vien" : "Cao Thang";
    const role = i % 3 === 0 ? "Maintenance Staff" : "Housekeeping Staff";
    add("users", `USR-DEMO-${i + 1}`, names[i], {
      branch,
      roles: [role],
      department: role.includes("Maintenance") ? "Maintenance" : "Housekeeping",
      username: `staff.${i + 1}`,
      email: `staff.${i + 1}@ducvuong.example`,
      phone: `09081230${String(i).padStart(2, "0")}`,
      password: "HotelOps@2026",
      temporary: i === 9,
      failed: 0,
      lockedUntil: 0,
      status: i === 8 ? "Inactive" : "Active",
    });
  }
  // Give existing demo accounts a real identity without changing their login or role.
  for (const [i, u] of db.users.entries()) {
    if (!u.displayName) u.displayName = names[i % names.length];
    if (
      u.roles?.some(
        (role: string) => u.name === role || u.name === role + " · Cao Thang",
      )
    )
      u.name = u.displayName;
  }
  for (const branch of ["Bui Vien", "Cao Thang"]) {
    const prefix = branch === "Bui Vien" ? "BV" : "CT";
    for (let floor = 4; floor <= 5; floor++)
      add("locations", `LOC-${prefix}-F${floor}`, `Floor ${floor}`, {
        branch,
        kind: "Floor",
      });
    for (const [i, area] of [
      "East wing",
      "West wing",
      "Lobby",
      "Rooftop",
    ].entries())
      add("locations", `LOC-${prefix}-A${i + 1}`, area, {
        branch,
        kind: "Area",
      });
    for (const [i, type] of [
      "Superior Double",
      "Deluxe Twin",
      "Family Suite",
      "Premium Balcony",
    ].entries())
      add("roomTypes", `RT-${prefix}-${i + 1}`, type, {
        branch,
        minutes: 30 + i * 5,
        checklist: db.checklists[0].id,
        description: `${type} · Duc Vuong room quality standard`,
      });
    for (const floor of db.locations.filter(
      (l) => l.branch === branch && l.kind === "Floor",
    )) {
      const n = Number(floor.name.match(/\d+/)?.[0]);
      for (let room = 1; room <= 4; room++) {
        if (
          db.rooms.some((r) => r.branch === branch && r.name === `${n}0${room}`)
        )
          continue;
        add("rooms", `RM-${prefix}-${n}0${room}`, `${n}0${room}`, {
          branch,
          floor: floor.id,
          area: `LOC-${prefix}-A${(room % 2) + 1}`,
          roomType: db.roomTypes.filter((t) => t.branch === branch)[room % 5]
            .id,
          active: true,
          passed: room === 1,
          status: room === 1 ? "Ready" : "Pending Cleaning",
          description: "Guest room · daily quality and equipment checks",
        });
      }
    }
    for (const [i, label] of ["Linen store", "Engineering supplies"].entries())
      add("warehouses", `WH-${prefix}-${i + 1}`, label, {
        branch,
        department: "Warehouse",
        description: i
          ? "Electrical, plumbing and HVAC consumables"
          : "Clean linen and guest amenities",
      });
    const hk = baseUser(
      branch === "Bui Vien" ? "housekeeper" : "housekeeperct",
    );
    const tech = baseUser(
      branch === "Bui Vien" ? "technician" : "technicianct",
    );
    const sup = baseUser(
      branch === "Bui Vien" ? "hksupervisor" : "hksupervisorct",
    );
    const store = baseUser(
      branch === "Bui Vien" ? "storekeeper" : "storekeeperct",
    );
    const rooms = db.rooms
      .filter((r) => r.branch === branch)
      .sort(
        (a, b) =>
          Number(db.cleaning.some((c) => c.room === a.id)) -
          Number(db.cleaning.some((c) => c.room === b.id)),
      );
    for (let i = 0; i < 20; i++) {
      const room = rooms[i % rooms.length];
      const asset = add(
        "assets",
        `AST-${prefix}-${i + 1}`,
        `${["Daikin inverter AC", "Samsung guest TV", "Yale door lock", "Minibar refrigerator"][i % 4]} · ${room.name}`,
        {
          branch,
          room: room.id,
          department: "Maintenance",
          category: ["HVAC", "Electronics", "Security", "Appliances"][i % 4],
          condition: i % 7 === 0 ? "Under Repair" : "In Use",
          model: [
            "Daikin FTKC",
            "Samsung Hospitality",
            "Yale RFID",
            "Minibar 40L",
          ][i % 4],
          serial: `DV-${prefix}-${String(i + 1).padStart(4, "0")}`,
          purchaseDate: "2025-06-10",
          description: "Registered guest room equipment",
        },
      );
      if (i < 10) {
        const cl = add(
          "cleaning",
          `CLN-${prefix}-${i + 1}`,
          `Checkout cleaning · Room ${room.name}`,
          {
            branch,
            room: room.id,
            department: "Housekeeping",
            assignee: hk.id,
            performedBy: i % 4 === 3 ? hk.id : undefined,
            status: [
              "Assigned",
              "In Progress",
              "Pending Inspection",
              "Rework Required",
            ][i % 4],
            due: at(i % 3 === 0 ? -2 : 3 + i),
            priority: i % 3 === 0 ? "High" : "Normal",
            cleaningType: "Turnover",
            criteria: db.checklists[0].criteria,
            completedItems:
              i % 4 === 2 ? db.checklists[0].criteria.split("\n") : [],
            description:
              "Replace linen, sanitise bathroom and replenish guest amenities.",
          },
        );
        const failed = i % 2 === 1;
        if (failed) {
          cl.status = "Rework Required";
          cl.performedBy = hk.id;
        }
        const ins = add(
          "inspections",
          `INS-${prefix}-${i + 1}`,
          `Room ${room.name} · quality inspection`,
          {
            branch,
            room: room.id,
            cleaning: cl.id,
            department: "Housekeeping",
            assignee: sup.id,
            checklist: db.checklists[0].id,
            checklistVersion: 1,
            criteria: db.checklists[0].criteria,
            status: failed ? "Failed" : "Assigned",
            due: at(i + 2),
            results: failed
              ? db.checklists[0].criteria
                  .split("\n")
                  .map((text: string, n: number) => ({
                    text,
                    result: n === 1 ? "Fail" : "Pass",
                    defect: n === 1 ? "Bathroom mirror requires polishing" : "",
                  }))
              : undefined,
          },
        );
        if (failed)
          add(
            "rework",
            `RW-${prefix}-${i + 1}`,
            `Bathroom finishing · Room ${room.name}`,
            {
              branch,
              room: room.id,
              cleaning: cl.id,
              inspection: ins.id,
              department: "Housekeeping",
              assignee: hk.id,
              status:
                i % 3 === 0 ? "Pending Reinspection" : "Pending Acceptance",
              defects: "Bathroom mirror requires polishing",
              remediation:
                i % 3 === 0 ? "Mirror polished and bathroom surface dried" : "",
              due: at(i + 1),
              description:
                "Correct the failed bathroom criterion before releasing the room.",
            },
          );
        const inc = add(
          "incidents",
          `INC-${prefix}-${i + 1}`,
          `${["Air conditioner not cooling", "Door lock battery warning", "Bathroom tap dripping", "Guest TV signal unstable", "Minibar temperature high"][i % 5]} · Room ${room.name}`,
          {
            branch,
            department: "Maintenance",
            room: room.id,
            asset: asset.id,
            assignee: tech.id,
            status: ["Assigned", "In Progress", "Resolved", "Closed"][i % 4],
            severity: i % 5 === 0 ? "Critical" : i % 2 ? "High" : "Normal",
            priority: i % 2 ? "High" : "Normal",
            due: at(i % 3 === 0 ? -4 : i + 3),
            detectedAt: at(-5),
            description:
              "Detected during daily room checks. Inspect equipment and record the repair outcome.",
            creator: hk.id,
          },
        );
        if (i < 8)
          add(
            "occurrences",
            `MNT-${prefix}-${i + 1}`,
            `Preventive equipment check · Room ${room.name}`,
            {
              branch,
              department: "Maintenance",
              room: room.id,
              asset: asset.id,
              schedule: `PM-${prefix}-${(i % 5) + 1}`,
              scheduledDue: at(-24 - i),
              assignee: tech.id,
              status: ["Assigned", "In Progress", "Completed"][i % 3],
              due: at(i % 3 === 0 ? -3 : i + 4),
              priority: "High",
              criteria:
                "Equipment isolated\nRepair completed\nFunctional test recorded",
              description:
                "Diagnose the linked incident and restore equipment safely.",
            },
          );
        if (i < 5)
          add(
            "schedules",
            `PM-${prefix}-${i + 1}`,
            `${["HVAC filter service", "Door lock safety check", "TV connection check", "Minibar thermostat inspection", "Electrical safety inspection"][i]} · ${room.name}`,
            {
              branch,
              room: room.id,
              asset: asset.id,
              department: "Maintenance",
              assignee: tech.id,
              interval: 1,
              intervalUnit: "Months",
              due: at(720 + i * 24),
              nextDue: at(720 + i * 24),
              criteria:
                "Inspect condition\nService equipment\nRecord performance",
              description: "Monthly preventive equipment care",
            },
          );
      }
    }
    const wh = db.warehouses.find((w) => w.branch === branch)!;
    for (let i = 0; i < 15; i++) {
      add(
        "tasks",
        `TSK-${prefix}-${i + 1}`,
        `${["Prepare rooftop evening service", "Check lobby safety signage", "Arrange guest arrival amenities", "Inspect linen delivery", "Reconcile housekeeping supplies"][i % 5]} · ${i + 1}`,
        {
          branch,
          department: i % 2 ? "Housekeeping" : "Warehouse",
          assignee: i % 2 ? hk.id : store.id,
          creator: baseUser(branch === "Bui Vien" ? "head" : "headct").id,
          status: [
            "Assigned",
            "Accepted",
            "In Progress",
            "Paused",
            "Pending Verification",
            "Closed",
          ][i % 6],
          due: at(i % 4 === 0 ? -3 : i + 2),
          priority: i % 3 === 0 ? "High" : "Normal",
          criteria: "Preparation complete\nQuality checked\nHandover recorded",
          description:
            "Coordinate daily hotel operations and record completion evidence.",
          completedItems:
            i % 6 === 5
              ? ["Preparation complete", "Quality checked", "Handover recorded"]
              : [],
        },
      );
      if (i < 8)
        add(
          "materials",
          `MR-${prefix}-${i + 1}`,
          `${i % 2 ? "Guest amenities restock" : "Repair consumables"} · ${i + 1}`,
          {
            branch,
            department: i % 2 ? "Housekeeping" : "Maintenance",
            warehouse: wh.id,
            creator: i % 2 ? hk.id : tech.id,
            approver: baseUser(
              i % 2
                ? `headhousekeeping${prefix === "CT" ? "ct" : ""}`
                : `headmaintenance${prefix === "CT" ? "ct" : ""}`,
            ).id,
            status: [
              "Pending Approval",
              "Approved",
              "Partially Issued",
              "Fully Issued",
            ][i % 4],
            due: at(i + 3),
            linkedWork:
              i % 2
                ? `cleaning:CLN-${prefix}-${i + 1}`
                : `incidents:INC-${prefix}-${i + 1}`,
            lines: [
              {
                item: db.items[i % db.items.length].id,
                quantity: 10,
                approved: i % 4 ? 10 : 0,
                issued: i % 4 === 2 ? 4 : i % 4 === 3 ? 10 : 0,
                returned: 0,
              },
            ],
          },
        );
      if (i < 8)
        add(
          "requests",
          `IR-${prefix}-${i + 1}`,
          `${["Move event furniture", "Repair staff pantry lighting", "Deliver spare linen", "Prepare early arrival room"][i % 4]} · ${i + 1}`,
          {
            branch,
            department: "Housekeeping",
            receivingDepartment: i % 2 ? "Warehouse" : "Maintenance",
            requestType: "Operational support",
            creator: hk.id,
            assignee: i % 2 ? store.id : tech.id,
            status: ["Pending Acceptance", "Accepted", "In Progress", "Closed"][
              i % 4
            ],
            priority: "Normal",
            due: at(i % 3 === 0 ? -2 : i + 3),
            description:
              "Please coordinate with the requesting department and acknowledge completion.",
          },
        );
      if (i < 8)
        add(
          "shifts",
          `SH-${prefix}-${i + 1}`,
          `${i % 2 ? "Evening" : "Day"} · ${i % 3 ? "Housekeeping" : "Maintenance"} · ${i + 1}`,
          {
            branch,
            department: i % 3 ? "Housekeeping" : "Maintenance",
            date: at((Math.floor(i / 2) - 2) * 24).slice(0, 10),
            start: i % 2 ? "16:00" : "08:00",
            end: i % 2 ? "23:00" : "16:00",
            assignees: i % 3 ? [hk.id, sup.id] : [tech.id],
            status: i < 2 ? "Completed" : "Scheduled",
          },
        );
      if (i < 5)
        add(
          "handovers",
          `HO-${prefix}-${i + 1}`,
          `Room operations shift handover · ${i + 1}`,
          {
            branch,
            department: "Housekeeping",
            sender: hk.id,
            receiver: sup.id,
            status: i % 2 ? "Acknowledged" : "Pending Acknowledgment",
            description:
              "Review unfinished cleaning, bathroom rework and maintenance follow-up before the next shift.",
            snapshot: [
              {
                entity: "cleaning",
                id: `CLN-${prefix}-${i + 1}`,
                name: `Room ${rooms[i].name} cleaning`,
                status: db.cleaning.find(
                  (r) => r.id === `CLN-${prefix}-${i + 1}`,
                )!.status,
              },
            ],
          },
        );
      if (i < 5)
        add(
          "replenishments",
          `REP-${prefix}-${i + 1}`,
          `Amenities reorder · ${i + 1}`,
          {
            branch,
            department: "Warehouse",
            warehouse: wh.id,
            supplier: "Saigon Hospitality Supplies",
            status: i % 2 ? "Partially Received" : "Requested",
            due: at(i + 24),
            lines: [
              {
                item: db.items[i % db.items.length].id,
                quantity: 50,
                received: i % 2 ? 20 : 0,
                unit: db.items[i % db.items.length].unit,
                factor: 1,
              },
            ],
          },
        );
      add(
        "notifications",
        `NOT-${prefix}-${i + 1}`,
        `${i % 2 ? "Cleaning deadline approaching" : "Maintenance follow-up required"} · ${rooms[i % rooms.length].name}`,
        {
          branch,
          recipient:
            i % 3 === 0 ? baseUser("admin").id : i % 2 ? hk.id : tech.id,
          status: i % 3 ? "Unread" : "Read",
          type: i % 2 ? "Task" : "Incident",
          priority: i % 4 === 0 ? "High" : "Normal",
          relatedKey: i % 2 ? "tasks" : "incidents",
          relatedId:
            i % 2 ? `TSK-${prefix}-${i + 1}` : `INC-${prefix}-${(i % 10) + 1}`,
          description:
            "Open the related work to review its deadline and current status.",
        },
      );
      add(
        "audit",
        `AUD-${prefix}-${i + 1}`,
        `${i % 2 ? "Task assignment" : "Incident triage"} · ${i + 1}`,
        {
          branch,
          actor: baseUser("manager").name,
          actorId: baseUser("manager").id,
          entity: i % 2 ? "tasks" : "incidents",
          recordId:
            i % 2 ? `TSK-${prefix}-${i + 1}` : `INC-${prefix}-${(i % 10) + 1}`,
          action: i % 2 ? "Assign" : "Classify",
          before: { status: "Pending Assignment" },
          after: { status: "Assigned", assignee: i % 2 ? hk.id : tech.id },
          status: "Recorded",
          createdAt: at(-i - 1),
        },
      );
    }
  }
  for (let i = db.items.length; i < supplies.length; i++)
    add("items", `ITM-${String(i + 1).padStart(3, "0")}`, supplies[i], {
      unit: i < 19 ? "Piece" : i < 22 ? "Litre" : "Piece",
      category:
        i < 8
          ? "Linen"
          : i < 19
            ? "Amenities"
            : i < 24
              ? "Cleaning"
              : "Engineering",
      minimum: 10 + (i % 5) * 5,
      minimums: Object.fromEntries(
        db.warehouses.map((w) => [w.id, 10 + (i % 5) * 5]),
      ),
    });
  for (const wh of db.warehouses)
    for (const [i, item] of db.items.entries()) {
      if (!db.ledger.some((l) => l.warehouse === wh.id && l.item === item.id))
        add("ledger", `LED-${wh.id}-${item.id}`, "Opening physical count", {
          branch: wh.branch,
          warehouse: wh.id,
          item: item.id,
          delta: i % 7 === 0 ? 0 : i % 4 === 0 ? 5 : 80 + i,
          document: "opening",
          department: "Warehouse",
        });
    }
  for (const branch of ["Bui Vien", "Cao Thang"]) {
    const prefix = branch === "Bui Vien" ? "BV" : "CT",
      wh = db.warehouses.find((w) => w.branch === branch)!;
    for (let i = 0; i < 16; i++) {
      const item = db.items[i];
      const posted = i % 3 !== 0;
      const id = `TX-${prefix}-${i + 1}`;
      add("transactions", id, `Supplier delivery · ${item.name}`, {
        branch,
        department: "Warehouse",
        warehouse: wh.id,
        type: "Receipt",
        source: "Saigon Hospitality Supplies",
        status: posted ? "Posted" : "Draft",
        postedAt: posted ? at(-i) : undefined,
        lines: [{ item: item.id, quantity: 20, unit: item.unit, factor: 1 }],
      });
      if (posted)
        add("ledger", `LED-${id}`, `Receipt · ${item.name}`, {
          branch,
          department: "Warehouse",
          warehouse: wh.id,
          item: item.id,
          delta: 20,
          document: id,
          createdAt: at(-i),
        });
      if (i < 5)
        add(
          "stocktakes",
          `ST-${prefix}-${i + 1}`,
          `Cycle count · ${["Linen", "Amenities", "Engineering", "Cleaning", "General supplies"][i]}`,
          {
            branch,
            department: "Warehouse",
            warehouse: wh.id,
            creator: baseUser(prefix === "BV" ? "storekeeper" : "storekeeperct")
              .id,
            status: "In Progress",
            cutoff: at(-1),
            lines: db.items.slice(i * 4, i * 4 + 4).map((item) => {
              const qty = db.ledger
                .filter((l) => l.warehouse === wh.id && l.item === item.id)
                .reduce((n, l) => n + Number(l.delta), 0);
              return { item: item.id, system: qty, quantity: qty };
            }),
          },
        );
    }
  }
  // Posted issues and received replenishments have matching stock movements.
  for (const request of db.materials.filter((r) => /^MR-(BV|CT)-/.test(r.id))) {
    const issued = request.lines.filter(
      (line: { issued: number }) => Number(line.issued) > 0,
    );
    if (!issued.length) continue;
    const document = add(
      "transactions",
      `ISS-${request.id}`,
      `Material issue · ${request.name}`,
      {
        branch: request.branch,
        department: "Warehouse",
        warehouse: request.warehouse,
        type: "Issue",
        status: "Posted",
        materialRequest: request.id,
        receiver: request.creator,
        lines: issued.map((line: { item: string; issued: number }) => ({
          item: line.item,
          quantity: line.issued,
          unit: db.items.find((i) => i.id === line.item)?.unit,
          factor: 1,
        })),
      },
    );
    for (const line of document.lines)
      add("ledger", `LED-${document.id}-${line.item}`, document.name, {
        branch: request.branch,
        department: "Warehouse",
        warehouse: request.warehouse,
        item: line.item,
        delta: -Number(line.quantity),
        document: document.id,
      });
  }
  for (const request of db.replenishments.filter((r) =>
    /^REP-(BV|CT)-/.test(r.id),
  )) {
    const received = request.lines.filter(
      (line: { received: number }) => Number(line.received) > 0,
    );
    if (!received.length) continue;
    const document = add(
      "transactions",
      `RCV-${request.id}`,
      `Replenishment delivery · ${request.name}`,
      {
        branch: request.branch,
        department: "Warehouse",
        warehouse: request.warehouse,
        type: "Receipt",
        status: "Posted",
        replenishment: request.id,
        source: request.supplier,
        lines: received.map(
          (line: { item: string; received: number; unit: string }) => ({
            item: line.item,
            quantity: line.received,
            unit: line.unit,
            factor: 1,
          }),
        ),
      },
    );
    for (const line of document.lines)
      add("ledger", `LED-${document.id}-${line.item}`, document.name, {
        branch: request.branch,
        department: "Warehouse",
        warehouse: request.warehouse,
        item: line.item,
        delta: Number(line.quantity),
        document: document.id,
      });
  }
  for (const branch of ["Bui Vien", "Cao Thang"]) {
    const prefix = branch === "Bui Vien" ? "BV" : "CT",
      warehouses = db.warehouses.filter((w) => w.branch === branch),
      wh = warehouses[0],
      item = db.items[0];
    const issue = db.transactions.find(
      (t) =>
        t.type === "Issue" &&
        t.branch === branch &&
        t.lines.some(
          (l: { item: string; quantity: number }) =>
            l.item === item.id && l.quantity >= 2,
        ),
    );
    if (issue)
      add("transactions", `RET-${prefix}-001`, "Unused clean linen return", {
        branch,
        department: "Warehouse",
        warehouse: wh.id,
        type: "Return",
        issue: issue.id,
        status: "Draft",
        source: "Housekeeping linen trolley",
        lines: [{ item: item.id, quantity: 2, unit: item.unit, factor: 1 }],
      });
    add(
      "transactions",
      `TRF-${prefix}-001`,
      "Linen store replenishment transfer",
      {
        branch,
        department: "Warehouse",
        warehouse: wh.id,
        destination: warehouses[1].id,
        type: "Transfer",
        status: "Draft",
        source: "Main store",
        lines: [{ item: item.id, quantity: 5, unit: item.unit, factor: 1 }],
      },
    );
  }
  for (const stocktake of db.stocktakes.filter(
    (r) => /^ST-(BV|CT)-/.test(r.id) && r.version === 1,
  )) {
    stocktake.cutoff = at(0);
    stocktake.lines = stocktake.lines.map(
      (
        line: { item: string; system: number; quantity: number },
        index: number,
      ) => {
        const system = db.ledger
          .filter(
            (l) => l.warehouse === stocktake.warehouse && l.item === line.item,
          )
          .reduce((n, l) => n + Number(l.delta), 0);
        return {
          ...line,
          system,
          quantity:
            system + (stocktake.id.endsWith("-2") && index === 0 ? 1 : 0),
        };
      },
    );
  }
  // Historical workdays remain closed so the normal Start workday workflow is available.
  for (const user of db.users.filter((u) => u.status === "Active")) {
    for (let day = 1; day <= 3; day++) {
      const start = at(-day * 24 - 7),
        end = at(-day * 24 + 1);
      add(
        "workdays",
        `WD-${user.id}-${day}`,
        `${user.name} · recorded workday`,
        {
          branch: user.branch,
          department: user.department,
          assignee: user.id,
          start,
          end,
          status: "Ended",
          reason: "Scheduled hotel operations shift",
        },
      );
      const work =
        db.tasks.find((t) => t.assignee === user.id) ||
        db.cleaning.find((t) => t.assignee === user.id);
      if (work)
        add("intervals", `TIME-${user.id}-${day}`, work.name, {
          branch: user.branch,
          department: user.department,
          assignee: user.id,
          entity: db.tasks.includes(work) ? "tasks" : "cleaning",
          recordId: work.id,
          start: at(-day * 24 - 6),
          end: at(-day * 24 - 5),
          status: "Closed",
        });
    }
  }
  // Rooms also carry useful history in the default populated state.
  for (const room of db.rooms)
    if (!room.history)
      room.history = [
        { from: "Occupied", to: "Pending Cleaning", time: at(-5) },
        { from: "Pending Cleaning", to: room.status, time: at(-3) },
      ];
  add("mockMetadata", mockVersion, "Rich demo dataset migration", {
    status: "Applied",
  });
  return db;
}
