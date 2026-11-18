import { ActionDialog } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Line = {
  stocktakeLineId: number;
  itemId: number;
  itemName: string;
  uomName: string;
  systemQty: string;
  currentQty: string;
  resultingQty: string | null;
  countedQty: string | null;
  varianceQty: string | null;
  previewVariance: string | null;
  discrepancyReason: string | null;
  countedBy: number | null;
  countedAt: string | null;
};
type Detail = {
  stocktakeId: number;
  stocktakeNo: string;
  status: string;
  warehouseId: number;
  snapshotAt: string;
  rowVersion: string;
  note: string | null;
  canPerform: boolean;
  canDecide: boolean;
  adjustmentDocumentId: number | null;
  lines: Line[];
};
type Catalogue = {
  warehouses: {
    id: number;
    name: string;
    isActive: boolean;
    canPerform: boolean;
  }[];
  rows: {
    stocktakeId: number;
    stocktakeNo: string;
    warehouseId: number;
    status: string;
    snapshotAt: string;
  }[];
  count: number;
  pageSize: number;
};
type Count = {
  stocktakeLineId: number;
  countedQty: string;
  discrepancyReason: string;
};

export function StocktakePage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const [page, setPage] = useState(1),
    [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    key: string;
    value: Catalogue | Detail;
  }>();
  const key = id || `Danh sách:${page}`;
  const data =
    loaded?.owner === owner && loaded.key === key ? loaded.value : null;
  const detail = id && data ? (data as Detail) : null,
    catalogue = !id && data ? (data as Catalogue) : null;
  const [counts, setCounts] = useState<Count[]>([]),
    [warehouse, setWarehouse] = useState("");
  const [note, setNote] = useState(""),
    [reason, setReason] = useState("");
  const [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false),
    [uncertain, setUncertain] = useState(false);
  const writer = useRef<AbortController | null>(null),
    loadedVersion = useRef("");
  useEffect(() => {
    setWarehouse("");
    setNote("");
    setReason("");
    setCounts([]);
    loadedVersion.current = "";
  }, [owner, id]);
  useEffect(() => {
    setLoaded(undefined);
    if (
      !owner ||
      (id && (!/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id))))
    )
      return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(
          id
            ? `/api/stocktake-workspace/${id}`
            : `/api/stocktake-workspace?page=${page}`,
          {
            credentials: "same-origin",
            cache: "no-store",
            redirect: "error",
            signal: abort.signal,
          },
        );
        if (response.status === 401) actions.invalidateSession(owner);
        const value = await response.json();
        if (!response.ok)
          throw new Error(
            value.detail ?? `Kiểm kê chưa khả dụng (${response.status}).`,
          );
        if (abort.signal.aborted || current.current !== owner) return;
        if (id && loadedVersion.current !== value.rowVersion) {
          setCounts(
            value.lines.map((l: Line) => ({
              stocktakeLineId: l.stocktakeLineId,
              countedQty: l.countedQty ?? "",
              discrepancyReason: l.discrepancyReason ?? "",
            })),
          );
          loadedVersion.current = value.rowVersion;
        }
        setLoaded({ owner, key, value });
      } catch (e) {
        if (!abort.signal.aborted)
          setMessage(e instanceof Error ? e.message : "Kiểm kê chưa khả dụng.");
      }
    })();
    const refresh = () => {
      if (!writer.current) {
        setLoaded(undefined);
        setRevision((v) => v + 1);
      }
    };
    const timer = setInterval(refresh, 30000);
    window.addEventListener("focus", refresh);
    return () => {
      abort.abort();
      clearInterval(timer);
      window.removeEventListener("focus", refresh);
    };
  }, [owner, id, page, key, revision, actions]);
  useEffect(() => () => writer.current?.abort(), [owner, id]);
  const dirty =
    !!detail &&
    counts.some((c) => {
      const saved = detail.lines.find(
        (l) => l.stocktakeLineId === c.stocktakeLineId,
      );
      return (
        c.countedQty !== (saved?.countedQty ?? "") ||
        c.discrepancyReason !== (saved?.discrepancyReason ?? "")
      );
    });
  const missing = detail?.lines.filter((l) => l.countedQty == null) ?? [];

  async function write(
    action:
      | "start"
      | "counts"
      | "submit"
      | "cancel"
      | "reconcile"
      | "approve-adjustment"
      | "return",
  ) {
    if (!owner || !data || busy || uncertain || writer.current) return;
    if (
      action === "start" &&
      !catalogue?.warehouses.some(
        (w) => String(w.id) === warehouse && w.isActive && w.canPerform,
      )
    ) {
      setMessage("Chọn kho đang hoạt động được phép truy cập.");
      return;
    }
    const decision = action === "approve-adjustment" || action === "return";
    if (
      action !== "start" &&
      !(decision ? detail?.canDecide : detail?.canPerform)
    )
      return;
    if (
      decision &&
      (detail?.status !== "PENDING_ADJUSTMENT" ||
        reason.trim().length < (action === "return" ? 10 : 3))
    ) {
      setMessage("Nhập ghi chú duyệt hoặc lý do trả lại ít nhất 10 ký tự.");
      return;
    }
    if (
      action === "counts" &&
      counts.some(
        (c) => c.countedQty !== "" && !/^\d+(\.\d{1,3})?$/.test(c.countedQty),
      )
    ) {
      setMessage("Số kiểm đếm phải không âm và có tối đa 3 chữ số thập phân.");
      return;
    }
    if (action === "submit" && (dirty || missing.length)) {
      setMessage(
        "Lưu số kiểm đếm trước và nhập số lượng, kể cả 0, cho từng vật tư.",
      );
      return;
    }
    if (
      action === "reconcile" &&
      (dirty ||
        missing.length ||
        detail?.lines.some(
          (l) =>
            l.previewVariance != null &&
            /[1-9]/.test(l.previewVariance) &&
            (l.discrepancyReason?.trim().length ?? 0) < 3,
        ))
    ) {
      setMessage(
        "Lưu số kiểm đếm và nguyên nhân cho mọi chênh lệch khác 0 trước khi đối soát.",
      );
      return;
    }
    if (action === "cancel" && reason.trim().length < 10) {
      setMessage("Cần lý do hủy ít nhất 10 ký tự.");
      return;
    }
    const actionLabel = {
      start: "bắt đầu",
      counts: "lưu số kiểm đếm",
      submit: "nộp kết quả",
      cancel: "hủy",
      reconcile: "đối soát",
      "approve-adjustment": "duyệt điều chỉnh",
      return: "trả lại",
    }[action];
    if (
      (action === "submit" ||
        action === "cancel" ||
        action === "reconcile" ||
        decision) &&
      !window.confirm(`Xác nhận ${actionLabel} cho lần kiểm kê này?`)
    )
      return;
    const abort = new AbortController();
    writer.current = abort;
    setBusy(true);
    let sent = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      const csrf = await bootstrap.json();
      if (
        !bootstrap.ok ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        !csrf.csrfToken
      )
        throw new Error("Chưa có bảo vệ phiên (CSRF).");
      if (abort.signal.aborted || current.current !== owner) return;
      const body =
        action === "start"
          ? { warehouseId: Number(warehouse), note }
          : {
              expectedRowVersion: detail!.rowVersion,
              reason,
              ...(action === "counts"
                ? {
                    lines: counts.map((c) => ({
                      ...c,
                      countedQty: c.countedQty === "" ? null : c.countedQty,
                    })),
                  }
                : {}),
            };
      sent = true;
      const response = await fetch(
        action === "start"
          ? "/api/inventory/stocktakes"
          : `/api/inventory/stocktakes/${id}/${action}`,
        {
          method: action === "counts" ? "PUT" : "POST",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
          },
          body: JSON.stringify(body),
        },
      );
      if (response.status === 401) actions.invalidateSession(owner);
      const value = await response.json();
      if (!response.ok) {
        if (response.status < 500) sent = false;
        if ([401, 403].includes(response.status)) setLoaded(undefined);
        throw new Error(
          value.detail ?? `Thao tác thất bại (${response.status}).`,
        );
      }
      if (!abort.signal.aborted && current.current === owner) {
        setMessage(
          action === "reconcile"
            ? value.status === "RECONCILED"
              ? "MSG08: Không có chênh lệch; đã đối soát không cần điều chỉnh."
              : "MSG09: Đã gửi đối soát để quản lý vận hành xét duyệt."
            : "MSG08: Đã lưu kiểm kê.",
        );
        setLoaded(undefined);
        setRevision((v) => v + 1);
        if (action === "start")
          navigate(`/inventory/stocktakes/${value.stocktakeId}`);
      }
    } catch (e) {
      if (sent) setUncertain(true);
      if (!abort.signal.aborted)
        setMessage(
          `${e instanceof Error ? e.message : "Thao tác thất bại."}${sent ? "Chưa xác định kết quả. Mở lại và kiểm tra trước khi ghi tiếp." : ""}`,
        );
    } finally {
      if (writer.current === abort) {
        writer.current = null;
        setBusy(false);
      }
    }
  }
  return (
    <main>
      <h1>Kiểm kê</h1>
      <Link to="/inventory/adjustments">Điều chỉnh tồn kho</Link> |{" "}
      <Link to="/inventory/stocktakes">Danh sách kiểm kê</Link> |{" "}
      <Link to="/inventory/stock">Tồn kho hiện tại</Link>
      {message && <p role="status">{message}</p>}
      <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
        Tải lại
      </button>
      {catalogue && (
        <>
          <table>
            <thead>
              <tr>
                <th>Kiểm kê</th>
                <th>Kho</th>
                <th>Trạng thái</th>
                <th>Mốc chốt (UTC)</th>
              </tr>
            </thead>
            <tbody>
              {catalogue.rows.map((s) => (
                <tr key={s.stocktakeId}>
                  <td>
                    <Link to={`/inventory/stocktakes/${s.stocktakeId}`}>
                      {s.stocktakeNo}
                    </Link>
                  </td>
                  <td>
                    {
                      catalogue.warehouses.find((w) => w.id === s.warehouseId)
                        ?.name
                    }
                  </td>
                  <td>{displayCode(s.status)}</td>
                  <td>{s.snapshotAt}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p>
            {catalogue.count} phiếu kiểm kê được phép xem; trang {page}
          </p>
          <button
            disabled={busy || page === 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Trước
          </button>
          <button
            disabled={busy || page * catalogue.pageSize >= catalogue.count}
            onClick={() => setPage((p) => p + 1)}
          >
            Tiếp
          </button>
          <ActionDialog title="Bắt đầu kiểm kê" busy={busy} error={message}>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                void write("start");
              }}
            >
              <fieldset disabled={busy || uncertain}>
                <legend>Bắt đầu kiểm kê</legend>
                <label>
                  Kho
                  <select
                    required
                    value={warehouse}
                    onChange={(e) => setWarehouse(e.target.value)}
                  >
                    <option value="">Chọn kho</option>
                    {catalogue.warehouses
                      .filter((w) => w.isActive && w.canPerform)
                      .map((w) => (
                        <option key={w.id} value={w.id}>
                          {w.name}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  Ghi chú
                  <textarea
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                  />
                </label>
                <button data-dialog-action>Bắt đầu kiểm kê</button>
              </fieldset>
            </form>
          </ActionDialog>
        </>
      )}
      {detail && (
        <>
          <h2>
            {detail.stocktakeNo}: {displayCode(detail.status)}
          </h2>
          <p>
            Mốc chốt (UTC): {detail.snapshotAt}. Quantities use each material's
            base unit.
          </p>
          {detail.note && <p>{detail.note}</p>}
          {missing.length > 0 && (
            <p role="status">
              Chưa kiểm đếm: {missing.map((l) => l.itemName).join(", ")}. Enter
              zero for an empty count.
            </p>
          )}
          <fieldset
            disabled={
              busy ||
              uncertain ||
              !detail.canPerform ||
              !["IN_PROGRESS", "COUNTED"].includes(detail.status)
            }
          >
            <table>
              <thead>
                <tr>
                  <th>Vật tư</th>
                  <th>Đơn vị cơ sở</th>
                  <th>Số lượng tại mốc chốt</th>
                  <th>Đã kiểm đếm</th>
                  <th>Số lượng hiện tại</th>
                  <th>Kết quả sau điều chỉnh</th>
                  <th>Chênh lệch đã lưu</th>
                  <th>Nguyên nhân</th>
                </tr>
              </thead>
              <tbody>
                {detail.lines.map((l) => {
                  const entry = counts.find(
                    (c) => c.stocktakeLineId === l.stocktakeLineId,
                  );
                  return (
                    <tr key={l.stocktakeLineId}>
                      <td>{l.itemName}</td>
                      <td>{l.uomName}</td>
                      <td>{l.systemQty}</td>
                      <td>
                        <input
                          aria-label={`Số lượng ${l.itemName}`}
                          inputMode="decimal"
                          pattern="[0-9]+([.][0-9]{1,3})?"
                          value={entry?.countedQty ?? ""}
                          onChange={(e) =>
                            setCounts((rows) =>
                              rows.map((c) =>
                                c.stocktakeLineId === l.stocktakeLineId
                                  ? { ...c, countedQty: e.target.value }
                                  : c,
                              ),
                            )
                          }
                        />
                      </td>
                      <td>{l.currentQty}</td>
                      <td>{l.resultingQty ?? "N/A"}</td>
                      <td>{l.previewVariance ?? "Chưa kiểm đếm"}</td>
                      <td>
                        <input
                          aria-label={`Nguyên nhân ${l.itemName}`}
                          value={entry?.discrepancyReason ?? ""}
                          onChange={(e) =>
                            setCounts((rows) =>
                              rows.map((c) =>
                                c.stocktakeLineId === l.stocktakeLineId
                                  ? { ...c, discrepancyReason: e.target.value }
                                  : c,
                              ),
                            )
                          }
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            <button
              disabled={!dirty || !counts.length}
              onClick={() => void write("counts")}
            >
              Lưu số liệu
            </button>
            {detail.status === "COUNTED" && (
              <button
                disabled={dirty || missing.length > 0}
                onClick={() => void write("reconcile")}
              >
                Gửi đối soát
              </button>
            )}
            {detail.status === "IN_PROGRESS" && (
              <>
                <button
                  disabled={dirty || missing.length > 0}
                  onClick={() => void write("submit")}
                >
                  Gửi số lượng
                </button>
                <label>
                  Lý do hủy
                  <input
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <button onClick={() => void write("cancel")}>
                  Hủy kiểm kê
                </button>
              </>
            )}
          </fieldset>
          {detail.adjustmentDocumentId && (
            <p>Điều chỉnh đã ghi sổ: {detail.adjustmentDocumentId}</p>
          )}
          {detail.canDecide && detail.status === "PENDING_ADJUSTMENT" && (
            <fieldset disabled={busy || uncertain}>
              <legend>Xem lại điều chỉnh</legend>
              <p>
                Số dư kết quả gồm biến động sau mốc chốt kiểm đếm; máy chủ kiểm
                tra lại tồn hiện tại khi ghi sổ.
              </p>
              <label>
                Ghi chú duyệt / lý do trả lại
                <textarea
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
              <button onClick={() => void write("approve-adjustment")}>
                Phê duyệt điều chỉnh
              </button>
              <button onClick={() => void write("return")}>
                Trả lại để kiểm đếm lại
              </button>
            </fieldset>
          )}
        </>
      )}
    </main>
  );
}
