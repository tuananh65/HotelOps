import { useLayoutEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { PublicSession } from "../auth/authStore";
import { createHotelApi } from "./hotelApi";
import type {
  Context,
  Filters,
  HotelError,
  MutationKind,
  MutationRequests,
  ReadKind,
  ReadTypes,
} from "./contracts";
export interface HotelSnapshot {
  owner: PublicSession;
  key: string;
  revision: number;
  data: ReadTypes[ReadKind];
  etag: string | null;
}
export function hotelErrorMessage(error: HotelError) {
  if (error.reason === "DUPLICATE") return "MSG35: Mã hoặc số này đã tồn tại trong phạm vi đã chọn.";
  if (error.reason === "HEAD_ADMIN_REQUIRED") return "Cần vai trò quản trị viên để điều chuyển trưởng bộ phận.";
  if (error.reason === "HEAD_SCHEMA_REQUIRED") return "Cần cài đặt và xác minh cấu trúc dữ liệu điều chuyển trưởng bộ phận.";
  if (error.reason === "HEAD_ROLE_OR_ACCESS_REQUIRED") return "Trưởng bộ phận đã chọn cần tài khoản hoạt động, vai trò DEPT_MANAGER và quyền chi nhánh.";
  if (error.reason === "HEAD_DEPARTMENT_REQUIRED") return "Trưởng bộ phận đã chọn cần phân công thành viên rõ ràng còn hiệu lực trong bộ phận. Với bộ phận mới, tạo chưa có trưởng, cấu hình thành viên rồi gán trưởng.";
  if (error.reason === "HEAD_MISSING_IR_PROCESS") return "Trưởng bộ phận đã chọn cần IR_PROCESS trong chi nhánh.";
  if (error.reason === "HEAD_MISSING_MR_APPROVE_L1") return "Trưởng bộ phận đã chọn cần MR_APPROVE_L1 trong chi nhánh.";
  if (error.reason === "HEAD_MISSING_MR_APPROVE_FALLBACK") return "Trưởng bộ phận đã chọn cần MR_APPROVE_FALLBACK cho bộ phận của kho này.";
  if (error.reason === "HEAD_MAPPING_NOT_EFFECTIVE") return "Trưởng bộ phận đã chọn có phân công hiệu lực tương lai. Cần xử lý thời điểm kích hoạt trước khi điều chuyển.";
  if (error.reason === "HEAD_MAPPING_PROVENANCE_REQUIRED") return "Chưa xác định nguồn gốc phân công thẩm quyền hiện có. Đối soát nguồn trước khi điều chuyển trưởng bộ phận.";
  if (error.reason === "INVALIDREFERENCE") return "MSG03: Tham chiếu đã chọn không còn khả dụng hoặc đủ điều kiện. Kiểm tra chi nhánh và trạng thái hoạt động hiện tại.";
  if (error.reason === "STRUCTURE_CODE_EXISTS") return "MSG35: Mã tầng hoặc khu vực đã tồn tại trong chi nhánh. Chọn mã khác.";
  if (error.reason === "PARENT_FLOOR_UNAVAILABLE") return "MSG03: Chọn tầng cha đang hoạt động trong chi nhánh này.";
  if (error.reason === "PARENT_CHANGE_CONFLICT") return "MSG03: Đổi mục cha xung đột với phòng đang liên kết khu vực này.";
  if (error.reason === "PASSWORD_CHANGE_REQUIRED")
    return "Cần kiểm tra phiên trước khi tiếp tục.";
  if (error.reason === "ACTIVE_ROOMS_EXIST")
    return (
      "Loại phòng vẫn có phòng đang hoạt động. Xử lý ràng buộc trước khi vô hiệu hóa." +
      (error.activeRooms?.length
        ? "Phòng bị ảnh hưởng:" +
          error.activeRooms
            .map((v) => `${v.roomNumber} (#${v.id})`)
            .join(", ") +
          "."
        : "")
    );
  if (error.reason === "ROOM_TYPE_SCHEMA_REQUIRED")
    return "Chưa có cấu trúc dữ liệu loại phòng bắt buộc ở backend. Liên hệ người phụ trách tích hợp.";
  switch (error.status) {
    case 400:
      return "Máy chủ từ chối dữ liệu hoặc kiểm tra bảo mật. Kiểm tra trường trước lần thử mới.";
    case 401:
      return "Cần kiểm tra phiên. Không gửi lại yêu cầu nào.";
    case 403:
      return "Không có quyền truy cập. Phản hồi này không xác nhận hồ sơ có tồn tại hay không.";
    case 404:
      return "Không tìm thấy hồ sơ yêu cầu.";
    case 409:
      return "Máy chủ từ chối do trùng, ngừng hoạt động hoặc xung đột ràng buộc. Chưa xác nhận thay đổi.";
    case 412:
    case 428:
      return "Cần phiên bản mới. Tải lại hồ sơ, kiểm tra giá trị rồi xác nhận lần thử mới.";
    case 503:
      return "Dịch vụ chưa khả dụng. Không thay bằng dữ liệu giả lập.";
  }
  return error.kind === "invalid-request"
    ? "Kiểm tra chi nhánh, mã hồ sơ và các trường bắt buộc."
    : "Không xác minh an toàn phản hồi. Không hiển thị lỗi máy chủ nguyên bản.";
}
export const unknownHotelOutcome =
  "Chưa xác nhận thay đổi; có thể đã lưu. Không tự gửi lại. Nhờ quản trị viên kiểm tra hồ sơ trước khi thay đổi tiếp.";
export function useHotelFlow(key: string) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" ? state.session : null;
  const current = useRef({ owner, key });
  current.current = { owner, key };
  const prior = useRef({ owner, key });
  const [api] = useState(() => createHotelApi());
  const [snapshot, setSnapshot] = useState<HotelSnapshot | null>(null);
  const [phase, setPhase] = useState<
    | "idle"
    | "reading"
    | "pending"
    | "confirmed"
    | "unknown"
    | "stale"
    | "denied"
  >("idle");
  const phaseRef = useRef(phase);
  const [message, setMessage] = useState("");
  const [receipt, setReceipt] = useState<unknown>(null);
  const mounted = useRef(false),
    busy = useRef(false),
    recovered = useRef(false),
    unknown = useRef(false);
  const generation = useRef(0),
    revision = useRef(0);
  const readAbort = useRef<AbortController | null>(null),
    writeAbort = useRef<AbortController | null>(null);
  const usable = useRef<HotelSnapshot | null>(null);
  function publish(next: typeof phase) {
    phaseRef.current = next;
    setPhase(next);
  }
  useLayoutEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      ++generation.current;
      readAbort.current?.abort();
      writeAbort.current?.abort();
    };
  }, []);
  useLayoutEffect(() => {
    if (prior.current.owner === owner && prior.current.key === key) return;
    if (busy.current) unknown.current = true;
    ++generation.current;
    readAbort.current?.abort();
    writeAbort.current?.abort();
    busy.current = false;
    usable.current = null;
    setSnapshot(null);
    setReceipt(null);
    setMessage("");
    recovered.current = false;
    publish(unknown.current ? "unknown" : "idle");
    prior.current = { owner, key };
  }, [owner, key]);
  const visible =
    owner && snapshot?.owner === owner && snapshot.key === key
      ? snapshot
      : null;
  usable.current = visible;
  const valid = (n: number) =>
    mounted.current &&
    n === generation.current &&
    current.current.owner === owner &&
    current.current.key === key;
  function recover(e: HotelError) {
    if (
      (e.status === 401 || e.reason === "PASSWORD_CHANGE_REQUIRED") &&
      !recovered.current
    ) {
      recovered.current = true;
      void actions.revalidate();
    }
  }
  async function load(kind: ReadKind, context: Context, filters: Filters) {
    if (!owner || busy.current || phaseRef.current === "confirmed") return;
    readAbort.current?.abort();
    const abort = new AbortController(),
      n = ++generation.current;
    readAbort.current = abort;
    usable.current = null;
    setSnapshot(null);
    setMessage("");
    publish("reading");
    const result = await api.read(kind, context, filters, abort.signal);
    if (!valid(n)) return;
    if (result.ok) {
      setSnapshot({
        owner,
        key,
        revision: ++revision.current,
        data: result.data,
        etag: result.etag,
      });
      publish(unknown.current ? "unknown" : "idle");
    } else {
      setMessage(hotelErrorMessage(result.error));
      publish(
        unknown.current
          ? "unknown"
          : result.error.status === 403
            ? "denied"
            : "idle",
      );
      recover(result.error);
    }
  }
  async function save<K extends MutationKind>(
    kind: K,
    context: Context,
    request: MutationRequests[K],
    reviewed: HotelSnapshot | null,
  ) {
    const needsSnapshot = !kind.startsWith("create");
    if (
      !mounted.current ||
      !owner ||
      current.current.owner !== owner ||
      current.current.key !== key ||
      busy.current ||
      unknown.current ||
      ["confirmed", "stale", "denied"].includes(phaseRef.current) ||
      (needsSnapshot &&
        (!reviewed ||
          usable.current !== reviewed ||
          reviewed.owner !== owner ||
          reviewed.key !== key ||
          !reviewed.etag))
    )
      return;
    readAbort.current?.abort();
    const abort = new AbortController(),
      n = ++generation.current;
    writeAbort.current = abort;
    busy.current = true;
    publish("pending");
    setMessage("");
    const result = await api.mutate(
      kind,
      context,
      request,
      reviewed?.etag,
      abort.signal,
    );
    if (!valid(n)) return;
    busy.current = false;
    writeAbort.current = null;
    if (result.outcome === "CONFIRMED") {
      usable.current = null;
      setSnapshot(null);
      setReceipt(result.data);
      publish("confirmed");
    } else if (result.outcome === "OUTCOME_UNKNOWN") {
      unknown.current = true;
      usable.current = null;
      setSnapshot(null);
      publish("unknown");
      recover(result.error);
    } else {
      const e = result.error;
      if ([401, 403, 404, 412, 428].includes(e.status ?? 0)) {
        usable.current = null;
        setSnapshot(null);
      }
      setMessage(
        (result.outcome === "NOT_DISPATCHED" ? "Không gửi thao tác ghi dữ liệu." : "") +
          hotelErrorMessage(e),
      );
      publish(
        e.status === 403
          ? "denied"
          : e.status === 412 || e.status === 428
            ? "stale"
            : "idle",
      );
      recover(e);
    }
  }
  return {
    snapshot: visible,
    phase,
    message,
    receipt: owner ? receipt : null,
    load,
    save,
    unknown: unknown.current,
  };
}
