import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createBranchMaintenanceApi } from "./branchMaintenanceApi";
import {
  safeId,
  validUpdate,
  type BranchSnapshot,
  type BranchUpdate,
} from "./contracts";
const unknownMessage =
  "Chưa xác định kết quả cập nhật. Không gửi lại. Nhờ quản trị viên kiểm tra chi nhánh trước khi mở quy trình mới.";
const empty: BranchUpdate = {
  name: "",
  address: "",
  phone: null,
  email: null,
  reason: "",
};
type ReadState =
  { key: object; data: BranchSnapshot } | { key: object; error: string };
type Review = { key: object; input: BranchUpdate; etag: string };
/** UC012 normal flow. Uses the native-rowversion BranchAdministrationController under AuthProvider.
 * No role inference.
 */
export function BranchMaintenancePanel({ branchId }: { branchId: number }) {
  const { state, actions } = useAuth();
  const [api] = useState(() => createBranchMaintenanceApi());
  const confirmed =
    state.status === "AUTHENTICATED" &&
    state.session !== null &&
    !state.session.mustChangePassword &&
    state.pending === null;
  const key = useMemo(() => ({}), [state.session, branchId, confirmed]);
  const latest = useRef({ key, confirmed });
  latest.current = { key, confirmed };
  const [read, setRead] = useState<ReadState | null>(null),
    [attempt, setAttempt] = useState(0);
  const [fields, setFields] = useState<BranchUpdate>(empty),
    [review, setReview] = useState<Review | null>(null);
  const reviewRef = useRef<Review | null>(null),
    busy = useRef(false),
    locked = useRef(false),
    mounted = useRef(false);
  const writer = useRef<AbortController | null>(null);
  const [phase, setPhase] = useState("idle"),
    [message, setMessage] = useState("");
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      writer.current?.abort();
    };
  }, []);
  useEffect(() => {
    reviewRef.current = null;
    setReview(null);
    setRead(null);
    setFields({ ...empty });
    setPhase(locked.current ? "unknown" : "idle");
    setMessage(locked.current ? unknownMessage : "");
    if (!confirmed || !safeId(branchId)) return;
    const controller = new AbortController();
    let active = true;
    void api.read(branchId, controller.signal).then((result) => {
      if (!active || latest.current.key !== key) return;
      if (result.ok) {
        setRead({ key, data: result.data });
        setFields({
          name: result.data.branch.name,
          address: result.data.branch.address ?? "",
          phone: result.data.branch.phone,
          email: result.data.branch.email,
          reason: "",
        });
      } else
        setRead({
          key,
          error:
            result.error.status === 404
              ? "Dịch vụ đọc chi nhánh chưa khả dụng hoặc không tìm thấy chi nhánh. Chưa tải dữ liệu."
              : result.error.status === 401
                ? "Phiên đăng nhập không hợp lệ. Xác minh lại trước khi tiếp tục."
                : result.error.status === 403
                  ? "Máy chủ từ chối quyền quản lý chi nhánh."
                  : "Thông tin chi nhánh chưa khả dụng.",
        });
    });
    return () => {
      active = false;
      controller.abort();
      writer.current?.abort();
      if (busy.current) {
        locked.current = true;
        busy.current = false;
      }
    };
  }, [key, confirmed, branchId, api, attempt]);
  const snapshot = read?.key === key && "data" in read ? read.data : null;
  const currentReview = review?.key === key ? review : null;
  async function confirm() {
    const selected = reviewRef.current;
    if (
      !selected ||
      selected.key !== key ||
      !confirmed ||
      busy.current ||
      locked.current
    )
      return;
    reviewRef.current = null;
    setReview(null);
    busy.current = true;
    setPhase("saving");
    setMessage("");
    const controller = new AbortController();
    writer.current = controller;
    const current = () =>
      mounted.current &&
      latest.current.key === selected.key &&
      latest.current.confirmed;
    const result = await api.update(
      branchId,
      selected.etag,
      selected.input,
      current,
      controller.signal,
    );
    if (!current()) return;
    busy.current = false;
    writer.current = null;
    if (result.outcome === "CONFIRMED") {
      setRead({ key, data: result.data });
      setFields({ ...selected.input, reason: "" });
      setPhase("saved");
      setMessage("Đã cập nhật thông tin chi nhánh.");
    } else if (result.outcome === "OUTCOME_UNKNOWN") {
      locked.current = true;
      setPhase("unknown");
      setMessage(unknownMessage);
    } else if (result.error.status === 412 || result.error.status === 428) {
      setRead(null);
      setPhase("stale");
      setMessage(
        "Phiên bản đã đổi hoặc chưa khả dụng. Đọc lại, xem giá trị mới nhất rồi xác nhận cập nhật mới.",
      );
    } else {
      if ([401, 403, 404].includes(result.error.status ?? 0)) {
        setRead({
          key,
          error:
            "Dữ liệu chi nhánh đã ẩn sau khi máy chủ từ chối. Xác minh lại phiên / quyền trước khi tiếp tục.",
        });
        setFields({ ...empty });
      }
      setPhase("rejected");
      setMessage(
        result.error.status === 401
          ? "Phiên đăng nhập không hợp lệ. Xác minh lại trước khi tiếp tục."
          : result.error.status === 403
            ? "Máy chủ từ chối cập nhật này."
            : result.error.status === 404
              ? "Dịch vụ cập nhật chưa khả dụng hoặc không tìm thấy chi nhánh."
              : result.error.status === 409
                ? "Xung đột cập nhật. Kiểm tra trạng thái máy chủ trước khi thử tiếp."
                : "Chưa xác nhận cập nhật. Sửa dữ liệu hoặc đọc lại.",
      );
    }
  }
  if (!confirmed)
    return <p role="alert">Cần phiên đăng nhập đã xác minh và không bị hạn chế.</p>;
  if (!safeId(branchId))
    return <p role="alert">Chọn mã chi nhánh là số nguyên dương hợp lệ.</p>;
  return (
    <section aria-label="Cập nhật thông tin chi nhánh">
      <h1>Cập nhật thông tin chi nhánh</h1>
      <p>Thay đổi thông tin chi nhánh cần máy chủ cấp quyền.</p>
      <button
        disabled={phase === "saving"}
        onClick={() => {
          reviewRef.current = null;
          setReview(null);
          setAttempt((v) => v + 1);
        }}
      >
        Xem chi nhánh
      </button>
      <button
        disabled={phase === "saving"}
        onClick={() => void actions.revalidate()}
      >
        Kiểm tra phiên đăng nhập
      </button>
      {read?.key === key && "error" in read && <p role="alert">{read.error}</p>}
      {!snapshot &&
        !(read?.key === key && "error" in read) &&
        phase !== "stale" && <p role="status">Đang tải thông tin chi nhánh...</p>}
      {snapshot && (
        <>
          <p>
            Chi nhánh {snapshot.branch.id}: {snapshot.branch.code}; múi giờ{" "}
            {snapshot.branch.timezone ?? "Chưa cung cấp"};{" "}
            {snapshot.branch.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
          </p>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              if (!snapshot || busy.current || locked.current) return;
              if (!validUpdate(fields)) {
                setMessage(
                  "Tên và địa chỉ là bắt buộc. Kiểm tra giới hạn trường và nhập lý do ít nhất 10 ký tự.",
                );
                return;
              }
              const next = { key, input: { ...fields }, etag: snapshot.etag };
              reviewRef.current = next;
              setReview(next);
              setMessage("");
            }}
          >
            {(["name", "address", "phone", "email", "reason"] as const).map(
              (field) => (
                <label key={field}>
                  {field}
                  <input
                    aria-label={field}
                    value={fields[field] ?? ""}
                    disabled={phase === "saving" || locked.current}
                    maxLength={
                      field === "address"
                        ? 4000
                        : field === "name"
                          ? 200
                          : field === "phone"
                            ? 20
                            : field === "email"
                              ? 150
                              : 500
                    }
                    onChange={(event) => {
                      reviewRef.current = null;
                      setReview(null);
                      setFields((v) => ({
                        ...v,
                        [field]:
                          event.target.value === "" &&
                          (field === "phone" || field === "email")
                            ? null
                            : event.target.value,
                      }));
                    }}
                  />
                </label>
              ),
            )}
            <button
              type="submit"
              disabled={phase === "saving" || locked.current}
            >
              Xem lại cập nhật
            </button>
          </form>
        </>
      )}
      {currentReview && (
        <section aria-label="Xem lại cập nhật chi nhánh">
          <p>
            Chi nhánh {branchId}: {currentReview.input.name};{" "}
            {currentReview.input.address};{" "}
            {currentReview.input.phone ?? "(xóa số điện thoại)"};{" "}
            {currentReview.input.email ?? "(xóa email)"}; Lý do{" "}
            {currentReview.input.reason}
          </p>
          <button
            type="button"
            disabled={phase === "saving" || locked.current}
            onClick={() => void confirm()}
          >
            Xác nhận cập nhật
          </button>
        </section>
      )}
      {phase === "saving" && <p role="status">Đang lưu…</p>}
      {message && (
        <p role={phase === "saved" ? "status" : "alert"}>{message}</p>
      )}
    </section>
  );
}
