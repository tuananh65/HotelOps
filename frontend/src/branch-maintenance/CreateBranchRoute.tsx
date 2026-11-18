import { AppButton } from "../components/design-system";
import {
  ArrowLeft,
  Building2,
  Contact,
  Clock3,
  ArrowRight,
} from "lucide-react";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { BranchSummary, branchLabels } from "./branch-ui";
import { ActionDialog } from "../components/workspace-interactions";
import { useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createBranchApi } from "./createBranchApi";
import { useBranchManagementOptions } from "./BranchManagementPage";
import {
  createRequest,
  type CreateBranchRequest,
  type CreatedBranch,
} from "./createBranchContracts";
const blank: CreateBranchRequest = {
  code: "",
  name: "",
  address: "",
  phone: null,
  email: null,
};
const unknownMessage =
  "Chưa xác định kết quả tạo. Chi nhánh có thể đã tồn tại. Không gửi lại; nhờ quản trị viên kiểm tra trước khi bắt đầu quy trình mới.";
function CreateForm({
  session,
  locked,
  timezone,
}: {
  timezone: string;
  session: object;
  locked: RefObject<boolean>;
}) {
  const { state, actions } = useAuth();
  const key = useMemo(() => ({}), [session]);
  const latest = useRef(key);
  latest.current = key;
  const mounted = useRef(false),
    busy = useRef(false),
    writer = useRef<AbortController | null>(null);
  const reviewed = useRef<{ key: object; input: CreateBranchRequest } | null>(
    null,
  );
  const [api] = useState(() => createBranchApi());
  const [fields, setFields] = useState<CreateBranchRequest>({ ...blank });
  const [review, setReview] = useState<{
    key: object;
    input: CreateBranchRequest;
  } | null>(null);
  const [result, setResult] = useState<{
    key: object;
    data: CreatedBranch;
  } | null>(null);
  const [phase, setPhase] = useState("idle"),
    [message, setMessage] = useState("");
  const [denied, setDenied] = useState(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      writer.current?.abort();
    };
  }, []);
  useEffect(() => {
    reviewed.current = null;
    setReview(null);
    setResult(null);
    setFields({ ...blank });
    setPhase(locked.current ? "unknown" : "idle");
    setMessage(locked.current ? unknownMessage : "");
    return () => {
      writer.current?.abort();
      if (busy.current) {
        locked.current = true;
        busy.current = false;
      }
    };
  }, [key]);
  const current = () =>
    mounted.current &&
    latest.current === key &&
    state.status === "AUTHENTICATED" &&
    state.pending === null;
  async function confirm() {
    const selected = reviewed.current;
    if (
      !selected ||
      selected.key !== key ||
      busy.current ||
      locked.current ||
      denied
    )
      return;
    reviewed.current = null;
    busy.current = true;
    setPhase("saving");
    setMessage("");
    const controller = new AbortController();
    writer.current = controller;
    const outcome = await api(selected.input, current, controller.signal);
    if (!current()) return;
    busy.current = false;
    writer.current = null;
    if (outcome.outcome === "CONFIRMED") {
      setResult({ key, data: outcome.data });
      setPhase("created");
      setReview(null);
      setFields({ ...blank });
    } else if (outcome.outcome === "OUTCOME_UNKNOWN") {
      locked.current = true;
      setPhase("unknown");
      setMessage(unknownMessage);
    } else {
      setPhase("rejected");
      setMessage(
        outcome.error.status === 409
          ? "Mã chi nhánh đã tồn tại. Chọn mã khác."
          : outcome.error.status === 401
            ? "Phiên đăng nhập không hợp lệ. Kiểm tra phiên trước khi tiếp tục."
            : outcome.error.status === 403
              ? "Máy chủ từ chối tạo chi nhánh."
              : outcome.error.status === 400
                ? "Máy chủ từ chối thông tin chi nhánh. Sửa và kiểm tra lại."
                : outcome.error.status === 404
                  ? "Dịch vụ tạo chi nhánh chưa khả dụng."
                  : "Chưa gửi hoặc chưa xác nhận tạo mới. Kiểm tra phiên và xem lại.",
      );
      if (outcome.error.status === 401 || outcome.error.status === 403) {
        setReview(null);
        setDenied(true);
        setFields({ ...blank });
      }
    }
  }
  const created = result?.key === key ? result.data.branch : null;
  return (
    <section className="branch-workspace">
      <nav aria-label="Điều hướng chi nhánh" className="branch-back">
        <Link to="/branches">
          <ArrowLeft size={16} aria-hidden="true" />
          Quản lý chi nhánh
        </Link>
      </nav>
      <header className="branch-heading">
        <div>
          <span className="branch-eyebrow">Cấu trúc khách sạn</span>
          <h1>Tạo chi nhánh</h1>
          <p>Nhập thông tin chi nhánh mới. Các trường có dấu * là bắt buộc.</p>
        </div>
      </header>
      {created ? (
        <div className="branch-card" aria-label="Chi nhánh đã tạo">
          <p className="branch-feedback is-success" role="status">
            Đã tạo chi nhánh thành công.
          </p>
          <BranchSummary branch={created} />
          <div className="branch-actions" style={{ marginTop: 24 }}>
            <Link
              className="ds-button ds-button--primary"
              to={"/branches/" + created.id + "/edit"}
            >
              Xem thông tin chi nhánh
            </Link>
            <Link className="ds-button ds-button--secondary" to="/branches">
              Về danh sách
            </Link>
          </div>
        </div>
      ) : (
        <form
          className="branch-card branch-form"
          onSubmit={(event) => {
            event.preventDefault();
            if (busy.current || locked.current || denied) return;
            const input = createRequest(fields);
            if (!input) {
              setMessage(
                "Nhập mã, tên và địa chỉ hợp lệ. Mã chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới; kiểm tra lại thông tin liên hệ.",
              );
              return;
            }
            const next = { key, input };
            reviewed.current = next;
            setReview(next);
            setMessage("");
          }}
        >
          {(
            [
              {
                title: "Thông tin chung",
                description: "Mã, tên và địa chỉ giúp nhận diện chi nhánh.",
                fields: ["code", "name", "address"],
                Icon: Building2,
              },
              {
                title: "Thông tin liên hệ",
                description: "Có thể bổ sung hoặc cập nhật sau.",
                fields: ["phone", "email"],
                Icon: Contact,
              },
            ] as const
          ).map(({ title, description, fields: groupFields, Icon }) => (
            <fieldset className="branch-form-section" key={title}>
              <legend>
                <Icon size={19} aria-hidden="true" />
                {title}
              </legend>
              <p className="branch-muted">{description}</p>
              <div className="branch-form-grid">
                {groupFields.map((field) => (
                  <label
                    key={field}
                    className={field === "address" ? "branch-wide" : ""}
                  >
                    {branchLabels[field]}
                    {["code", "name", "address"].includes(field) ? " *" : ""}
                    <input
                      aria-label={branchLabels[field]}
                      value={fields[field] ?? ""}
                      placeholder={
                        field === "code"
                          ? "VD: CN-BUIVIEN"
                          : field === "name"
                            ? "Nhập tên chi nhánh"
                            : field === "address"
                              ? "Số nhà, đường, phường/xã, tỉnh/thành phố"
                              : field === "phone"
                                ? "Nhập số điện thoại liên hệ"
                                : "VD: lienhe@khachsan.vn"
                      }
                      type={
                        field === "email"
                          ? "email"
                          : field === "phone"
                            ? "tel"
                            : "text"
                      }
                      required={["code", "name", "address"].includes(field)}
                      maxLength={
                        field === "code"
                          ? 30
                          : field === "name"
                            ? 200
                            : field === "address"
                              ? 4000
                              : field === "phone"
                                ? 20
                                : 150
                      }
                      disabled={phase === "saving" || locked.current || denied}
                      onChange={(e) => {
                        reviewed.current = null;
                        setReview(null);
                        setFields((v) => ({
                          ...v,
                          [field]:
                            !e.target.value &&
                            (field === "phone" || field === "email")
                              ? null
                              : e.target.value,
                        }));
                      }}
                    />
                  </label>
                ))}
              </div>
            </fieldset>
          ))}
          <div className="branch-form-note">
            <Clock3 size={20} aria-hidden="true" />
            <div>
              <strong>Múi giờ: {timezone}</strong>
              <p>Chi nhánh mới sẽ ở trạng thái đang hoạt động.</p>
            </div>
          </div>
          <div className="branch-actions branch-form-footer">
            <AppButton
              icon={ArrowRight}
              variant="primary"
              type="submit"
              disabled={phase === "saving" || locked.current || denied}
            >
              Xem lại thông tin
            </AppButton>
            <Link to="/branches">Hủy và về danh sách</Link>
          </div>
        </form>
      )}
      {review?.key === key && (
        <ActionDialog
          open
          title="Xác nhận tạo chi nhánh"
          onClose={() => {
            setReview(null);
            reviewed.current = null;
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
          <BranchSummary
            branch={{ ...review.input, id: 0, timezone, isActive: true }}
          />
          <button
            data-dialog-action
            type="button"
            disabled={phase === "saving" || locked.current || !reviewed.current}
            onClick={() => void confirm()}
          >
            {phase === "saving" ? "Đang tạo…" : "Xác nhận tạo mới"}
          </button>
        </ActionDialog>
      )}
      {message && !review && (
        <p
          className={"branch-feedback " + (locked.current ? "is-warning" : "")}
          role="alert"
        >
          {message}
        </p>
      )}
      {denied && (
        <div>
          <AppButton onClick={() => void actions.revalidate()}>
            Kiểm tra phiên đăng nhập
          </AppButton>
        </div>
      )}
    </section>
  );
}

export function CreateBranchRoute() {
  const { state } = useAuth();
  const options = useBranchManagementOptions();
  const locked = useRef(false);
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.session.mustChangePassword ||
    state.pending !== null
  )
    return (
      <section>
        <h1>Tạo chi nhánh</h1>
        <p role="alert">Cần phiên đăng nhập đã xác minh và không bị hạn chế.</p>
        <Link to="/login">Đăng nhập</Link>
        <Link to="/change-password">Đổi mật khẩu</Link>
      </section>
    );
  if (options.loading)
    return <LoadingIndicator label="Đang kiểm tra quyền tạo chi nhánh" />;
  if (options.error || !options.data)
    return (
      <section className="branch-workspace">
        <h1>Tạo chi nhánh</h1>
        <Link to="/branches">← Quản lý chi nhánh</Link>
        <p className="branch-feedback" role="alert">
          {options.error ?? "Chưa thể tạo chi nhánh."}
        </p>
        <div>
          <AppButton onClick={options.retry}>Thử lại</AppButton>
        </div>
      </section>
    );
  return (
    <CreateForm
      session={state.authorityGeneration}
      locked={locked}
      timezone={options.data.timezone}
    />
  );
}
