import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Receipt = {
  documentId: number;
  documentNo: string;
  status: string;
  rowVersion: string;
  lines: { lineId: number; itemId: number; uomId: number }[];
};
type Detail = {
  document: Receipt;
  source: { replenishmentId: number | null } | null;
  expectedQuantities: string[];
};
type Entry = {
  lineId: number;
  acceptedQty: string;
  rejectedQty: string;
  damagedQty: string;
  discrepancyReason: string;
};

export function ReceiptPostPanel({
  id,
  onSaved,
  items,
  units,
}: {
  id: number;
  onSaved: () => void;
  items: { id: number; name: string }[];
  units: { id: number; name: string }[];
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const current = useRef(owner);
  current.current = owner;
  const loadedVersion = useRef<{ owner: typeof owner; version: string } | null>(
    null,
  );
  const [data, setData] = useState<{
    owner: typeof owner;
    value: Detail;
  } | null>(null);
  const [lines, setLines] = useState<Entry[]>([]),
    [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false),
    [uncertain, setUncertain] = useState(false),
    [revision, setRevision] = useState(0);
  const writer = useRef<AbortController | null>(null);
  const receipt = data?.owner === owner ? data.value : null;
  useEffect(() => {
    setData(null);
    if (!owner) {
      setLines([]);
      return;
    }
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/receipt-workspace/${id}`, {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        });
        const value = await response.json();
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok)
          throw new Error(
            value.detail ?? `Phiếu nhập chưa khả dụng (${response.status}).`,
          );
        if (abort.signal.aborted || current.current !== owner) return;
        setData({ owner, value });
        if (
          loadedVersion.current?.owner !== owner ||
          loadedVersion.current?.version !== value.document.rowVersion
        )
          setLines(
            value.document.lines.map((l: { lineId: number }, i: number) => ({
              lineId: l.lineId,
              acceptedQty: value.expectedQuantities[i],
              rejectedQty: "0",
              damagedQty: "0",
              discrepancyReason: "",
            })),
          );
        loadedVersion.current = { owner, version: value.document.rowVersion };
        if (value.document.status === "POSTED")
          setMessage("MSG28: Phiếu nhập đã ghi sổ. Không nhập thêm tồn kho.");
      } catch (e) {
        if (!abort.signal.aborted)
          setMessage(
            e instanceof Error ? e.message : "Phiếu nhập chưa khả dụng.",
          );
      }
    })();
    // Revalidate current authority; preserve entered quantities only for the same source version.
    const refresh = () => {
      if (!writer.current) {
        setData(null);
        setRevision((v) => v + 1);
      }
    };
    window.addEventListener("focus", refresh);
    const timer = window.setInterval(refresh, 30000);
    return () => {
      abort.abort();
      clearInterval(timer);
      window.removeEventListener("focus", refresh);
    };
  }, [owner, id, revision, actions]);
  useEffect(() => () => writer.current?.abort(), [owner, id]);
  async function post() {
    if (
      !owner ||
      !receipt ||
      busy ||
      uncertain ||
      receipt.document.status !== "DRAFT" ||
      receipt.source?.replenishmentId
    )
      return;
    if (
      !window.confirm(
        "Ghi sổ số lượng đã chấp nhận và xem xét? Không thể sửa tồn kho đã ghi sổ.",
      )
    )
      return;
    const abort = new AbortController();
    writer.current = abort;
    setBusy(true);
    setMessage("");
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
      const response = await fetch(`/api/inventory/receipts/${id}/post`, {
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
          expectedRowVersion: receipt.document.rowVersion,
          lines,
        }),
      });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) {
        if (response.status < 500) sent = false;
        if ([401, 403].includes(response.status)) setData(null);
        throw new Error(
          (await response.json()).detail ??
            `Ghi sổ thất bại (${response.status}).`,
        );
      }
      if (current.current === owner && !abort.signal.aborted) {
        (announceWorkflowSuccess(
          "MSG08: Đã ghi sổ phiếu nhập.",
          "inventory/ReceiptPostPanel.tsx",
        ),
          setMessage("MSG08: Đã ghi sổ phiếu nhập."));
        setRevision((v) => v + 1);
        onSaved();
      }
    } catch (e) {
      if (sent) setUncertain(true);
      if (!abort.signal.aborted)
        setMessage(
          `${e instanceof Error ? e.message : "Ghi sổ thất bại."}${sent ? "Chưa xác định kết quả. Tải lại phiếu nhập trước mọi thao tác tiếp theo." : ""}`,
        );
    } finally {
      if (writer.current === abort) {
        writer.current = null;
        setBusy(false);
      }
    }
  }
  return (
    <section aria-label="Xác minh và ghi sổ phiếu nhập">
      <h2>Xác minh và ghi sổ</h2>
      {message && <p role="status">{message}</p>}
      <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
        Tải lại phiếu nhập
      </button>
      {receipt && (
        <>
          <p>
            {receipt.document.documentNo}:{" "}
            {displayCode(receipt.document.status)}
          </p>
          {receipt.source?.replenishmentId ? (
            <p>
              <Link
                to={`/operations/replenishments/${receipt.source.replenishmentId}`}
              >
                Xác minh và ghi sổ qua yêu cầu bổ sung liên quan
              </Link>{" "}
              — tồn kho và số lượng nhận phải được ghi nhận đồng thời.
            </p>
          ) : (
            receipt.document.status === "DRAFT" && (
              <ActionDialog
                scope="inventory/ReceiptPostPanel.tsx"
                title="Ghi sổ phiếu nhập"
                busy={busy}
                error={message}
              >
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    void post();
                  }}
                >
                  <fieldset disabled={busy || uncertain}>
                    <p>
                      Chỉ số lượng được chấp nhận mới tăng tồn kho; số bị từ
                      chối và hư hỏng không tăng tồn. Giải thích mọi chênh lệch
                      so với dự kiến.
                    </p>
                    {lines.map((line, index) => (
                      <fieldset key={line.lineId}>
                        <legend>
                          {items.find(
                            (i) =>
                              i.id === receipt.document.lines[index].itemId,
                          )?.name ??
                            `Vật tư ${receipt.document.lines[index].itemId}`}
                          ,{" "}
                          {units.find(
                            (u) => u.id === receipt.document.lines[index].uomId,
                          )?.name ??
                            `Đơn vị ${receipt.document.lines[index].uomId}`}
                          ; dự kiến {receipt.expectedQuantities[index]}
                        </legend>
                        {(
                          ["acceptedQty", "rejectedQty", "damagedQty"] as const
                        ).map((field) => (
                          <label key={field}>
                            {field}
                            <input
                              required
                              inputMode="decimal"
                              pattern="[0-9]+([.][0-9]{1,3})?"
                              value={line[field]}
                              onChange={(e) =>
                                setLines((v) =>
                                  v.map((x) =>
                                    x.lineId === line.lineId
                                      ? { ...x, [field]: e.target.value }
                                      : x,
                                  ),
                                )
                              }
                            />
                          </label>
                        ))}
                        <label>
                          Lý do chênh lệch{" "}
                          <input
                            value={line.discrepancyReason}
                            onChange={(e) =>
                              setLines((v) =>
                                v.map((x) =>
                                  x.lineId === line.lineId
                                    ? {
                                        ...x,
                                        discrepancyReason: e.target.value,
                                      }
                                    : x,
                                ),
                              )
                            }
                          />
                        </label>
                      </fieldset>
                    ))}
                    <button data-dialog-action>Xác nhận và ghi sổ</button>
                  </fieldset>
                </form>
              </ActionDialog>
            )
          )}
        </>
      )}
    </section>
  );
}
