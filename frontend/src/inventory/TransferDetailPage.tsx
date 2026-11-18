import { ActionDialog } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Quantity = {
  lineId: number;
  itemName: string;
  uomName: string;
  planned: string;
  dispatched: string;
  received: string | null;
  damaged: string | null;
  missing: string | null;
  outstanding: string | null;
};
type Detail = {
  document: {
    documentId: number;
    documentNo: string;
    status: string;
    rowVersion: string;
    sourceWarehouseId: number;
    destinationWarehouseId: number;
  };
  quantities: Quantity[];
};
type Entry = {
  transferLineId: number;
  receivedQty: string;
  damagedQty: string;
  missingQty: string;
  note: string;
};
const positive = (value: string) => /[1-9]/.test(value);

export function TransferDetailPage() {
  const { id = "" } = useParams();
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    id: string;
    data: Detail;
  }>();
  const data = loaded?.owner === owner && loaded.id === id ? loaded.data : null;
  const [entries, setEntries] = useState<Entry[]>([]),
    [reason, setReason] = useState("");
  const [message, setMessage] = useState(""),
    [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false),
    [uncertain, setUncertain] = useState(false);
  const writer = useRef<AbortController | null>(null);
  const version = useRef<string>("");
  useEffect(() => {
    setLoaded(undefined);
    if (!owner || !/^[1-9]\d*$/.test(id) || !Number.isSafeInteger(Number(id)))
      return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/transfer-workspace/${id}`, {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        });
        if (response.status === 401) actions.invalidateSession(owner);
        const value = await response.json();
        if (!response.ok)
          throw new Error(
            value.detail ?? `Điều chuyển chưa khả dụng (${response.status}).`,
          );
        if (abort.signal.aborted || current.current !== owner) return;
        const key = `${owner}:${id}:${value.document.rowVersion}`;
        if (version.current !== key)
          setEntries(
            value.quantities.map((q: Quantity) => ({
              transferLineId: q.lineId,
              receivedQty: "0",
              damagedQty: "0",
              missingQty: "0",
              note: "",
            })),
          );
        version.current = key;
        setLoaded({ owner, id, data: value });
      } catch (e) {
        if (!abort.signal.aborted)
          setMessage(
            e instanceof Error ? e.message : "Điều chuyển chưa khả dụng.",
          );
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
  }, [owner, id, revision, actions]);
  useEffect(() => () => writer.current?.abort(), [owner, id]);

  async function write(action: "dispatch" | "receive" | "cancel") {
    if (!owner || !data || busy || uncertain || writer.current) return;
    const selected = entries.filter(
      (e) =>
        positive(e.receivedQty) ||
        positive(e.damagedQty) ||
        positive(e.missingQty),
    );
    if (
      action === "receive" &&
      (!selected.length ||
        selected.some(
          (e) =>
            [e.receivedQty, e.damagedQty, e.missingQty].some(
              (q) => !/^\d+(\.\d{1,3})?$/.test(q),
            ) ||
            ((positive(e.damagedQty) || positive(e.missingQty)) &&
              e.note.trim().length < 3),
        ))
    ) {
      setMessage("Nhập số lượng dương và lý do cho từng dòng hỏng / thiếu.");
      return;
    }
    if (action === "cancel" && reason.trim().length < 10) {
      setMessage("Lý do hủy cần ít nhất 10 ký tự.");
      return;
    }
    if (
      !window.confirm(
        `Xác nhận ${{ dispatch: "gửi hàng", receive: "nhận hàng", cancel: "hủy" }[action]} cho ${data.document.documentNo}?`,
      )
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
      sent = true;
      const path =
        action === "cancel"
          ? `/api/inventory/documents/${id}/cancel`
          : `/api/inventory/transfers/${id}/${action === "receive" ? "receipts" : "dispatch"}`;
      const response = await fetch(path, {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
        },
        body: JSON.stringify({
          expectedRowVersion: data.document.rowVersion,
          reason,
          lines: selected,
        }),
      });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) {
        if (response.status < 500) sent = false;
        if ([401, 403].includes(response.status)) setLoaded(undefined);
        throw new Error(
          (await response.json()).detail ??
            `Thao tác thất bại (${response.status}).`,
        );
      }
      if (!abort.signal.aborted && current.current === owner) {
        setMessage(
          action === "dispatch"
            ? "MSG09: Đã gửi điều chuyển."
            : action === "receive"
              ? "MSG08/MSG18: Đã ghi nhận lô hàng; tải lại để xem số lượng còn lại."
              : "Đã hủy bản nháp điều chuyển.",
        );
        setLoaded(undefined);
        setRevision((v) => v + 1);
      }
    } catch (e) {
      if (sent) setUncertain(true);
      if (!abort.signal.aborted)
        setMessage(
          `${e instanceof Error ? e.message : "Thao tác thất bại."}${sent ? "Chưa xác định kết quả. Mở lại và kiểm tra điều chuyển trước khi thao tác tiếp." : ""}`,
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
      <h1>Điều chuyển kho</h1>
      {message && <p role="status">{message}</p>}
      <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
        Tải lại điều chuyển
      </button>
      {data && (
        <>
          <p>
            {data.document.documentNo}: {displayCode(data.document.status)}; Kho{" "}
            {data.document.sourceWarehouseId} →{" "}
            {data.document.destinationWarehouseId}.
          </p>
          <table>
            <thead>
              <tr>
                <th>Vật tư / đơn vị</th>
                <th>Theo kế hoạch</th>
                <th>Đã gửi xử lý</th>
                <th>Đã nhận</th>
                <th>Hư hỏng</th>
                <th>Thiếu</th>
                <th>Đang vận chuyển</th>
              </tr>
            </thead>
            <tbody>
              {data.quantities.map((q) => (
                <tr key={q.lineId}>
                  <td>
                    {q.itemName} / {q.uomName}
                  </td>
                  <td>{q.planned}</td>
                  <td>{q.dispatched}</td>
                  <td>{q.received ?? "N/A"}</td>
                  <td>{q.damaged ?? "N/A"}</td>
                  <td>{q.missing ?? "N/A"}</td>
                  <td>{q.outstanding ?? "N/A"}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <fieldset disabled={busy || uncertain}>
            {data.document.status === "DRAFT" && (
              <>
                <button onClick={() => void write("dispatch")}>
                  Gửi xử lý
                </button>
                <label>
                  Lý do hủy{" "}
                  <input
                    minLength={10}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <button onClick={() => void write("cancel")}>
                  Hủy bản nháp
                </button>
              </>
            )}
            {["IN_TRANSIT", "PARTIALLY_RECEIVED"].includes(
              data.document.status,
            ) && (
              <ActionDialog
                title="Tiếp nhận điều chuyển"
                busy={busy}
                error={message}
              >
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    void write("receive");
                  }}
                >
                  <p>
                    Chỉ nhập lô này. Số lượng nhận dùng được tăng tồn kho; hỏng
                    và thiếu được ghi nhận là hao hụt.
                  </p>
                  {entries.map((line) => (
                    <fieldset key={line.transferLineId}>
                      <legend>
                        {
                          data.quantities.find(
                            (q) => q.lineId === line.transferLineId,
                          )?.itemName
                        }
                      </legend>
                      {(
                        ["receivedQty", "damagedQty", "missingQty"] as const
                      ).map((field) => (
                        <label key={field}>
                          {field}
                          <input
                            required
                            inputMode="decimal"
                            pattern="[0-9]+([.][0-9]{1,3})?"
                            value={line[field]}
                            onChange={(e) =>
                              setEntries((rows) =>
                                rows.map((row) =>
                                  row.transferLineId === line.transferLineId
                                    ? { ...row, [field]: e.target.value }
                                    : row,
                                ),
                              )
                            }
                          />
                        </label>
                      ))}
                      <label>
                        Lý do hỏng / thiếu{" "}
                        <input
                          value={line.note}
                          onChange={(e) =>
                            setEntries((rows) =>
                              rows.map((row) =>
                                row.transferLineId === line.transferLineId
                                  ? { ...row, note: e.target.value }
                                  : row,
                              ),
                            )
                          }
                        />
                      </label>
                    </fieldset>
                  ))}
                  <button data-dialog-action>Xác nhận lô hàng đã nhận</button>
                </form>
              </ActionDialog>
            )}
          </fieldset>
        </>
      )}
    </main>
  );
}
