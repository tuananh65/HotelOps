import { referenceLabel } from "../../i18n/referenceLabel";
import { useWorkspacePermissions } from "../housekeeping/useWorkspacePermissions";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import type {
  RoomDetail,
  RoomHistory,
  RoomSource,
} from "../../api/dev1/roomBoard";
import {
  Fact,
  operationalLabel as label,
  ReadDrawer,
} from "../housekeeping/OperationalReadView";
import { roomLabel, hotelTime } from "../housekeeping/RoomWorkspace";
import RoomWorkflowActions from "./RoomWorkflowActions";

export function RoomStatusDetails({
  detail,
  history,
  branchName,
  close,
  paginate,
  sourceLink,
}: {
  detail: RoomDetail;
  history?: RoomHistory;
  branchName: string;
  close: () => void;
  paginate: (page: number) => void;
  sourceLink: (source: RoomSource | null) => ReactNode;
}) {
  const { room, currentCycle: cycle, currentStatusEvidence: evidence } = detail;
  const access = useWorkspacePermissions(room.branchId);
  return (
    <ReadDrawer title={`Phòng ${room.roomNumber}`} onClose={close}>
      <section aria-label="Chi tiết trạng thái phòng">
        <dl className="ops-facts">
          <Fact label="Chi nhánh">{branchName}</Fact>
          <Fact label="Vị trí">
            {room.floor.name}
            {room.area && ` · ${room.area.name}`}
          </Fact>
          <Fact label="Loại phòng">{room.roomType.name}</Fact>
          <Fact label="Hoạt động">
            {room.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
          </Fact>
          <Fact label="Mức sẵn sàng thực tế">
            {roomLabel(room.effectiveReadiness ?? "UNKNOWN")}
          </Fact>
          <Fact label="Trạng thái đã ghi nhận">
            {roomLabel(room.operationalStatusCode)}
          </Fact>
        </dl>
        {(room.effectiveReadiness ?? "UNKNOWN") === "UNKNOWN" && (
          <p className="ops-note">
            Chưa đủ dữ liệu xác nhận phòng sẵn sàng. Cần kiểm tra chu kỳ vệ sinh
            và các lệnh khóa.
          </p>
        )}
        {detail.description && (
          <p className="ops-muted">{detail.description}</p>
        )}
      </section>
      <section aria-label="Chu kỳ vệ sinh hiện tại">
        <h3>Chu kỳ vệ sinh hiện tại</h3>
        {!cycle && <p>Chưa ghi nhận chu kỳ vệ sinh.</p>}
        {cycle && (
          <dl className="ops-facts">
            <Fact label="Công việc vệ sinh">
              {cycle.cleaningTaskId ? (
                <>
                  {access.allows("CLEANING_VIEW") ? (
                    <Link
                      to={`/cleaning/tasks/${cycle.cleaningTaskId}?branchId=${room.branchId}&monitor=1&kind=cleaning`}
                    >
                      Vệ sinh {referenceLabel(cycle, "cleaningTaskId")}
                    </Link>
                  ) : (
                    <span>Vệ sinh {referenceLabel(cycle, "cleaningTaskId")}</span>
                  )}
                  <p>{label(cycle.cleaningState)}</p>
                </>
              ) : (
                "Chưa ghi nhận"
              )}
            </Fact>
            <Fact label="Kiểm tra phòng">
              {cycle.inspectionId ? (
                <>
                  {access.allows("INSPECTION_VIEW") ? (
                    <Link
                      to={`/inspection-history/${cycle.inspectionId}?branchId=${room.branchId}`}
                    >
                      Kiểm tra {referenceLabel(cycle, "inspectionId")}
                    </Link>
                  ) : (
                    <span>Kiểm tra {referenceLabel(cycle, "inspectionId")}</span>
                  )}
                  <p>{label(cycle.inspectionState)}</p>
                </>
              ) : (
                "Chưa ghi nhận"
              )}
            </Fact>
            {cycle.reworkId && (
              <Fact label="Công việc làm lại">
                {access.allows("CLEANING_VIEW") ? (
                  <Link
                    to={`/cleaning/reworks/${cycle.reworkId}?branchId=${room.branchId}`}
                  >
                    Làm lại {referenceLabel(cycle, "reworkId")}
                  </Link>
                ) : (
                  <span>Làm lại {referenceLabel(cycle, "reworkId")}</span>
                )}
              </Fact>
            )}
          </dl>
        )}
      </section>
      <RoomWorkflowActions
        branch={String(room.branchId)}
        room={String(room.id)}
      />
      <section aria-label="Lệnh khóa phòng có hiệu lực">
        <h3>Lệnh khóa phòng</h3>
        {!detail.effectiveBlocks.length && (
          <p>Không có lệnh khóa đang có hiệu lực.</p>
        )}
        {detail.effectiveBlocks.map((b) => (
          <article key={b.id}>
            <strong>Lệnh khóa {referenceLabel(b, "id")}</strong>
            <p>Từ {hotelTime(b.blockedAtUtc)}</p>
            {b.incidentId && access.allows("INCIDENT_VIEW") && (
              <Link
                to={`/operations/incidents/${b.incidentId}?branchId=${room.branchId}&action=A004A-023`}
              >
                Xem sự cố {referenceLabel(b, "incidentId")}
              </Link>
            )}
          </article>
        ))}
        {!!detail.unreleasedBlocks.length && (
          <details>
            <summary>
              Lệnh khóa chưa kết thúc ({detail.unreleasedBlocks.length})
            </summary>
            <p className="ops-muted">
              Thời điểm dự kiến không xác nhận phòng đã được mở lại.
            </p>
            {detail.unreleasedBlocks.map((b) => (
              <article key={b.id}>
                <strong>{b.reason}</strong>
                <p>Bắt đầu: {hotelTime(b.blockedAtUtc)}</p>
                <p>Dự kiến mở lại: {hotelTime(b.expectedReleaseAtUtc)}</p>
              </article>
            ))}
          </details>
        )}
      </section>
      <details>
        <summary>Thông tin xác nhận trạng thái</summary>
        <dl className="ops-facts">
          <Fact label="Ghi nhận">{label(evidence.availability)}</Fact>
          <Fact label="Thời điểm (giờ Việt Nam)">
            {hotelTime(evidence.recordedAtUtc)}
          </Fact>
        </dl>
        <p>{evidence.reason || "Chưa ghi nhận lý do."}</p>
        {evidence.source && (
          <>
            <p className="ops-muted">
              {label(evidence.source.type)} ·{" "}
              {label(evidence.source.availability)}
            </p>
            {sourceLink(evidence.source)}
          </>
        )}
      </details>
      {history && (
        <details open aria-label="Lịch sử trạng thái">
          <summary>Lịch sử trạng thái ({history.totalCount})</summary>
          {!history.items.length && <p>Chưa có sự kiện trên trang này.</p>}
          <ol className="ops-timeline">
            {history.items.map((e) => (
              <li key={e.id}>
                <time>{hotelTime(e.changedAtUtc)}</time>
                <strong>
                  {label(e.fromStatusCode)} → {label(e.toStatusCode)}
                </strong>
                {e.effectiveTo && (
                  <p>
                    Mức sẵn sàng: {label(e.effectiveFrom)} →{" "}
                    {label(e.effectiveTo)}
                  </p>
                )}
                {e.reason && <p>{e.reason}</p>}
                <p className="ops-muted">
                  {label(e.source.type)} · {label(e.source.availability)}
                  {e.changedByUserId &&
                    ` · Mã người cập nhật: ${e.changedByUserId}`}
                </p>
                {sourceLink(e.source)}
              </li>
            ))}
          </ol>
          {history.totalCount > history.pageSize && (
            <nav className="ops-actions" aria-label="Phân trang lịch sử phòng">
              <button
                disabled={history.page <= 1}
                onClick={() => paginate(history.page - 1)}
              >
                Trước
              </button>
              <span>Trang {history.page}</span>
              <button
                disabled={history.page * history.pageSize >= history.totalCount}
                onClick={() => paginate(history.page + 1)}
              >
                Sau
              </button>
            </nav>
          )}
        </details>
      )}
    </ReadDrawer>
  );
}
