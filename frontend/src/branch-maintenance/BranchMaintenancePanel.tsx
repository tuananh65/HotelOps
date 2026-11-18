import { AppButton } from "../components/design-system";
import { Pencil, RefreshCw } from "lucide-react";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { BranchSummary, branchLabels } from "./branch-ui";
import { BranchDeactivationReview } from "./BranchDeactivationReview";
import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
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
  const key = useMemo(
    () => ({}),
    [state.authorityGeneration, branchId, confirmed],
  );
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
  const deactivationLocked = useRef(false);
  const [editing, setEditing] = useState(false);
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
    setEditing(false);
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
                  : result.error.status === 503
                    ? "Cơ sở dữ liệu chi nhánh chưa sẵn sàng. Liên hệ quản trị viên kiểm tra cấu trúc dữ liệu."
                    : "Thông tin chi nhánh chưa khả dụng. Vui lòng thử lại.",
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
      setFields({
        name: result.data.branch.name,
        address: result.data.branch.address ?? "",
        phone: result.data.branch.phone,
        email: result.data.branch.email,
        reason: "",
      });
      setReview(null);
      setEditing(false);
      setPhase("saved");
      (announceWorkflowSuccess(
        "Đã cập nhật thông tin chi nhánh.",
        "branch-maintenance/BranchMaintenancePanel.tsx",
      ),
        setMessage("Đã cập nhật thông tin chi nhánh."));
    } else if (result.outcome === "OUTCOME_UNKNOWN") {
      locked.current = true;
      setPhase("unknown");
      setMessage(unknownMessage);
    } else if (result.error.status === 412 || result.error.status === 428) {
      setRead(null);
      setReview(null);
      setEditing(false);
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
        setReview(null);
        setEditing(false);
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
    return (
      <p role="alert">Cần phiên đăng nhập đã xác minh và không bị hạn chế.</p>
    );
  if (!safeId(branchId))
    return <p role="alert">Chọn mã chi nhánh là số nguyên dương hợp lệ.</p>;
  const resetEditor = () => {
    reviewRef.current = null;
    setReview(null);
    if (snapshot)
      setFields({
        name: snapshot.branch.name,
        address: snapshot.branch.address ?? "",
        phone: snapshot.branch.phone,
        email: snapshot.branch.email,
        reason: "",
      });
    if (!locked.current) setMessage("");
  };
  return (
    <section className="branch-workspace" aria-label="Thông tin chi nhánh">
      <header className="branch-heading">
        <div>
          <span className="branch-eyebrow">Thông tin chi nhánh</span>
          <h1>{snapshot?.branch.name ?? "Thông tin chi nhánh"}</h1>
          <p>Xem thông tin liên hệ và trạng thái hoạt động.</p>
        </div>
        <AppButton
          icon={RefreshCw}
          disabled={phase === "saving"}
          onClick={() => setAttempt((v) => v + 1)}
        >
          Tải lại thông tin
        </AppButton>
      </header>
      {read?.key === key && "error" in read && (
        <div className="branch-feedback" role="alert">
          {read.error}
        </div>
      )}
      {!snapshot &&
        !(read?.key === key && "error" in read) &&
        phase !== "stale" && (
          <LoadingIndicator label="Đang tải thông tin chi nhánh" />
        )}
      {snapshot && (
        <>
          <div className="branch-card">
            <div className="branch-section-heading">
              <div>
                <h2>Hồ sơ chi nhánh</h2>
                <p className="branch-muted">
                  Thông tin chung và liên hệ của khách sạn.
                </p>
              </div>
              <AppButton
                icon={Pencil}
                variant="primary"
                disabled={phase === "saving" || locked.current}
                onClick={() => {
                  resetEditor();
                  setEditing(true);
                }}
              >
                Chỉnh sửa chi nhánh
              </AppButton>
            </div>
            <BranchSummary branch={snapshot.branch} />
          </div>
          <ActionDialog
            open={editing}
            title={
              currentReview
                ? "Xác nhận cập nhật chi nhánh"
                : "Chỉnh sửa chi nhánh"
            }
            onClose={() => {
              setEditing(false);
              resetEditor();
            }}
            busy={phase === "saving"}
            locked={locked.current}
            error={
              message ? (
                <p className="branch-feedback" role="alert">
                  {message}
                </p>
              ) : undefined
            }
          >
            {currentReview ? (
              <section aria-label="Xem lại cập nhật chi nhánh">
                <BranchSummary
                  branch={{ ...snapshot.branch, ...currentReview.input }}
                />
                <p>
                  <strong>Lý do thay đổi:</strong> {currentReview.input.reason}
                </p>
                <button
                  type="button"
                  disabled={phase === "saving" || locked.current}
                  onClick={() => {
                    setReview(null);
                    reviewRef.current = null;
                    setMessage("");
                  }}
                >
                  Sửa lại thông tin
                </button>
                <button
                  data-dialog-action
                  type="button"
                  disabled={
                    phase === "saving" || locked.current || !reviewRef.current
                  }
                  onClick={() => void confirm()}
                >
                  {phase === "saving" ? "Đang lưu…" : "Xác nhận cập nhật"}
                </button>
              </section>
            ) : (
              <form
                className="branch-form"
                onSubmit={(event) => {
                  event.preventDefault();
                  if (!snapshot || busy.current || locked.current) return;
                  if (!validUpdate(fields)) {
                    setMessage(
                      "Tên và địa chỉ là bắt buộc. Kiểm tra thông tin liên hệ và nhập lý do ít nhất 10 ký tự.",
                    );
                    return;
                  }
                  const next = {
                    key,
                    input: { ...fields },
                    etag: snapshot.etag,
                  };
                  reviewRef.current = next;
                  setReview(next);
                  setMessage("");
                }}
              >
                <p className="branch-form-intro">
                  Cập nhật thông tin bên dưới, sau đó xem lại trước khi lưu.
                </p>
                <div className="branch-form-grid">
                  {(
                    ["name", "address", "phone", "email", "reason"] as const
                  ).map((field) => (
                    <label
                      key={field}
                      className={
                        field === "address" || field === "reason"
                          ? "branch-wide"
                          : ""
                      }
                    >
                      {branchLabels[field]}
                      {["name", "address", "reason"].includes(field)
                        ? " *"
                        : ""}
                      <input
                        aria-label={branchLabels[field]}
                        value={fields[field] ?? ""}
                        placeholder={
                          field === "reason"
                            ? "Nhập lý do cập nhật (ít nhất 10 ký tự)"
                            : undefined
                        }
                        type={
                          field === "email"
                            ? "email"
                            : field === "phone"
                              ? "tel"
                              : "text"
                        }
                        required={["name", "address", "reason"].includes(field)}
                        minLength={field === "reason" ? 10 : undefined}
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
                        disabled={phase === "saving" || locked.current}
                        onChange={(e) =>
                          setFields((v) => ({
                            ...v,
                            [field]:
                              !e.target.value &&
                              (field === "phone" || field === "email")
                                ? null
                                : e.target.value,
                          }))
                        }
                      />
                    </label>
                  ))}
                </div>
                <p className="branch-muted">
                  * Thông tin bắt buộc. Mã chi nhánh và múi giờ được giữ theo
                  cấu hình hiện tại.
                </p>
                <button
                  data-dialog-action
                  type="submit"
                  disabled={phase === "saving" || locked.current}
                >
                  Xem lại cập nhật
                </button>
              </form>
            )}
          </ActionDialog>
          <BranchDeactivationReview
            uncertain={deactivationLocked}
            branchId={branchId}
            isActive={snapshot.branch.isActive}
            disabled={editing || phase === "saving" || locked.current}
            onDeactivated={(data) => {
              setRead({ key, data });
              resetEditor();
              setEditing(false);
            }}
          />
        </>
      )}
      {message && !editing && (
        <p
          className={
            "branch-feedback " +
            (phase === "saved"
              ? "is-success"
              : locked.current
                ? "is-warning"
                : "")
          }
          role={phase === "saved" ? "status" : "alert"}
        >
          {message}
        </p>
      )}
      {read && "error" in read && (
        <div>
          <AppButton onClick={() => void actions.revalidate()}>
            Kiểm tra phiên đăng nhập
          </AppButton>
        </div>
      )}
    </section>
  );
}
