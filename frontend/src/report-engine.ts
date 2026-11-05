import type { RecordData } from "./domain";
import * as service from "./services";
export const reportTypes = [
  { key: "rooms", name: "RPT-01 · Room readiness and housekeeping" },
  { key: "inspections", name: "RPT-02 · Inspection and rework" },
  { key: "incidents", name: "RPT-03 · Incidents and maintenance" },
  { key: "assets", name: "RPT-04 · Assets" },
  { key: "items", name: "RPT-05 · Inventory and material usage" },
  { key: "tasks", name: "RPT-06 · Tasks and workload" },
  { key: "requests", name: "RPT-07 · Internal requests" },
];
export function generateReport(
  key: string,
  from: string,
  to: string,
  department = "",
  employee = "",
) {
  if (!service.can("UC-135"))
    throw Error("You do not have permission to generate reports.");
  if (!from || !to || from > to) throw Error("Select a valid report period.");
  const db = service.snapshot();
  const snapshot = new Date().toISOString();
  const start = new Date(from + "T00:00:00+07:00").toISOString();
  const end = new Date(to + "T23:59:59.999+07:00").toISOString();
  const scoped = (k: string) =>
    db[k].filter(
      (r) =>
        service.inScope(r) &&
        (service.branchContext() === "All branches" ||
          r.branch === service.branchContext()) &&
        (!department || r.department === department) &&
        (!employee || r.assignee === employee),
    );
  const inPeriod = (r: RecordData) =>
    r.createdAt >= start && r.createdAt <= end;
  const person = (id: string) =>
    db.users.find((u) => u.id === id)?.name || "N/A";
  const minutes = (a: string, b: string) =>
    a && b
      ? Math.round((new Date(b).getTime() - new Date(a).getTime()) / 60000)
      : "N/A";
  let rows: RecordData[] = [];
  let columns = ["name", "branch", "department", "status"];
  if (key === "rooms") {
    columns.push(
      "floor",
      "roomType",
      "cleaningCycles",
      "cleaningMinutes",
      "openBlockers",
    );
    rows = scoped("rooms").map((r) => {
      const tasks = db.cleaning.filter((t) => t.room === r.id && inPeriod(t));
      return {
        ...r,
        floor: db.locations.find((l) => l.id === r.floor)?.name || "N/A",
        roomType: db.roomTypes.find((t) => t.id === r.roomType)?.name || "N/A",
        cleaningCycles: tasks.length,
        cleaningMinutes: tasks
          .filter((t) => t.actualStart && t.actualEnd)
          .reduce((a, t) => a + Number(minutes(t.actualStart, t.actualEnd)), 0),
        openBlockers: db.blocks.filter(
          (b) => b.room === r.id && b.status === "Effective",
        ).length,
      };
    });
  } else if (key === "inspections") {
    columns.push(
      "inspector",
      "housekeeper",
      "checklistVersion",
      "failedCriteria",
      "defects",
      "reworkRounds",
      "createdAt",
    );
    rows = scoped("inspections")
      .filter(inPeriod)
      .map((r) => ({
        ...r,
        inspector: person(r.assignee),
        housekeeper: person(
          db.cleaning.find((c) => c.id === r.cleaning)?.performedBy,
        ),
        failedCriteria:
          r.results?.filter((v: any) => v.result === "Fail").length ?? "N/A",
        defects:
          r.results
            ?.filter((v: any) => v.defect)
            .map((v: any) => v.defect)
            .join("; ") || "—",
        reworkRounds: db.rework.filter((w) => w.cleaning === r.cleaning).length,
      }));
  } else if (key === "incidents") {
    columns.push(
      "category",
      "severity",
      "technician",
      "responseMinutes",
      "resolutionMinutes",
      "slaMet",
      "overdue",
      "createdAt",
    );
    rows = [...scoped("incidents"), ...scoped("occurrences")]
      .filter(inPeriod)
      .map((r) => ({
        ...r,
        technician: person(r.assignee),
        responseMinutes: minutes(r.detectedAt, r.acceptedAt),
        resolutionMinutes: minutes(r.detectedAt, r.actualEnd),
        slaMet:
          r.actualEnd && r.due
            ? new Date(r.actualEnd) <= new Date(r.due)
              ? "Yes"
              : "No"
            : "N/A",
        overdue:
          r.due &&
          !["Closed", "Cancelled", "Completed"].includes(r.status) &&
          new Date(r.due) < new Date(snapshot)
            ? "Yes"
            : "No",
      }));
  } else if (key === "assets") {
    columns.push(
      "category",
      "condition",
      "location",
      "model",
      "serial",
      "movementCount",
      "custodyHandover",
    );
    rows = scoped("assets").map((r) => ({
      ...r,
      location:
        db.rooms.find((room) => room.id === r.room)?.name ||
        db.locations.find((l) => l.id === r.area)?.name ||
        "Unassigned",
      movementCount: db.audit.filter(
        (a) =>
          a.entity === "assets" &&
          a.recordId === r.id &&
          ["Assign location", "Transfer location"].includes(a.action),
      ).length,
      custodyHandover: r.handover?.status || "—",
    }));
  } else if (key === "items") {
    columns = [
      "name",
      "branch",
      "warehouse",
      "unit",
      "minimum",
      "openingBalance",
      "received",
      "issued",
      "closingBalance",
      "transferLosses",
    ];
    rows = scoped("warehouses").flatMap((w) =>
      db.items.map((item) => {
        const movement = db.ledger.filter(
          (l) =>
            l.warehouse === w.id &&
            l.item === item.id &&
            l.createdAt >= start &&
            l.createdAt <= end,
        );
        return {
          ...item,
          id: w.id + ":" + item.id,
          branch: w.branch,
          warehouse: w.name,
          minimum: service.minimumStock(item, w.id) ?? "N/A",
          openingBalance: service.balance(
            db,
            w.id,
            item.id,
            new Date(new Date(start).getTime() - 1).toISOString(),
          ),
          received: movement
            .filter((l) => l.delta > 0)
            .reduce((a, l) => a + l.delta, 0),
          issued: movement
            .filter((l) => l.delta < 0)
            .reduce((a, l) => a - l.delta, 0),
          closingBalance: service.balance(db, w.id, item.id, end),
          transferLosses: db.transactions
            .filter(
              (t) =>
                t.type === "Transfer" && t.destination === w.id && inPeriod(t),
            )
            .flatMap((t) => t.lines)
            .filter((l: any) => l.item === item.id)
            .reduce(
              (a: number, l: any) =>
                a +
                (Number(l.damaged || 0) + Number(l.missing || 0)) *
                  (l.factor || 1),
              0,
            ),
        };
      }),
    );
  } else if (key === "tasks") {
    columns.push(
      "employee",
      "priority",
      "due",
      "effortMinutes",
      "openInterval",
      "workdayMinutes",
      "overdue",
    );
    rows = scoped("tasks")
      .filter(inPeriod)
      .map((r) => ({
        ...r,
        employee: person(r.assignee),
        effortMinutes: db.intervals
          .filter(
            (i) =>
              i.recordId === r.id && i.end && i.start >= start && i.end <= end,
          )
          .reduce((a, i) => a + Number(minutes(i.start, i.end)), 0),
        openInterval: db.intervals.some((i) => i.recordId === r.id && !i.end)
          ? "Provisional"
          : "None",
        workdayMinutes: db.workdays
          .filter(
            (w) =>
              w.assignee === r.assignee &&
              w.end &&
              w.start >= start &&
              w.end <= end,
          )
          .reduce((a, w) => a + Number(minutes(w.start, w.end)), 0),
        overdue:
          r.due &&
          !["Closed", "Cancelled"].includes(r.status) &&
          new Date(r.due) < new Date(snapshot)
            ? "Yes"
            : "No",
      }));
  } else if (key === "requests") {
    columns.push(
      "requestType",
      "receivingDepartment",
      "handler",
      "responseMinutes",
      "completionMinutes",
      "overdue",
      "cycles",
      "createdAt",
    );
    rows = scoped("requests")
      .filter(inPeriod)
      .map((r) => ({
        ...r,
        handler: person(r.assignee),
        responseMinutes: minutes(r.createdAt, r.acceptedAt),
        completionMinutes: minutes(r.createdAt, r.closedAt),
        cycles: (r.cycles?.length || 0) + 1,
        overdue:
          r.due &&
          !["Closed", "Rejected", "Cancelled"].includes(r.status) &&
          new Date(r.due) < new Date(snapshot)
            ? "Yes"
            : "No",
      }));
  }
  return { rows: structuredClone(rows), columns, snapshot, key, from, to };
}
