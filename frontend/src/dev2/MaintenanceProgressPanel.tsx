import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

type Unit = { uomId: number; conversionFactor: number };
type Line = {
  stockDocumentLineId: number;
  itemId: number;
  documentNo: string;
  lineNo: number;
  baseUomId: number;
  allowedUoms: Unit[];
  remainingBaseQty: number;
};
type Selection = { line: Line; uomId: number; quantity: string };
const positiveId = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function validLine(v: unknown): v is Line {
  return (
    isObject(v) &&
    positiveId(v.stockDocumentLineId) &&
    positiveId(v.itemId) &&
    positiveId(v.lineNo) &&
    positiveId(v.baseUomId) &&
    typeof v.documentNo === "string" &&
    Array.isArray(v.allowedUoms) &&
    v.allowedUoms.length > 0 &&
    v.allowedUoms.every(
      (u) =>
        isObject(u) &&
        positiveId(u.uomId) &&
        typeof u.conversionFactor === "number" &&
        Number.isFinite(u.conversionFactor) &&
        u.conversionFactor > 0,
    ) &&
    typeof v.remainingBaseQty === "number" &&
    Number.isFinite(v.remainingBaseQty) &&
    v.remainingBaseQty >= 0
  );
}
function validSelection(s: Selection) {
  const factor = s.line.allowedUoms.find(
    (u) => u.uomId === s.uomId,
  )?.conversionFactor;
  const quantity = Number(s.quantity);
  return (
    /^\d+(?:\.\d{1,3})?$/.test(s.quantity) &&
    Number.isFinite(quantity) &&
    quantity > 0 &&
    factor !== undefined &&
    quantity * factor <= s.line.remainingBaseQty + 1e-9
  );
}

