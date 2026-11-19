import { referenceLabel } from "../i18n/referenceLabel";
import { StatusBadge } from "../components/design-system";
import { displayCode } from "../i18n/vi";
import { staffDate } from "../internalRequests/staffDate";
import { MaintenanceVerifyPanel } from "./MaintenanceVerifyPanel";
import { IncidentEvidencePanel } from "./IncidentEvidencePanel";
import { MaintenanceReturnPanel } from "./MaintenanceReturnPanel";
import { MaintenanceDeclinePanel } from "./MaintenanceDeclinePanel";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";
import { MaintenanceProgressPanel } from "./MaintenanceProgressPanel";
import { MaintenanceSubmissionPanel } from "./MaintenanceSubmissionPanel";
import { MaintenanceResumePanel } from "./MaintenanceResumePanel";

const show = (v: unknown) =>
  typeof v === "string" || typeof v === "number" ? String(v) : "Chưa khả dụng";
const target = (row: Values) =>
  [
    row.roomNumber && `Phòng ${row.roomNumber}`,
    row.areaName,
    row.assetCode &&
      `${row.assetCode}${row.assetName ? " — " + row.assetName : ""}`,
  ]
    .filter((v) => typeof v === "string" && v.length > 0)
    .join(" / ") || "Đối tượng chưa khả dụng";
const time = (v: unknown) =>
  typeof v === "string" ? staffDate(v) : "Chưa ghi nhận";
const sourceLabel = (v: unknown) =>
  v === "INCIDENT"
    ? "Sự cố"
    : v === "PREVENTIVE"
      ? "Bảo trì định kỳ"
      : displayCode(v);

