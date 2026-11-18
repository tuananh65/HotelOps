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
}: {
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
    setReview(null);
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
        setDenied(true);
        setFields({ ...blank });
      }
    }
  }
  const created = result?.key === key ? result.data.branch : null;
  return (
    <main>
      <h1>Tạo chi nhánh</h1>
      <p>
        Đăng nhập không cấp MASTER_DATA_MANAGE. Quyền do máy chủ kiểm tra.
      </p>
      <nav>
        <Link to="/hotel">Cấu trúc khách sạn</Link>
      </nav>
      {created ? (
        <section aria-label="Chi nhánh đã tạo">
          <h2>Đã tạo chi nhánh</h2>
          <p>Mã chi nhánh: {created.id}</p>
          <dl>
            {(
              [
                "code",
                "name",
                "address",
                "phone",
                "email",
                "timezone",
                "isActive",
              ] as const
            ).map((field) => (
              <div key={field}>
                <dt>{field}</dt>
                <dd>{String(created[field] ?? "")}</dd>
              </div>
            ))}
          </dl>
          <Link to={`/branches/${created.id}/edit`}>
            Xem thông tin chi nhánh
          </Link>
        </section>
      ) : (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            if (busy.current || locked.current || denied) return;
            const input = createRequest(fields);
            if (!input) {
              setMessage(
                "Mã, tên và địa chỉ là bắt buộc. Kiểm tra mã, định dạng liên hệ và giới hạn trường.",
              );
              return;
            }
            const next = { key, input };
            reviewed.current = next;
            setReview(next);
            setMessage("");
          }}
        >
          {(["code", "name", "address", "phone", "email"] as const).map(
            (field) => (
              <label key={field}>
                {field}
                <input
                  aria-label={field}
                  value={fields[field] ?? ""}
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
                  onChange={(event) => {
                    reviewed.current = null;
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
            disabled={phase === "saving" || locked.current || denied}
          >
            Xem lại tạo mới
          </button>
        </form>
      )}
      {review?.key === key && (
        <section aria-label="Xem lại tạo chi nhánh">
          <p>
            {review.input.code}; {review.input.name}; {review.input.address};{" "}
            {review.input.phone ?? "(chưa có số điện thoại)"};{" "}
            {review.input.email ?? "(chưa có email)"}
          </p>
          <button
            type="button"
            disabled={busy.current || locked.current}
            onClick={() => void confirm()}
          >
            Xác nhận tạo mới
          </button>
        </section>
      )}
      {phase === "saving" && <p role="status">Đang tạo chi nhánh...</p>}
      {message && <p role="alert">{message}</p>}
      <button
        disabled={phase === "saving"}
        onClick={() => void actions.revalidate()}
      >
        Kiểm tra phiên đăng nhập
      </button>
    </main>
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
  if (!options) return <p>Đang kiểm tra quyền và SYS-PAR-10…</p>;
  if (options.error || !options.data) return <p role="alert">{options.error ?? "Danh mục chưa sẵn sàng."}</p>;
  return <><p>Múi giờ cố định theo SYS-PAR-10: {options.data.timezone}</p>
    <CreateForm session={state.authorityGeneration} locked={locked} /></>;
}