export function MaintenanceProgressPanel({
  data,
  allowMaterials,
  onCommitted,
}: {
  data: Values;
  allowMaterials: boolean;
  onCommitted?: (message: string) => void;
}) {
  const { state } = useAuth(),
    session = state.session;
  const [bound] = useState(session),
    current = useRef(session);
  current.current = session;
  const [percent, setPercent] = useState("");
  const [note, setNote] = useState(""),
    [selected, setSelected] = useState<Selection[]>([]);
  const [lines, setLines] = useState<Line[]>([]),
    [page, setPage] = useState(0),
    [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false),
    [materialMessage, setMaterialMessage] = useState("");
  const [busy, setBusy] = useState(false),
    [attempted, setAttempted] = useState(false),
    [review, setReview] = useState(false);
  const [message, setMessage] = useState("");
  const reader = useRef<AbortController | null>(null),
    writer = useRef<AbortController | null>(null),
    sending = useRef(false);
  useEffect(
    () => () => {
      reader.current?.abort();
      writer.current?.abort();
    },
    [session, data],
  );
  const id = data.maintenanceWorkId,
    tag = data.executionETag,
    actor = Number(session?.user.id);
  if (
    !session ||
    session !== bound ||
    data.canLog !== true ||
    !["IN_PROGRESS", "ON_HOLD"].includes(String(data.status)) ||
    !positiveId(id) ||
    !positiveId(actor) ||
    typeof tag !== "string" ||
    !/^"[0-9A-F]{16}"$/.test(tag)
  )
    return null;
  const valid =
    (data.progressPercentAvailable !== true ||
      (/^\d{1,3}$/.test(percent) && Number(percent) <= 100)) &&
    note.trim().length > 0 &&
    selected.every(validSelection) &&
    (selected.length === 0 || allowMaterials);
  async function load(next: number) {
    if (
      !allowMaterials ||
      sending.current ||
      attempted ||
      current.current !== session
    )
      return;
    reader.current?.abort();
    const abort = new AbortController();
    reader.current = abort;
    setLoading(true);
    setMaterialMessage("");
    try {
      const response = await fetch(
        `/api/maintenance-works/${id}/material-availability?page=${next}&pageSize=20`,
        {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        },
      );
      if (!response.ok)
        throw Error(
          "Vật tư khả dụng chưa tải được. Cần quyền kho và yêu cầu vật tư hiện tại.",
        );
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.page !== next ||
        result.pageSize !== 20 ||
        !Number.isSafeInteger(result.total) ||
        typeof result.total !== "number" ||
        result.total < 0 ||
        !Array.isArray(result.items) ||
        result.items.length > 20 ||
        !result.items.every(validLine) ||
        new Set(result.items.map((x) => x.stockDocumentLineId)).size !==
          result.items.length
      )
        throw Error("Không xác minh được vật tư khả dụng.");
      if (!abort.signal.aborted && current.current === session) {
        setLines(result.items);
        setPage(next);
        setTotal(result.total);
        const refreshed = result.items as Line[];
        setSelected((items) =>
          items.map((item) => {
            const line = refreshed.find(
              (row) =>
                row.stockDocumentLineId === item.line.stockDocumentLineId,
            );
            return line ? { ...item, line } : item;
          }),
        );
      }
    } catch (error) {
      if (!abort.signal.aborted && current.current === session) {
        setLines([]);
        setPage(0);
        setTotal(0);
        setMaterialMessage(
          error instanceof Error
            ? error.message
            : "Vật tư khả dụng chưa khả dụng.",
        );
      }
    } finally {
      if (!abort.signal.aborted) setLoading(false);
    }
  }
  async function save() {
    if (
      !valid ||
      loading ||
      sending.current ||
      attempted ||
      current.current !== session
    )
      return;
    sending.current = true;
    setBusy(true);
    setAttempted(true);
    setMessage("");
    const abort = new AbortController();
    writer.current = abort;
    let dispatched = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      if (!bootstrap.ok)
        throw Error(
          "Không xác minh được phiên đăng nhập. Tải lại trước khi thử lại.",
        );
      const csrf: unknown = await bootstrap.json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        abort.signal.aborted ||
        current.current !== session
      )
        throw Error("Phiên đăng nhập đã thay đổi. Tải lại trước khi thử lại.");
      dispatched = true;
      const response = await fetch(`/api/maintenance-works/${id}/logs`, {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
          "If-Match": tag as string,
        },
        body: JSON.stringify({
          actorUserId: actor,
          ...(data.progressPercentAvailable === true
            ? { progressPercent: Number(percent) }
            : {}),
          workNote: note.trim(),
          spentMinutes: 0,
          materialUsages: selected.map((s) => ({
            stockDocumentLineId: s.line.stockDocumentLineId,
            uomId: s.uomId,
            usedQty: Number(s.quantity),
          })),
        }),
      });
      if (!response.ok) {
        if ([400, 409, 422].includes(response.status)) {
          // The server rejected the command. Permit corrections, but never retry
          // an ambiguous transport/server failure or reuse a stale ETag.
          if (!abort.signal.aborted && current.current === session)
            setAttempted(false);
          throw Error(
            "Tiến độ bị từ chối. Kiểm tra ghi chú, đơn vị và số lượng. Tải lại vật tư đã cấp, sửa dữ liệu và xem lại trước khi lưu.",
          );
        }
        throw Error(
          response.status === 412
            ? "Công việc đã đổi. Tải lại trước khi ghi tiến độ tiếp."
            : "Chưa xác nhận tiến độ. Tải lại và kiểm tra quyền, chứng từ nguồn và số lượng còn lại.",
        );
      }
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.maintenanceWorkId !== id ||
        result.loggedBy !== actor ||
        !positiveId(result.logId) ||
        (data.progressPercentAvailable === true &&
          result.progressPercent !== Number(percent)) ||
        result.workNote !== note.trim() ||
        result.status !== data.status ||
        typeof result.executionETag !== "string" ||
        !/^"[0-9A-F]{16}"$/.test(result.executionETag) ||
        result.executionETag === tag ||
        !Array.isArray(result.materialUsages) ||
        result.materialUsages.length !== selected.length ||
        !selected.every(
          (s) =>
            (result.materialUsages as unknown[]).filter(
              (u) =>
                isObject(u) &&
                u.stockDocumentLineId === s.line.stockDocumentLineId &&
                u.uomId === s.uomId &&
                u.usedQty === Number(s.quantity) &&
                u.maintenanceWorkId === id &&
                u.maintenanceWorkLogId === result.logId,
            ).length === 1,
        )
      )
        throw Error(
          "Chưa xác minh được phản hồi tiến độ. Tải lại hồ sơ hiện tại.",
        );
      if (!abort.signal.aborted && current.current === session) {
        const notice =
          "Đã ghi tiến độ. Đạt 100% không tự nộp hoặc đóng công việc.";
        setMessage(notice);
        announceWorkflowSuccess(notice, "dev2/MaintenanceProgressPanel.tsx");
        onCommitted?.(notice);
      }
    } catch (error) {
      if (!abort.signal.aborted && current.current === session) {
        if (!dispatched) setAttempted(false);
        setMessage(
          error instanceof Error
            ? error.message
            : "Chưa xác nhận tiến độ. Vui lòng tải lại.",
        );
      }
    } finally {
      if (!abort.signal.aborted) {
        setBusy(false);
        setReview(false);
        sending.current = false;
      }
    }
  }
  return (
    <ActionDialog
      scope="dev2/MaintenanceProgressPanel.tsx"
      title="Tiến độ bảo trì"
      busy={busy}
    >
      <section aria-label="Tiến độ bảo trì">
        <h3>Ghi tiến độ và vật tư sử dụng</h3>
        <p>
          Thời gian thực tế tính từ các khoảng làm việc. Vật tư phải lấy từ dòng
          cấp phát đã ghi sổ và hợp lệ với sự cố này.
        </p>
        {message && <p role="status">{message}</p>}
        <fieldset disabled={busy || attempted || review}>
          {data.progressPercentAvailable === true && (
            <label>
              Tiến độ (%)
              <input
                type="number"
                min="0"
                max="100"
                step="1"
                value={percent}
                onChange={(e) => setPercent(e.target.value)}
              />
            </label>
          )}
          {data.progressPercentAvailable !== true && (
            <p>Chưa theo dõi được phần trăm. Bạn vẫn có thể ghi chú.</p>
          )}
          <p>Đạt 100% không tự nộp kết quả hoặc đóng công việc.</p>
          <label>
            Ghi chú tiến độ
            <textarea value={note} onChange={(e) => setNote(e.target.value)} />
          </label>
          <p>Ghi chẩn đoán và biện pháp đã thực hiện vào ghi chú tiến độ.</p>
          {allowMaterials ? (
            <>
              <button disabled={loading} onClick={() => void load(1)}>
                Tải vật tư đã cấp
              </button>
              {materialMessage && <p role="status">{materialMessage}</p>}
              {loading && <p>Đang tải dòng cấp phát hợp lệ…</p>}
              {page > 0 && (
                <>
                  <p>
                    {total} dòng hợp lệ. Trang {page}.
                  </p>
                  {lines.length === 0 && (
                    <p>Không có vật tư đã cấp hợp lệ trên trang này.</p>
                  )}
                  {lines.map((line) => (
                    <p key={line.stockDocumentLineId}>
                      {line.documentNo}, Dòng {line.lineNo}, Vật tư{" "}
                      {line.itemId}: {line.remainingBaseQty} còn lại (đơn vị cơ
                      sở {line.baseUomId}).
                      <button
                        disabled={
                          loading ||
                          line.remainingBaseQty <= 0 ||
                          selected.some(
                            (s) =>
                              s.line.stockDocumentLineId ===
                              line.stockDocumentLineId,
                          )
                        }
                        onClick={() =>
                          setSelected((items) => [
                            ...items,
                            {
                              line,
                              uomId: line.allowedUoms[0].uomId,
                              quantity: "",
                            },
                          ])
                        }
                      >
                        Thêm dòng {line.lineNo} từ {line.documentNo}
                      </button>
                    </p>
                  ))}
                  <button
                    disabled={loading || page <= 1}
                    onClick={() => void load(page - 1)}
                  >
                    Vật tư trước
                  </button>
                  <button
                    disabled={loading || page * 20 >= total}
                    onClick={() => void load(page + 1)}
                  >
                    Vật tư tiếp theo
                  </button>
                </>
              )}
            </>
          ) : (
            <p>Chưa chọn được vật tư. Bạn vẫn có thể ghi chú tiến độ.</p>
          )}
          {selected.map((s) => (
            <div key={s.line.stockDocumentLineId}>
              <p>
                {s.line.documentNo}, Dòng {s.line.lineNo}, Vật tư{" "}
                {s.line.itemId}
              </p>
              <label>
                Đơn vị của dòng {s.line.stockDocumentLineId}
                <select
                  aria-label={`Đơn vị của dòng ${s.line.stockDocumentLineId}`}
                  value={s.uomId}
                  onChange={(e) =>
                    setSelected((items) =>
                      items.map((x) =>
                        x === s ? { ...x, uomId: Number(e.target.value) } : x,
                      ),
                    )
                  }
                >
                  {s.line.allowedUoms.map((u) => (
                    <option key={u.uomId} value={u.uomId}>
                      Đơn vị {displayCode(u.uomId)} (
                      {displayCode(u.conversionFactor)} đơn vị cơ sở)
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Số lượng đã dùng của dòng {s.line.stockDocumentLineId}
                <input
                  inputMode="decimal"
                  value={s.quantity}
                  onChange={(e) =>
                    setSelected((items) =>
                      items.map((x) =>
                        x === s ? { ...x, quantity: e.target.value } : x,
                      ),
                    )
                  }
                />
              </label>
              {!validSelection(s) && (
                <p>
                  Nhập số lượng dương tối đa 3 chữ số thập phân, không vượt số
                  lượng cơ sở còn lại ({s.line.remainingBaseQty}).
                </p>
              )}
              <button
                onClick={() =>
                  setSelected((items) => items.filter((x) => x !== s))
                }
              >
                Xóa dòng {s.line.stockDocumentLineId}
              </button>
            </div>
          ))}
        </fieldset>
        {review ? (
          <>
            <p>
              Ghi nhận tiến độ này và {selected.length} dòng sử dụng vật tư?
            </p>
            <p>{note.trim()}</p>
            <button
              data-dialog-action
              disabled={busy}
              onClick={() => void save()}
            >
              Xác nhận tiến độ
            </button>
            <button disabled={busy} onClick={() => setReview(false)}>
              Hủy
            </button>
          </>
        ) : (
          <button
            disabled={!valid || busy || attempted || loading}
            onClick={() => setReview(true)}
          >
            Xem lại tiến độ
          </button>
        )}
      </section>
    </ActionDialog>
  );
}
