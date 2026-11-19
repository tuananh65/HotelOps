import { referenceLabel } from "../../i18n/referenceLabel";
import type { Detail, Evidence, Inspection } from "./model";
import {
  Fact,
  operationalLabel as label,
  recordedTime as time,
} from "../../screens/housekeeping/OperationalReadView";

export function InspectionSummary({ row }: { row: Inspection }) {
  return (
    <dl className="ops-facts">
      <Fact label="Phòng">{row.roomNumber}</Fact>
      <Fact label="Người kiểm tra">{row.inspectorName}</Fact>
      <Fact label="Trạng thái">{label(row.status)}</Fact>
      <Fact label="Kết quả">
        {row.result ? label(row.result) : "Chưa kết luận"}
      </Fact>
      <Fact label="Thời điểm ghi nhận">{time(row.at)}</Fact>
      <Fact label="Danh sách kiểm tra">
        Phiên bản {row.versionNumber}
      </Fact>
      <Fact label="Lỗi của lượt kiểm tra">
        {row.defectCount ?? "Chưa xác minh"}
      </Fact>
      <Fact label="Yêu cầu làm lại">{row.reworkCount}</Fact>
    </dl>
  );
}
export function HistoryDetails({
  detail,
  open,
  saveEvidence,
  downloadBusy,
}: {
  detail: Detail;
  open: (id: number) => void;
  saveEvidence: (e: Evidence) => Promise<void>;
  downloadBusy: boolean;
}) {
  return (
    <>
      <section aria-label="Chi tiết lịch sử">
        <h3>{detail.inspection.number}</h3>
        <InspectionSummary row={detail.inspection} />
        {detail.inspection.summary && (
          <p className="ops-note">{detail.inspection.summary}</p>
        )}
      </section>
      <details>
        <summary>Nhân viên dọn phòng</summary>
        <p>
          {detail.completedBy
            ? `Người hoàn tất: ${detail.completedBy.name} · ${time(detail.completedBy.completedAt)}`
            : "Chưa ghi nhận người hoàn tất dọn phòng."}
        </p>
        <h3>Lịch sử phân công</h3>
        {!detail.housekeepers.length && <p>Chưa có phân công.</p>}
        {detail.housekeepers.map((a) => (
          <article key={a.id}>
            <strong>{a.name}</strong>
            <p>{label(a.status)}</p>
            <p className="ops-muted">
              {time(a.assignedAt)} —{" "}
              {a.endedAt ? time(a.endedAt) : "Chưa kết thúc"}
            </p>
            {a.reason && <p>{a.reason}</p>}
          </article>
        ))}
      </details>
      <details open>
        <summary>Tiêu chí kiểm tra ({detail.criteria.length})</summary>
        {!detail.criteria.length && <p>Chưa có tiêu chí được ghi nhận.</p>}
        {detail.criteria.map((c, index) => (
          <article key={c.itemId}>
            <h4>
              {index + 1}. {c.criterion}
            </h4>
            <p className="ops-muted">
              {c.group && `${c.group} · `}
              {c.mandatory ? "Bắt buộc" : "Tùy chọn"} · {label(c.outcome)}
            </p>
            {c.description && <p>{c.description}</p>}
            {c.passCondition && (
              <p>
                <strong>Điều kiện đạt:</strong> {c.passCondition}
              </p>
            )}
            {c.note && (
              <p>
                <strong>Ghi chú:</strong> {c.note}
              </p>
            )}
            {c.at && <p className="ops-muted">Ghi nhận: {time(c.at)}</p>}
            {!!c.evidenceIds?.length && (
              <p className="ops-muted">
                Tệp bằng chứng: {c.evidenceIds.length} tệp
              </p>
            )}
          </article>
        ))}
      </details>
      <details open={detail.defects.length > 0}>
        <summary>Lỗi cần khắc phục ({detail.defects.length})</summary>
        {!detail.defects.length && <p>Chưa ghi nhận lỗi.</p>}
        {detail.defects.map((d) => (
          <article key={d.id}>
            <h4>{d.description}</h4>
            <p>
              {label(d.status)}
              {d.location && ` · ${d.location}`}
            </p>
            {d.itemId && (
              <p className="ops-muted">
                Tiêu chí:{" "}
                {detail.criteria.find((c) => c.itemId === d.itemId)
                  ?.criterion ?? "Chưa có tên tiêu chí"}
              </p>
            )}
            {d.severityId && (
              <p className="ops-muted">Mức độ: {referenceLabel(d, "severityId")}</p>
            )}
            {d.correctiveAction && <p>Hướng xử lý: {d.correctiveAction}</p>}
            {d.resolutionNote && <p>{d.resolutionNote}</p>}
            {d.resolvedAt && <p>Đã xử lý: {time(d.resolvedAt)}</p>}
            {!!d.evidenceIds?.length && (
              <p className="ops-muted">
                Tệp bằng chứng: {d.evidenceIds.length} tệp
              </p>
            )}
          </article>
        ))}
      </details>
      <details>
        <summary>Công việc làm lại ({detail.reworks.length})</summary>
        {!detail.reworks.length && <p>Chưa có yêu cầu làm lại.</p>}
        {detail.reworks.map((r) => (
          <article key={r.id}>
            <h4>
              Công việc làm lại · {label(r.status)}
            </h4>
            <p>{r.reason}</p>
            {r.note && <p>{r.note}</p>}
            <p className="ops-muted">
              Người được giao:{" "}
              {detail.housekeepers.find((a) => a.userId === r.assigneeId)
                ?.name ??
                (r.assigneeId
                  ? "Chưa có tên nhân viên"
                  : "Chưa phân công")}
            </p>
            <p className="ops-muted">
              {time(r.requestedAt)} —{" "}
              {r.completedAt ? time(r.completedAt) : "Chưa hoàn tất"}
            </p>
          </article>
        ))}
      </details>
      <details>
        <summary>
          Các lượt kiểm tra cùng chu kỳ ({detail.cycleInspections.length})
        </summary>
        <p className="ops-muted">
          Kiểm tra lại: {detail.inspection.reinspectionCount ?? "Chưa xác minh"}{" "}
          · Lượt không đạt:{" "}
          {detail.inspection.cycleFailedInspectionCount ?? "Chưa xác minh"}
        </p>
        {detail.cycleInspections.map((row) => (
          <article key={row.id}>
            <strong>{row.number}</strong>
            <p>
              Phòng {row.roomNumber} · {time(row.at)} ·{" "}
              {label(row.result ?? row.status)}
            </p>
            <button
              disabled={row.id === detail.inspection.id}
              onClick={() => open(row.id)}
            >
              {row.id === detail.inspection.id ? "Đang xem" : "Xem chi tiết"}
            </button>
          </article>
        ))}
      </details>
      <details>
        <summary>Tệp bằng chứng ({detail.evidence.length})</summary>
        {!detail.evidence.length && <p>Chưa có tệp bằng chứng.</p>}
        {detail.evidence.map((e) => (
          <article key={e.id}>
            <strong>{e.name}</strong>
            <p className="ops-muted">
              {e.bytes === null
                ? "Chưa có kích thước"
                : `${Math.ceil(e.bytes / 1024)} KB`}{" "}
              · {time(e.uploadedAt)}
            </p>
            <button
              disabled={downloadBusy}
              onClick={() => void saveEvidence(e)}
            >
              Tải bằng chứng {referenceLabel(e, "id")}
            </button>
          </article>
        ))}
      </details>
      <p className="ops-muted">
        Thời gian có múi giờ được đổi sang giờ Việt Nam. Bản ghi chưa có múi giờ
        giữ nguyên giờ đã lưu.
      </p>
    </>
  );
}