export function MaintenanceReadView({
  data,
  detail,
  canDetail,
  canResume = false,
  canStart = false,
  canHold = false,
  canSubmit = false,
  canLog = false,
  canMaterials = false,
  canReturn = false,
  canVerify = false,
  onCommitted,
}: {
  data: Values;
  detail: boolean;
  canDetail: boolean;
  canResume?: boolean;
  canStart?: boolean;
  canHold?: boolean;
  canSubmit?: boolean;
  canLog?: boolean;
  canMaterials?: boolean;
  canReturn?: boolean;
  canVerify?: boolean;
  onCommitted?: (message: string) => void;
}) {
  if (!detail) {
    const rows = Array.isArray(data.items) ? data.items.filter(isObject) : [];
    return (
      <section className="ops-read-card" aria-label="Kết quả công việc bảo trì">
        <h2>Danh sách công việc</h2>
        <p>
          {show(data.totalCount)} hồ sơ phù hợp; trang {show(data.page)} trên{" "}
          {show(data.totalPages)}.
        </p>
        {rows.length === 0 ? (
          <p>Không có công việc bảo trì phù hợp bộ lọc và phạm vi quyền.</p>
        ) : (
          <div
            className="ops-read-scroll"
            role="region"
            aria-label="Bảng công việc bảo trì"
            tabIndex={0}
          >
            <table className="ops-read-table">
              <thead>
                <tr>
                  <th>Công việc</th>
                  <th>Vị trí / tài sản</th>
                  <th>Người thực hiện</th>
                  <th>Trạng thái</th>
                  <th>Hạn hoàn thành</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row, index) => (
                  <tr
                    key={
                      show(row.occurrenceId ?? row.maintenanceWorkId) +
                      ":" +
                      index
                    }
                  >
                    <td>
                      {typeof row.occurrenceId === "number" &&
                      Number.isSafeInteger(row.occurrenceId) &&
                      row.occurrenceId > 0 &&
                      typeof row.branchId === "number" &&
                      Number.isSafeInteger(row.branchId) &&
                      row.branchId > 0 ? (
                        <Link
                          to={`/operations/maintenance?branchId=${row.branchId}&runId=${row.occurrenceId}`}
                        >
                          {show(row.workNo)}
                        </Link>
                      ) : canDetail &&
                        typeof row.maintenanceWorkId === "number" &&
                        Number.isSafeInteger(row.maintenanceWorkId) &&
                        row.maintenanceWorkId > 0 ? (
                        <Link
                          to={`/operations/maintenance/${row.maintenanceWorkId}?action=A004A-036&branchId=${row.branchId}`}
                        >
                          {show(row.workNo)}
                        </Link>
                      ) : (
                        show(row.workNo)
                      )}
                      <small>{show(row.title)}</small>
                      <small>{sourceLabel(row.source)}</small>
                    </td>
                    <td>
                      {target(row)}
                      <small>Ưu tiên: {show(row.priorityName)}</small>
                    </td>
                    <td>{show(row.assigneeName ?? row.assigneeId)}</td>
                    <td>
                      <StatusBadge>{displayCode(row.status)}</StatusBadge>
                    </td>
                    <td>
                      {isObject(row.deadline) &&
                      typeof row.deadline.dueAt === "string"
                        ? time(row.deadline.dueAt)
                        : "Chưa có thông tin"}
                      <small>
                        {isObject(row.deadline) &&
                        row.deadline.isOverdue === true
                          ? "Quá hạn"
                          : isObject(row.deadline) &&
                              row.deadline.isOverdue === false
                            ? "Trong hạn"
                            : "Chưa xác định thời hạn"}
                      </small>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    );
  }
  const logs = Array.isArray(data.maintenanceWorkLogs)
    ? data.maintenanceWorkLogs.filter(isObject)
    : [];
  const deadline = isObject(data.deadline) ? data.deadline : null;
  const submitted = isObject(data.submittedReview)
    ? data.submittedReview
    : null;
  const usages = logs.flatMap((log) =>
    Array.isArray(log.materialUsages)
      ? log.materialUsages.filter(isObject)
      : [],
  );
  return (
    <section className="ops-read-card" aria-label="Chi tiết công việc bảo trì">
      {data.submissionWorkflow === "PENDING_VERIFICATION" && (
        <p role="status">
          Đang chờ xác minh. Đã nộp kết quả sửa chữa và kết thúc khoảng làm
          việc. Sự cố vẫn mở đến khi người xác minh độc lập chấp nhận.
        </p>
      )}
      {data.submissionWorkflow === "UNVERIFIED" && (
        <p role="alert">
          Bằng chứng nộp không nhất quán hoặc chưa khả dụng. Cần đối soát hồ sơ
          trước khi xác minh.
        </p>
      )}
      {typeof data.incidentId === "number" &&
        Number.isSafeInteger(data.incidentId) &&
        data.incidentId > 0 &&
        (data.assignmentWorkflow === "ASSIGNED" ||
          data.assignmentWorkflow === "PENDING_ASSIGNMENT") && (
          <Link
            to={`/operations/incidents/${data.incidentId}?action=A004A-023`}
          >
            Mở phân công xử lý sự cố
          </Link>
        )}
      {canStart && (
        <MaintenanceDeclinePanel
          onCommitted={onCommitted}
          key={
            String(data.maintenanceWorkId) + ":" + String(data.executionETag)
          }
          data={data}
        />
      )}
      {Array.isArray(data.declines) && data.declines.length > 0 && (
        <section aria-label="Lịch sử từ chối nhận việc">
          <h3>Lượt từ chối nhận việc</h3>
          <ul>
            {data.declines.filter(isObject).map((row) => (
              <li key={String(row.auditId)}>
                {time(row.occurredAt)} ? Người được giao{" "}
                {show(row.previousAssigneeId)}: {show(row.reason)}
              </li>
            ))}
          </ul>
        </section>
      )}
      {canVerify && (
        <MaintenanceVerifyPanel
          key={
            String(data.maintenanceWorkId) + ":" + String(data.executionETag)
          }
          data={data}
          onCommitted={onCommitted}
        />
      )}
      {canReturn && (
        <MaintenanceReturnPanel
          key={
            String(data.maintenanceWorkId) + ":" + String(data.executionETag)
          }
          data={data}
          onCommitted={onCommitted}
        />
      )}
      {submitted && (
        <section aria-label="Thông tin bảo trì tại thời điểm nộp">
          <h3>Kết quả đã nộp</h3>
          <p>
            Gửi kết quả #{show(submitted.submissionAuditId)} lúc{" "}
            {time(submitted.submittedAtUtc)}.
          </p>
          <dl>
            <dt>Chẩn đoán / nguyên nhân gốc</dt>
            <dd>{show(submitted.diagnosis)}</dd>
            <dt>Biện pháp đã thực hiện</dt>
            <dd>{show(submitted.actionTaken)}</dd>
          </dl>
          <p>
            Kiểm tra sử dụng vật tư bên dưới. Tải bằng chứng cần quyền xem sự cố
            hiện tại.
          </p>
          {Array.isArray(submitted.resultAttachmentIds) &&
            typeof data.incidentId === "number" &&
            Number.isSafeInteger(data.incidentId) &&
            data.incidentId > 0 && (
              <ul>
                {submitted.resultAttachmentIds
                  .filter(
                    (id): id is number =>
                      typeof id === "number" &&
                      Number.isSafeInteger(id) &&
                      id > 0,
                  )
                  .map((id) => (
                    <li key={id}>
                      <a
                        href={`/api/incidents/${data.incidentId}/evidence/${id}`}
                      >
                        Bằng chứng kết quả {id}
                      </a>
                    </li>
                  ))}
              </ul>
            )}
        </section>
      )}
      {canLog && (
        <MaintenanceProgressPanel
          key={
            String(data.maintenanceWorkId) + ":" + String(data.executionETag)
          }
          data={data}
          allowMaterials={canMaterials}
          onCommitted={onCommitted}
        />
      )}
      {canSubmit && (
        <MaintenanceSubmissionPanel
          key={
            String(data.maintenanceWorkId) + ":" + String(data.executionETag)
          }
          data={data}
          onCommitted={onCommitted}
        />
      )}
      {typeof data.incidentId === "number" &&
        Number.isSafeInteger(data.incidentId) &&
        data.incidentId > 0 &&
        !(canSubmit && data.canSubmit === true) && (
          <IncidentEvidencePanel
            key={data.incidentId}
            incidentId={data.incidentId}
            readOnly
          />
        )}
      {(canStart || canResume || canHold) && (
        <MaintenanceResumePanel
          key={
            String(data.maintenanceWorkId) + ":" + String(data.executionETag)
          }
          data={data}
          allowStart={canStart}
          allowResume={canResume}
          allowHold={canHold}
          onCommitted={onCommitted}
        />
      )}
      <h3>
        {show(data.workNo)} — {show(data.title)}
      </h3>
      <dl>
        <dt>Trạng thái</dt>
        <dd>{displayCode(data.status)}</dd>
        <dt>Người được giao</dt>
        <dd>{show(data.assigneeName ?? data.assigneeId)}</dd>
        <dt>Nguồn</dt>
        <dd>{sourceLabel(data.source)}</dd>
        <dt>Vị trí / tài sản</dt>
        <dd>{target(data)}</dd>
        <dt>Độ ưu tiên</dt>
        <dd>{show(data.priorityName)}</dd>
        <dt>Hướng dẫn</dt>
        <dd>{show(data.instruction)}</dd>
        <dt>Bắt đầu dự kiến</dt>
        <dd>{time(data.plannedStartAt)}</dd>
        <dt>Kết thúc dự kiến</dt>
        <dd>{time(data.plannedEndAt)}</dd>
        <dt>Bắt đầu thực tế</dt>
        <dd>{time(data.actualStartAt)}</dd>
        <dt>Xử lý</dt>
        <dd>{show(data.resolutionNote)}</dd>
        <dt>Xử lý xong lúc</dt>
        <dd>{time(data.resolvedAt)}</dd>
      </dl>
      <section aria-label="Thời hạn bảo trì">
        <h4>Hạn hoàn thành</h4>
        <p>
          {deadline?.dueAt ? time(deadline.dueAt) : "Chưa có thông tin"}{" "}
          {deadline?.source === "INCIDENT_RESOLUTION_SLA"
            ? "(SLA xử lý sự cố)"
            : deadline?.source === "PREVENTIVE_OCCURRENCE"
              ? "(Lần bảo trì định kỳ)"
              : ""}
        </p>
        <p>
          Quá hạn:{" "}
          {deadline?.isOverdue === true
            ? "Có"
            : deadline?.isOverdue === false
              ? "Không"
              : "Chưa có thông tin"}
        </p>
        {deadline?.unavailableReason != null && (
          <p role="status">
            Chưa có thông tin thời hạn: {show(deadline.unavailableReason)}.
          </p>
        )}
      </section>
      <h4>Lịch sử công việc</h4>
      {data.assignmentWorkflow != null && (
        <p>
          Quy trình phân công: {show(data.assignmentWorkflow)}.
          {data.assignmentWorkflow === "UNVERIFIED" &&
            " Cần đối soát thông tin phân loại và phân công trước khi nhận việc."}
        </p>
      )}
      {data.source === "INCIDENT" && (
        <section aria-label="Thời hạn SLA của sự cố">
          <h4>Thời hạn SLA của sự cố</h4>
          <dl>
            <dt>Hạn phản hồi</dt>
            <dd>
              {isObject(data.incidentSla)
                ? time(data.incidentSla.responseDeadlineAt)
                : "Chưa khả dụng"}
            </dd>
            <dt>Hạn xử lý</dt>
            <dd>
              {isObject(data.incidentSla)
                ? time(data.incidentSla.resolutionDeadlineAt)
                : "Chưa khả dụng"}
            </dd>
          </dl>
          {(!isObject(data.incidentSla) ||
            data.incidentSla.unavailableReason != null) && (
            <p>
              {isObject(data.incidentSla) &&
              data.incidentSla.unavailableReason ===
                "INCIDENT_SLA_ACCESS_UNAVAILABLE"
                ? "Quyền hiện tại không cho phép xem thời hạn SLA sự cố."
                : isObject(data.incidentSla) &&
                    data.incidentSla.unavailableReason ===
                      "INCIDENT_SLA_INVALID"
                  ? "Thời hạn đã ghi không nhất quán và không thể sử dụng."
                  : "Thời hạn SLA sự cố chưa được ghi nhận hoặc chưa khả dụng."}
            </p>
          )}
          <p>Thời gian bảo trì dự kiến không thay thế thời hạn SLA sự cố.</p>
        </section>
      )}
      {logs.length === 0 ? (
        <p>Chưa có nhật ký công việc đã ghi nhận.</p>
      ) : (
        <ol>
          {logs.map((log, index) => (
            <li key={show(log.logId) + ":" + index}>
              {typeof log.progressPercent === "number" &&
              Number.isInteger(log.progressPercent) &&
              log.progressPercent >= 0 &&
              log.progressPercent <= 100
                ? `${log.progressPercent}% ? `
                : ""}
              {time(log.loggedAt)} — {displayCode(log.status)} —{" "}
              {show(log.workNote)} ({show(log.spentMinutes)} phút)
            </li>
          ))}
        </ol>
      )}
      <h4>Sử dụng vật tư</h4>
      {data.materialUsageAvailable !== true ? (
        <p>
          Chưa có thông tin sử dụng vật tư: cần xác minh quyền vật tư / kho và
          tính toàn vẹn chứng từ nguồn.
        </p>
      ) : usages.length === 0 ? (
        <p>Chưa ghi nhận sử dụng vật tư.</p>
      ) : (
        <div className="ops-read-scroll">
          <table className="ops-read-table">
            <thead>
              <tr>
                <th>Mã vật tư</th>
                <th>Số lượng đã dùng</th>
                <th>Mã đơn vị</th>
                <th>Số lượng theo đơn vị cơ sở</th>
                <th>Mã đơn vị cơ sở</th>
                <th>Phiếu xuất kho</th>
                <th>Yêu cầu vật tư</th>
              </tr>
            </thead>
            <tbody>
              {usages.map((usage, index) => (
                <tr key={show(usage.usageId) + ":" + index}>
                  <td>{show(usage.itemId)}</td>
                  <td>{show(usage.usedQty)}</td>
                  <td>{show(usage.uomId)}</td>
                  <td>{show(usage.usedBaseQty)}</td>
                  <td>{show(usage.baseUomId)}</td>
                  <td>{show(usage.documentNo)}</td>
                  <td>
                    {typeof usage.requestId === "number" &&
                    Number.isSafeInteger(usage.requestId) &&
                    usage.requestId > 0 ? (
                      <Link
                        to={`/operations/material-requests/${usage.requestId}?action=A004A-038`}
                      >
                        {referenceLabel(usage, "requestId")}
                      </Link>
                    ) : (
                      "Chưa khả dụng"
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
