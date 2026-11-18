import { ActionDialog } from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";

type Receiver = {
  userId: number;
  name: string;
  departmentId: number;
  departmentName: string;
};
type Destination = {
  branchId: number;
  branchName: string;
  rooms: { id: number; name: string }[];
  areas: { id: number; name: string }[];
  receivers: Receiver[];
};
type Pending = {
  handoverId: number;
  handoverNo: string;
  conditionNote: string;
  reason: string;
  confirmationNote: string | null;
  destinationBranchId: number;
  targetRoomId: number | null;
  targetAreaId: number | null;
  toDepartmentId: number;
  toUserId: number;
  canDecide: boolean;
  canCancel: boolean;
};
type View = {
  assetCode: string;
  assetName: string;
  branchId: number;
  version: string;
  hasOpenWork: boolean;
  currentRoomId: number | null;
  currentAreaId: number | null;
  canSubmit: boolean;
  hasPendingHandover: boolean;
  destinations: Destination[];
  pending: Pending[];
};

export function AssetCrossBranchPage() {
  const { id } = useParams();
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const identity = `${owner}:${id}`;
  const live = useRef(identity);
  live.current = identity;
  const write = useRef<AbortController | null>(null);
  const [view, setView] = useState<{ identity: string; data: View } | null>(
    null,
  );
  const [revision, setRevision] = useState(0),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [message, setMessage] = useState("");
  const [branch, setBranch] = useState(""),
    [target, setTarget] = useState(""),
    [receiver, setReceiver] = useState("");
  const [reason, setReason] = useState(""),
    [condition, setCondition] = useState(""),
    [confirmation, setConfirmation] = useState("");
  const [decisionReason, setDecisionReason] = useState(""),
    [decisionConfirmation, setDecisionConfirmation] = useState("");
  useEffect(() => {
    setView(null);
    setError("");
    if (
      !owner ||
      !id ||
      !/^[1-9]\d*$/.test(id) ||
      !Number.isSafeInteger(Number(id))
    )
      return;
    const abort = new AbortController();
    void fetch(`/api/asset-transfer/${id}/cross-branch`, {
      credentials: "same-origin",
      cache: "no-store",
      redirect: "error",
      signal: abort.signal,
    })
      .then(async (r) => {
        if (r.status === 401) actions.invalidateSession(owner);
        const body = await r.json();
        if (!r.ok)
          throw Error(
            body.detail ?? `Điều chuyển chưa khả dụng (${r.status}).`,
          );
        if (!abort.signal.aborted && live.current === identity)
          setView({ identity, data: body });
      })
      .catch((e) => {
        if (!abort.signal.aborted && live.current === identity)
          setError(String(e));
      });
    const refresh = window.setInterval(() => {
      if (!write.current) setRevision((v) => v + 1);
    }, 30000);
    return () => {
      abort.abort();
      window.clearInterval(refresh);
    };
  }, [owner, id, identity, actions, revision]);
  useEffect(() => {
    setBranch("");
    setTarget("");
    setReceiver("");
    setReason("");
    setCondition("");
    setConfirmation("");
    setDecisionReason("");
    setDecisionConfirmation("");
    setMessage("");
    setBusy(false);
    return () => write.current?.abort();
  }, [identity]);
  const data = owner && view?.identity === identity ? view.data : null;
  const destination = data?.destinations.find(
    (d) => String(d.branchId) === branch,
  );

  async function post(path: string, body: object) {
    if (!data || !owner || write.current) return;
    const abort = new AbortController();
    write.current = abort;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const csrf = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      if (csrf.status === 401) actions.invalidateSession(owner);
      const token = await csrf.json();
      if (
        !csrf.ok ||
        token.headerName !== "X-CSRF-TOKEN" ||
        typeof token.csrfToken !== "string" ||
        !token.csrfToken
      )
        throw Error("Chưa khởi tạo được bảo vệ phiên (CSRF).");
      if (abort.signal.aborted || live.current !== identity) return;
      const response = await fetch(
        `/api/asset-transfer/${id}/cross-branch${path}`,
        {
          method: "POST",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token.csrfToken,
            "If-Match": data.version,
          },
          body: JSON.stringify(body),
        },
      );
      if (response.status === 401) actions.invalidateSession(owner);
      const result = await response.json();
      if (!response.ok)
        throw Error(
          result.detail ?? `Điều chuyển thất bại (${response.status}).`,
        );
      if (!abort.signal.aborted && live.current === identity) {
        setView(null);
        setMessage(
          `Bàn giao ${result.handoverId}: ${displayCode(result.state)}.`,
        );
        setReason("");
        setCondition("");
        setConfirmation("");
        setDecisionReason("");
        setDecisionConfirmation("");
      }
    } catch (e) {
      if (!abort.signal.aborted && live.current === identity) {
        setView(null);
        setError(
          `${String(e)} Tải lại trạng thái trước thao tác tiếp theo; không gửi lại khi chưa rõ kết quả.`,
        );
      }
    } finally {
      if (write.current === abort) {
        write.current = null;
        setBusy(false);
      }
    }
  }
  function submit() {
    const [kind, raw] = target.split(":");
    const location = Number(raw);
    const selected = destination?.receivers.find(
      (r) => `${r.departmentId}:${r.userId}` === receiver,
    );
    if (
      !destination ||
      !selected ||
      !(
        kind === "ROOM"
          ? destination.rooms
          : kind === "AREA"
            ? destination.areas
            : []
      ).some((r) => r.id === location) ||
      reason.trim().length < 10 ||
      !condition.trim() ||
      (data?.hasOpenWork && !confirmation.trim())
    ) {
      setError(
        "Chọn nơi nhận, người nhận đủ điều kiện và vị trí; nhập lý do, tình trạng và xác nhận công việc còn mở bắt buộc.",
      );
      return;
    }
    void post("", {
      branchId: destination.branchId,
      roomId: kind === "ROOM" ? location : null,
      areaId: kind === "AREA" ? location : null,
      departmentId: selected.departmentId,
      receiverId: selected.userId,
      reason,
      conditionNote: condition,
      confirmationNote: confirmation,
    });
  }
  function decide(row: Pending, action: "ACCEPT" | "REJECT" | "CANCEL") {
    if (
      (action !== "ACCEPT" && decisionReason.trim().length < 10) ||
      (action === "ACCEPT" && data?.hasOpenWork && !decisionConfirmation.trim())
    ) {
      setError(
        "Nhập lý do từ chối / hủy hoặc xác nhận công việc chưa hoàn tất vẫn thuộc chi nhánh nguồn.",
      );
      return;
    }
    void post(`/${row.handoverId}/decision`, {
      action,
      reason: decisionReason,
      confirmationNote: decisionConfirmation,
    });
  }
  return (
    <section>
      <h1>Bàn giao tài sản liên chi nhánh</h1>
      <Link to={`/assets/${id}`}>Hồ sơ tài sản</Link>
      {" · "}
      <Link to={`/assets/${id}/transfer`}>
        Điều chuyển trong chi nhánh hiện tại
      </Link>
      {message && <p role="status">{message}</p>}
      {error && <p role="alert">{error}</p>}
      <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
        Tải lại trạng thái hiện tại
      </button>
      {data && (
        <>
          <h2>
            {data.assetCode} — {data.assetName}
          </h2>
          <p>
            Trách nhiệm quản lý vẫn ở chi nhánh {data.branchId} đến khi trưởng
            bộ phận tiếp nhận xác nhận. Công việc, người được giao và SLA hiện
            có vẫn thuộc chi nhánh nguồn.
          </p>
          <p>
            Vị trí hiện tại:{" "}
            {data.currentRoomId != null
              ? `Phòng ${data.currentRoomId}`
              : `Khu vực ${data.currentAreaId}`}
            .
          </p>
          {data.hasOpenWork && (
            <p role="alert">
              Công việc liên quan còn mở. Người gửi và người nhận phải xác nhận
              rõ trách nhiệm của chi nhánh nguồn.
            </p>
          )}
          {data.canSubmit && (
            <ActionDialog
              error={error}
              title="Yêu cầu tiếp nhận tài sản"
              busy={busy}
            >
              <fieldset disabled={busy}>
                <legend>Yêu cầu tiếp nhận quản lý tài sản</legend>
                <label>
                  Chi nhánh đích{" "}
                  <select
                    value={branch}
                    onChange={(e) => {
                      setBranch(e.target.value);
                      setTarget("");
                      setReceiver("");
                    }}
                  >
                    <option value="">Chọn</option>
                    {data.destinations.map((d) => (
                      <option key={d.branchId} value={d.branchId}>
                        {displayCode(d.branchName)}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Vị trí đích{" "}
                  <select
                    value={target}
                    onChange={(e) => setTarget(e.target.value)}
                  >
                    <option value="">Chọn</option>
                    <optgroup label="Rooms">
                      {destination?.rooms.map((r) => (
                        <option key={r.id} value={`Phòng:${r.id}`}>
                          {r.name}
                        </option>
                      ))}
                    </optgroup>
                    <optgroup label="Areas">
                      {destination?.areas.map((a) => (
                        <option key={a.id} value={`Khu vực:${a.id}`}>
                          {a.name}
                        </option>
                      ))}
                    </optgroup>
                  </select>
                </label>
                <label>
                  Trưởng bộ phận / bộ phận tiếp nhận{" "}
                  <select
                    value={receiver}
                    onChange={(e) => setReceiver(e.target.value)}
                  >
                    <option value="">Chọn</option>
                    {destination?.receivers.map((r) => (
                      <option
                        key={`${r.departmentId}:${r.userId}`}
                        value={`${r.departmentId}:${r.userId}`}
                      >
                        {r.name} / {displayCode(r.departmentName)}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Lý do điều chuyển{" "}
                  <textarea
                    maxLength={4000}
                    minLength={10}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                </label>
                <label>
                  Tình trạng khi bàn giao{" "}
                  <textarea
                    maxLength={4000}
                    value={condition}
                    onChange={(e) => setCondition(e.target.value)}
                  />
                </label>
                <label>
                  Xác nhận công việc chưa hoàn tất vẫn thuộc chi nhánh nguồn{" "}
                  <textarea
                    maxLength={4000}
                    value={confirmation}
                    onChange={(e) => setConfirmation(e.target.value)}
                  />
                </label>
                <button data-dialog-action onClick={submit}>
                  Gửi bàn giao
                </button>
              </fieldset>
            </ActionDialog>
          )}
          {data.hasPendingHandover && data.pending.length === 0 && (
            <p>
              Bàn giao đang chờ xử lý. Cần quyền của người tham gia và quyền chi
              nhánh để xem chi tiết.
            </p>
          )}
          {data.pending.map((p) => (
            <ActionDialog
              error={error}
              title="Quyết định bàn giao tài sản"
              busy={busy}
            >
              <fieldset key={p.handoverId} disabled={busy}>
                <legend>{p.handoverNo}</legend>
                <p>
                  Chi nhánh đích {p.destinationBranchId}; Bộ phận{" "}
                  {p.toDepartmentId}; Người nhận {p.toUserId};{" "}
                  {p.targetRoomId != null
                    ? `Phòng ${p.targetRoomId}`
                    : `Khu vực ${p.targetAreaId}`}
                  .
                </p>
                <p>Lý do: {p.reason}</p>
                <p>Tình trạng: {p.conditionNote}</p>
                <p>Xác nhận của người gửi: {p.confirmationNote ?? "N/A"}</p>
                <label>
                  Lý do quyết định{" "}
                  <textarea
                    maxLength={4000}
                    value={decisionReason}
                    onChange={(e) => setDecisionReason(e.target.value)}
                  />
                </label>
                <label>
                  Xác nhận trách nhiệm của chi nhánh nguồn với công việc chưa
                  hoàn tất{" "}
                  <textarea
                    maxLength={4000}
                    value={decisionConfirmation}
                    onChange={(e) => setDecisionConfirmation(e.target.value)}
                  />
                </label>
                {p.canDecide && (
                  <>
                    <button
                      data-dialog-action
                      onClick={() => decide(p, "ACCEPT")}
                    >
                      Xác nhận nhận tài sản và chuyển trách nhiệm quản lý
                    </button>
                    <button onClick={() => decide(p, "REJECT")}>
                      Từ chối bàn giao
                    </button>
                  </>
                )}
                {p.canCancel && (
                  <button onClick={() => decide(p, "CANCEL")}>
                    Hủy yêu cầu
                  </button>
                )}
              </fieldset>
            </ActionDialog>
          ))}
        </>
      )}
    </section>
  );
}
