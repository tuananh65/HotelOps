import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";
import { LoadingIndicator } from "../components/LoadingIndicator";

type Option = { id: number; code: string; name: string };
type Context = {
  incidentId: number;
  categoryId: number | null;
  severityId: number | null;
  affectsRoomReadiness: boolean | null;
  version: string;
  categories: Option[];
  severities: Option[];
};
const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const options = (v: unknown): v is Option[] =>
  Array.isArray(v) &&
  v.every(
    (x) =>
      isObject(x) &&
      positive(x.id) &&
      typeof x.code === "string" &&
      typeof x.name === "string",
  );
const tag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);

export function IncidentClassificationPanel({
  data,
  onCommitted,
}: {
  data: Values;
  onCommitted?: (notice: string) => void;
}) {
  const { state } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const mountedOwner = useRef(owner).current;
  const currentOwner = useRef(owner);
  currentOwner.current = owner;
  const id = data.incidentId;
  const [context, setContext] = useState<Context>();
  const [category, setCategory] = useState(""),
    [severity, setSeverity] = useState("");
  const [impact, setImpact] = useState("");
  const [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false);
  const [attempted, setAttempted] = useState(false);
  const [revision, setRevision] = useState(0),
    [loading, setLoading] = useState(false);
  const [forbidden, setForbidden] = useState(false);
  const sending = useRef(false),
    writer = useRef<AbortController | null>(null);
  useEffect(() => {
    if (!owner || owner !== mountedOwner || !positive(id)) return;
    const abort = new AbortController();
    setLoading(true);
    setContext(undefined);
    setMessage("");
    setForbidden(false);
    void (async () => {
      try {
        const response = await fetch(`/api/incidents/${id}/classification`, {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        });
        if (response.status === 403) {
          if (!abort.signal.aborted && currentOwner.current === owner)
            setForbidden(true);
          return;
        }
        if (response.status === 404)
          throw Error(
            "Không tìm thấy sự cố hoặc sự cố nằm ngoài phạm vi truy cập.",
          );
        if (response.status === 409)
          throw Error("Sự cố đã đóng hoặc đã hủy, không thể phân loại.");
        if (!response.ok)
          throw Error(
            "Chưa thể phân loại. Tải lại để kiểm tra phiên và cấu hình.",
          );
        const raw: unknown = await response.json();
        if (
          !isObject(raw) ||
          raw.incidentId !== id ||
          !tag(raw.version) ||
          !(raw.categoryId === null || positive(raw.categoryId)) ||
          !(raw.severityId === null || positive(raw.severityId)) ||
          !(
            raw.affectsRoomReadiness === null ||
            typeof raw.affectsRoomReadiness === "boolean"
          ) ||
          !options(raw.categories) ||
          !options(raw.severities)
        )
          throw Error("Không xác minh được chi tiết phân loại.");
        if (!abort.signal.aborted && currentOwner.current === owner) {
          setContext(raw as Context);
          setCategory(raw.categoryId === null ? "" : String(raw.categoryId));
          setSeverity(raw.severityId === null ? "" : String(raw.severityId));
          setImpact(
            raw.affectsRoomReadiness === null
              ? ""
              : String(raw.affectsRoomReadiness),
          );
          setAttempted(false);
        }
      } catch (e) {
        if (!abort.signal.aborted && currentOwner.current === owner)
          setMessage(
            e instanceof TypeError
              ? "Không kết nối được máy chủ. Kiểm tra kết nối rồi tải lại phân loại."
              : e instanceof Error
                ? e.message
                : "Phân loại chưa khả dụng.",
          );
      } finally {
        if (!abort.signal.aborted && currentOwner.current === owner)
          setLoading(false);
      }
    })();
    return () => {
      abort.abort();
      writer.current?.abort();
    };
  }, [owner, mountedOwner, id, revision]);
  const valid =
    context?.categories.some((x) => String(x.id) === category) &&
    context.severities.some((x) => String(x.id) === severity) &&
    (impact === "true" || impact === "false");
  const changed =
    context &&
    (Number(category) !== context.categoryId ||
      Number(severity) !== context.severityId ||
      (impact === "true") !== context.affectsRoomReadiness);
  const legacySeverity =
    context?.severityId != null &&
    !context.severities.some((x) => x.id === context.severityId);
  const legacyCategory =
    context?.categoryId != null &&
    !context.categories.some((x) => x.id === context.categoryId);
  async function save() {
    if (
      !owner ||
      currentOwner.current !== owner ||
      !context ||
      !valid ||
      !changed ||
      sending.current ||
      attempted
    )
      return;
    sending.current = true;
    setBusy(true);
    setAttempted(true);
    const abort = new AbortController();
    writer.current = abort;
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
        currentOwner.current !== owner
      )
        throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const response = await fetch(`/api/incidents/${id}/classification`, {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
          "If-Match": context.version,
        },
        body: JSON.stringify({
          categoryId: Number(category),
          severityId: Number(severity),
          affectsRoomReadiness: impact === "true",
        }),
      });
      if (response.status === 400) {
        const problem: unknown = await response.json();
        if (
          isObject(problem) &&
          problem.title === "Invalid incident classification" &&
          !abort.signal.aborted &&
          currentOwner.current === owner
        ) {
          setAttempted(false);
          setMessage(
            typeof problem.detail === "string"
              ? problem.detail
              : "Kiểm tra nhóm, mức độ và ảnh hưởng đến khả năng sẵn sàng của phòng.",
          );
          return;
        }
      }
      if (!response.ok)
        throw Error(
          response.status === 412
            ? "Sự cố đã thay đổi. Tải lại trước khi thử lại."
            : "Chưa xác nhận phân loại. Tải lại để kiểm tra sự cố và quyền hiện tại.",
        );
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.incidentId !== id ||
        !positive(result.eventId) ||
        !tag(result.version) ||
        typeof result.suggestRoomBlock !== "boolean"
      )
        throw Error("Chưa xác minh được phản hồi phân loại. Vui lòng tải lại.");
      if (!abort.signal.aborted && currentOwner.current === owner) {
        const notice = result.suggestRoomBlock
          ? "MSG08: Đã lưu phân loại. Xem xét khóa phòng bị ảnh hưởng bằng thao tác Khóa phòng."
          : "MSG08: Đã lưu phân loại.";
        announceWorkflowSuccess(notice, "dev2/IncidentClassificationPanel.tsx");
        setMessage(notice);
        onCommitted?.(notice);
      }
    } catch (e) {
      if (!abort.signal.aborted && currentOwner.current === owner)
        setMessage(
          e instanceof Error
            ? e.message
            : "Chưa xác nhận phân loại. Vui lòng tải lại.",
        );
    } finally {
      if (!abort.signal.aborted) {
        setBusy(false);
        sending.current = false;
      }
    }
  }
  if (!owner || owner !== mountedOwner || !positive(id) || forbidden)
    return null;
  if (!context)
    return (
      <section id="incident-classification" aria-label="Phân loại sự cố">
        <h3>Phân loại sự cố</h3>
        {loading ? (
          <LoadingIndicator label="Đang tải phân loại" />
        ) : (
          message && <p role="alert">{userMessage(message)}</p>
        )}
        {!loading && (
          <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
            Tải lại phân loại
          </button>
        )}
      </section>
    );
  return (
    <ActionDialog
      scope="dev2/IncidentClassificationPanel.tsx"
      title="Phân loại sự cố"
      busy={busy}
    >
      <section id="incident-classification" aria-label="Phân loại sự cố">
        <h3>Phân loại sự cố</h3>
        <p>
          Đánh giá nhóm kỹ thuật, mức độ nghiêm trọng và ảnh hưởng đến phòng. Độ
          ưu tiên, thời hạn SLA và người xử lý được xác định ở bước tiếp nhận và
          phân công.
        </p>
        <p>
          Chọn nhóm theo chuyên môn xử lý chính. Việc đánh dấu ảnh hưởng đến
          phòng không tự động khóa phòng.
        </p>
        {legacySeverity && (
          <p role="status">
            Sự cố đang dùng mức độ cũ hoặc đã ngừng sử dụng. Hãy chọn mức độ
            trong bộ hiện hành trước khi lưu; lịch sử cũ được giữ nguyên.
          </p>
        )}
        {legacyCategory && (
          <p role="status">
            Nhóm hiện tại đã ngừng sử dụng. Hãy chọn một nhóm đang hoạt động
            trước khi lưu.
          </p>
        )}
        {!context.categories.length && (
          <p role="alert">
            Chưa cấu hình nhóm sự cố. Quản trị viên cần bổ sung danh mục nhóm sự
            cố trước khi lưu phân loại.
          </p>
        )}
        {!context.severities.length && (
          <p role="alert">
            Chưa có mức độ sự cố phù hợp đang hoạt động. Cần kiểm tra cấu hình
            danh mục mức độ.
          </p>
        )}
        <fieldset disabled={busy || attempted}>
          <label>
            Nhóm
            <SearchableSelect
              aria-label="Nhóm"
              value={category}
              onChange={(e) => setCategory(e.target.value)}
            >
              <option value="">Chọn nhóm</option>
              {legacyCategory && (
                <option value={String(context.categoryId)} disabled>
                  {String(data.categoryName ?? "Nhóm cũ / ngừng sử dụng")}
                </option>
              )}
              {context.categories.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name}
                </option>
              ))}
            </SearchableSelect>
          </label>
          <label>
            Mức độ
            <SearchableSelect
              aria-label="Mức độ"
              value={severity}
              onChange={(e) => setSeverity(e.target.value)}
            >
              <option value="">Chọn mức độ</option>
              {legacySeverity && (
                <option value={String(context.severityId)} disabled>
                  {String(data.severityName ?? "Mức độ cũ / ngừng sử dụng")}
                </option>
              )}
              {[...context.severities]
                .sort(
                  (a, b) =>
                    ["LOW", "NORMAL", "HIGH", "CRITICAL"].indexOf(a.code) -
                    ["LOW", "NORMAL", "HIGH", "CRITICAL"].indexOf(b.code),
                )
                .map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
            </SearchableSelect>
          </label>
          <label>
            Ảnh hưởng đến khả năng sẵn sàng của phòng
            <SearchableSelect
              aria-label="Ảnh hưởng đến khả năng sẵn sàng của phòng"
              value={impact}
              onChange={(e) => setImpact(e.target.value)}
            >
              <option value="">Chọn mức ảnh hưởng</option>
              <option value="true">Có</option>
              <option value="false">Không</option>
            </SearchableSelect>
          </label>
        </fieldset>
        <p>
          Chọn “Có” nếu sự cố làm phòng chưa sẵn sàng phục vụ khách. Với sự cố ở
          khu vực chung, đánh giá tác động đến các phòng liên quan; việc chọn
          phòng cần khóa thực hiện riêng.
        </p>
        {valid && !changed && (
          <p>Thông tin phân loại đã được lưu. Chỉ lưu lại khi có thay đổi.</p>
        )}
        {context.severities.find((x) => String(x.id) === severity)?.code ===
          "CRITICAL" && (
          <p>
            Phân loại nghiêm trọng sẽ gửi cảnh báo đến trưởng bảo trì và quản lý
            vận hành có quyền; người nhận cần xác nhận đã đọc.
          </p>
        )}
        {message && <p role="status">{userMessage(message)}</p>}
        <button
          data-dialog-action
          disabled={!valid || !changed || busy || attempted}
          onClick={() => void save()}
        >
          Lưu phân loại
        </button>
        <button
          disabled={busy || loading}
          onClick={() => setRevision((v) => v + 1)}
        >
          Tải lại phân loại
        </button>
      </section>
    </ActionDialog>
  );
}
