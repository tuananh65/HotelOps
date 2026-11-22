import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import { useState } from "react";
import { api, dateKey, fields, message, type Row, timestamp } from "./api";
import { useAuth } from "./auth";
import { Badge, Empty, Pager, State, useQuery } from "./ui";
import OperationalCharts from "./operational-charts";
import BranchMetrics from "./branch-metrics";
import OperationsMetrics from "./operations-metrics";
export default function Insights({
  mode,
  readScope,
}: {
  mode: "dashboard" | "reports" | "notifications" | "audit";
  readScope?: number;
}) {
  const { branch, can, profile } = useAuth();
  const branchRoles = profile.roles.filter(
    (r) => r.branchId === null || r.branchId === branch,
  );
  const departmentHead =
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "DEPT_MANAGER");
  const available = [
    ["tasks", "Công việc", "TASK_VIEW"],
    ["cleaning", "Dọn phòng", "CLEANING_VIEW"],
    ["incidents", "Sự cố", "INCIDENT_VIEW"],
    ["maintenance", "Bảo trì", "MAINTENANCE_VIEW"],
    ["requests", "Yêu cầu", "IR_VIEW"],
    ["materials", "Yêu cầu vật tư", "MR_VIEW"],
    ["workday", "Lịch phân ca", "SHIFT_MANAGE"],
    ["stock", "Tồn kho", "INVENTORY_VIEW"],
    ["documents", "Chứng từ", "INVENTORY_VIEW"],
    ["rooms", "Phòng hiện tại", "ROOM_VIEW"],
    ["assets", "Tài sản hiện tại", "ASSET_VIEW"],
  ].filter((r) => can(r[2], false));
  const [report, setReport] = useState(available[0]?.[0] || "tasks"),
    [from, setFrom] = useState(dateKey(new Date(Date.now() - 30 * 86400000))),
    [to, setTo] = useState(dateKey()),
    [revision, bump] = useState(0),
    [error, setError] = useState(""),
    [pending, setPending] = useState(false);
  const [reportPage, setReportPage] = useState(1);
  const operationsManager =
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "OPERATION_MANAGER");
  const [auditPage, setAuditPage] = useState(1),
    [auditSearch, setAuditSearch] = useState("");
  const [notificationPage, setNotificationPage] = useState(1);
  const path =
    mode === "notifications"
      ? `notifications?${readScope === 0 ? "" : `branchId=${readScope ?? branch}&`}page=${notificationPage}`
      : mode === "reports"
        ? `insights/reports/${report}?branchId=${branch}&from=${from}T00:00:00Z&to=${to}T23:59:59Z`
        : `insights/${mode}?branchId=${branch}${mode === "audit" ? `&page=${auditPage}&search=${encodeURIComponent(auditSearch)}` : ""}`;
  const query = useQuery(path, revision);
  const titles = {
    dashboard: "Tổng quan vận hành",
    reports: "Báo cáo",
    notifications: "Thông báo",
    audit: "Nhật ký hệ thống",
  };
  const raw = Array.isArray(query.data) ? query.data : query.data?.items || [],
    rows: Row[] = raw.map((r: Row) => r.fields || r);
  const currentReportPage = Math.min(
    reportPage,
    Math.max(1, Math.ceil(rows.length / 25)),
  );
  return (
    <>
      <div className="page-heading">
        <PageTitle>
          <h1>{titles[mode]}</h1>
          <p>
            {mode === "notifications"
              ? "Cập nhật và yêu cầu cần bạn xác nhận"
              : "Dữ liệu hiện tại trong phạm vi được cấp quyền"}
          </p>
        </PageTitle>
      </div>
      {mode === "reports" && (
        <PageToolbar className="report-controls">
          <label className="search">
            Loại báo cáo
            <select
              value={report}
              onChange={(e) => {
                setReport(e.target.value);
                setReportPage(1);
              }}
            >
              {available.map(([v, n]) => (
                <option key={v} value={v}>
                  {n}
                </option>
              ))}
            </select>
          </label>
          {!["stock", "documents", "rooms", "assets"].includes(report) ? (
            <div className="toolbar">
              <label>
                Từ ngày
                <input
                  type="date"
                  value={from}
                  max={to}
                  onChange={(e) => {
                    if (e.target.value) {
                      setFrom(e.target.value);
                      setReportPage(1);
                    }
                  }}
                />
              </label>
              <label>
                Đến ngày
                <input
                  type="date"
                  value={to}
                  min={from}
                  onChange={(e) => {
                    if (e.target.value) {
                      setTo(e.target.value);
                      setReportPage(1);
                    }
                  }}
                />
              </label>
            </div>
          ) : (
            <p className="hint">
              Dữ liệu hiện tại; không lọc theo ngày tạo công việc.
            </p>
          )}
          <button
            disabled={query.loading || !!query.error || !rows.length}
            onClick={() => {
              const keys = Object.keys(rows[0]).filter(
                  (k) => k !== "rowVersion",
                ),
                cell = (v: unknown) =>
                  '"' +
                  String(v ?? "")
                    .replace(/^[=+@-]/, "'$&")
                    .replaceAll('"', '""') +
                  '"',
                csv = [
                  keys.map(cell).join(","),
                  ...rows.map((r) => keys.map((k) => cell(r[k])).join(",")),
                ].join("\r\n");
              const href = URL.createObjectURL(
                  new Blob(["\uFEFF" + csv], {
                    type: "text/csv;charset=utf-8",
                  }),
                ),
                link = document.createElement("a");
              link.href = href;
              link.download = `hotelops-${report}-${dateKey()}.csv`;
              link.click();
              setTimeout(() => URL.revokeObjectURL(href), 1000);
            }}
          >
            Xuất CSV
          </button>
        </PageToolbar>
      )}
      {mode === "audit" && (
        <PageToolbar>
          <label className="search">
            Tìm nhật ký
            <input
              placeholder="Hành động, loại hoặc mã đối tượng…"
              value={auditSearch}
              onChange={(e) => {
                setAuditSearch(e.target.value);
                setAuditPage(1);
              }}
            />
          </label>
        </PageToolbar>
      )}
      {error && (
        <p role="alert" className="alert">
          {error}
        </p>
      )}
      <State {...query} retry={() => bump((x) => x + 1)}>
        {mode === "dashboard" ? (
          query.data && (
            <>
              {departmentHead && <BranchMetrics data={query.data} />}
              {operationsManager && <OperationsMetrics data={query.data} />}
              <OperationalCharts data={query.data} />
            </>
          )
        ) : mode === "notifications" ? (
          <div className="notification-list">
            {rows.map((r) => (
              <article className="panel" key={r.notificationId}>
                <div>
                  <h3>{r.title}</h3>
                  <p>{r.message}</p>
                  <small>{timestamp(r.createdAt)}</small>
                  {r.workId && (
                    <button
                      disabled={pending}
                      onClick={async () => {
                        setPending(true);
                        setError("");
                        try {
                          const d = await api(`work/${r.workId}`);
                          location.hash = `work?kind=${encodeURIComponent(d.work.fields.kind)}&id=${r.workId}`;
                        } catch (e) {
                          setError(message(e));
                        } finally {
                          setPending(false);
                        }
                      }}
                    >
                      Mở công việc
                    </button>
                  )}
                  {r.documentId && (
                    <button
                      disabled={pending}
                      onClick={async () => {
                        setPending(true);
                        setError("");
                        try {
                          const detail = await api(`documents/${r.documentId}`);
                          location.hash = `documents?kind=${encodeURIComponent(detail.document.fields.kind)}&id=${r.documentId}`;
                        } catch (e) {
                          setError(message(e));
                        } finally {
                          setPending(false);
                        }
                      }}
                    >
                      Mở yêu cầu vật tư
                    </button>
                  )}
                </div>
                <button
                  disabled={
                    pending ||
                    !!(r.requiresAcknowledgment ? r.acknowledgedAt : r.readAt)
                  }
                  onClick={async () => {
                    setPending(true);
                    try {
                      await api(
                        `notifications/${r.notificationId}/${r.requiresAcknowledgment ? "acknowledge" : "read"}`,
                        {},
                      );
                      bump((x) => x + 1);
                    } catch (e) {
                      setError(message(e));
                    } finally {
                      setPending(false);
                    }
                  }}
                >
                  {r.requiresAcknowledgment ? "Xác nhận" : "Đánh dấu đã đọc"}
                </button>
              </article>
            ))}
            {!rows.length && <Empty>Bạn đã xem hết thông báo.</Empty>}
            <div className="pager">
              <span>Trang {notificationPage}</span>
              <button
                disabled={notificationPage === 1}
                onClick={() => setNotificationPage((p) => p - 1)}
              >
                Trước
              </button>
              <button
                disabled={!query.data?.hasMore}
                onClick={() => setNotificationPage((p) => p + 1)}
              >
                Tiếp
              </button>
            </div>
          </div>
        ) : mode === "audit" ? (
          <>
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Thời điểm</th>
                    <th>Hành động</th>
                    <th>Đối tượng</th>
                    <th>Người thực hiện</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.auditId}>
                      <td>{timestamp(r.occurredAt)}</td>
                      <td>{r.action}</td>
                      <td>
                        {r.entityType} · {r.entityKey}
                      </td>
                      <td>{r.actorUserId ?? "Hệ thống"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {!rows.length && <Empty />}
            <div className="pager">
              <span>
                Trang {auditPage} · {rows.length} bản ghi
              </span>
              <button
                disabled={auditPage === 1}
                onClick={() => setAuditPage((p) => p - 1)}
              >
                Trước
              </button>
              <button
                disabled={rows.length < 100}
                onClick={() => setAuditPage((p) => p + 1)}
              >
                Tiếp
              </button>
            </div>
          </>
        ) : (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  {Object.keys(rows[0] || {})
                    .filter((k) => !["rowVersion"].includes(k))
                    .map((k) => (
                      <th key={k}>
                        {(
                          {
                            workId: "Mã việc",
                            assetCode: "Mã tài sản",
                            name: "Tên",
                            conditionCode: "Tình trạng",
                            isActive: "Đang sử dụng",
                            roomId: "Mã phòng",
                            locationId: "Mã vị trí",
                            warrantyUntil: "Bảo hành đến",
                            code: "Mã",
                            operationalStatus: "Trạng thái vận hành",
                            readiness: "Mức sẵn sàng",
                            isOccupied: "Có khách",
                            workNo: "Số công việc",
                            status: "Trạng thái",
                            title: "Tiêu đề",
                            createdAt: "Ngày tạo",
                            completedAt: "Hoàn thành",
                            verifiedAt: "Xác nhận",
                            dueAt: "Hạn xử lý",
                            completedSeconds: "Giây thực hiện",
                            warehouseId: "Kho",
                            itemId: "Vật tư",
                            baseQty: "Tồn cơ sở",
                            documentId: "Mã yêu cầu",
                            documentNo: "Số yêu cầu",
                            approvedAt: "Ngày duyệt",
                            closedAt: "Ngày đóng",
                            assignmentId: "Mã phân ca",
                            employeeCode: "Mã nhân viên",
                            fullName: "Nhân viên",
                            department: "Bộ phận",
                            shift: "Ca",
                            workDate: "Ngày làm việc",
                            startedAt: "Vào ca",
                            endedAt: "Ra ca",
                          } as Record<string, string>
                        )[k] || k}
                      </th>
                    ))}
                </tr>
              </thead>
              <tbody>
                {rows
                  .slice((currentReportPage - 1) * 25, currentReportPage * 25)
                  .map((r, i) => (
                    <tr key={i}>
                      {Object.entries(r)
                        .filter(([k]) => k !== "rowVersion")
                        .map(([k, v]) => (
                          <td key={k}>
                            {k === "status" ? (
                              <Badge value={String(v)} />
                            ) : k.endsWith("At") ? (
                              timestamp(v)
                            ) : (
                              String(v ?? "—")
                            )}
                          </td>
                        ))}
                    </tr>
                  ))}
              </tbody>
            </table>
            {!rows.length && <Empty />}
          </div>
        )}
      </State>
      {mode === "reports" && !query.loading && !query.error && (
        <>
          {rows.length === 10000 && (
            <p className="hint">
              Báo cáo đạt giới hạn 10.000 bản ghi. Thu hẹp khoảng ngày để xem đủ
              dữ liệu.
            </p>
          )}
          <Pager
            page={currentReportPage}
            pageSize={25}
            total={rows.length}
            onPage={setReportPage}
          />
        </>
      )}
    </>
  );
}
