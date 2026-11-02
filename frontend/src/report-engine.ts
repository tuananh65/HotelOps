import { request } from "./api/http";
import { endpoints } from "./api/endpoints";
import * as service from "./services";
import type { ReportSnapshot } from "./report-export";
export const reportTypes = [
  { key: "rooms", name: "RPT-01 · Room readiness and housekeeping" },
  { key: "inspections", name: "RPT-02 · Inspection and rework" },
  { key: "incidents", name: "RPT-03 · Incidents and maintenance" },
  { key: "assets", name: "RPT-04 · Assets" },
  { key: "items", name: "RPT-05 · Inventory and material usage" },
  { key: "tasks", name: "RPT-06 · Tasks and workload" },
  { key: "requests", name: "RPT-07 · Internal requests" },
];
/** Server generates the scoped, immutable reporting snapshot (GBR-161/163). */
export async function generateReport(
  key: string,
  from: string,
  to: string,
  department = "",
  employee = "",
  signal?: AbortSignal,
): Promise<ReportSnapshot> {
  if (!service.can("UC-135"))
    throw Error("You do not have permission to generate reports.");
  if (!from || !to || from > to) throw Error("Select a valid report period.");
  const branch = service.branchContext();
  return request<ReportSnapshot>(
    `${endpoints.reports}/${encodeURIComponent(key)}`,
    {
      signal,
      query: {
        from,
        to,
        branchId:
          branch === "All branches"
            ? undefined
            : service.resolveReference("branches", branch),
        departmentId: department || undefined,
        employeeId: employee || undefined,
      },
    },
  );
}
