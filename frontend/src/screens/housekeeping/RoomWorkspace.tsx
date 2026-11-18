import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError, getJson } from "../../api/dev1/client";
import { listRooms, type RoomDirectoryItem } from "../../api/dev1/rooms";
import { LoadingIndicator } from "../../components/LoadingIndicator";
import { displayCode } from "../../i18n/vi";
import "./room-workspace.css";

type Branch = {
  id: number;
  name: string;
  isPrimary: boolean;
  canViewRooms: boolean;
  canMonitor: boolean;
  canCreate: boolean;
  canReadOwn: boolean;
};
export function useRoomWorkspace(mode: "rooms" | "cleaning") {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [params, setParams] = useSearchParams();
  const requested = params.get("branchId") ?? "";
  const [revision, reload] = useState(0);
  const [result, setResult] = useState<{
    owner: typeof owner;
    items?: Branch[];
    error?: string;
  } | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    if (owner)
      void getJson("/rooms/workspace-options", abort.signal)
        .then((v) => {
          if (
            !Array.isArray(v) ||
            v.some(
              (b) =>
                !b ||
                !Number.isSafeInteger(b.id) ||
                b.id <= 0 ||
                typeof b.name !== "string" ||
                [
                  b.isPrimary,
                  b.canViewRooms,
                  b.canMonitor,
                  b.canCreate,
                  b.canReadOwn,
                ].some((x) => typeof x !== "boolean"),
            )
          )
            throw new Error();
          if (!abort.signal.aborted) setResult({ owner, items: v });
        })
        .catch((e) => {
          if (!abort.signal.aborted) {
            if (e instanceof ApiError && e.status === 401)
              actions.invalidateSession(owner);
            else
              setResult({
                owner,
                error: "Chưa tải được chi nhánh làm việc. Vui lòng thử lại.",
              });
          }
        });
    return () => abort.abort();
  }, [owner, actions, revision]);
  const visible = owner && result?.owner === owner ? result : null;
  const items = visible?.items?.filter((b) =>
    mode === "rooms" ? b.canViewRooms : b.canMonitor || b.canReadOwn,
  );
  const branch =
    items?.find((b) => String(b.id) === requested) ??
    (!requested
      ? (items?.find((b) => b.isPrimary) ??
        (items?.length === 1 ? items[0] : undefined))
      : undefined);
  const query = params.toString();
  useEffect(() => {
    if (branch && !requested) {
      const next = new URLSearchParams(query);
      next.set("branchId", String(branch.id));
      setParams(next, { replace: true });
    }
  }, [branch?.id, requested, query, setParams]);
  const error =
    visible?.error ||
    (requested && items && !branch
      ? "Chi nhánh trong đường dẫn không thuộc phạm vi được phép. Hãy chọn chi nhánh làm việc."
      : undefined);
  return {
    owner,
    items,
    branch,
    error,
    reload: () => reload((v) => v + 1),
    select: (id: string) => {
      const next = new URLSearchParams();
      if (id) next.set("branchId", id);
      if (params.get("view") === "mine") next.set("view", "mine");
      setParams(next);
    },
  };
}
export function RoomBranchField({
  scope,
}: {
  scope: ReturnType<typeof useRoomWorkspace>;
}) {
  return (
    <section className="room-scope" aria-label="Chi nhánh làm việc">
      {scope.error && (
        <p role="alert">
          {scope.error}{" "}
          <button type="button" onClick={scope.reload}>
            Thử lại
          </button>
        </p>
      )}
      {!scope.items && !scope.error ? (
        <LoadingIndicator compact label="Đang tải chi nhánh" />
      ) : scope.items?.length === 0 ? (
        <p>Bạn chưa có quyền sử dụng chức năng này tại chi nhánh nào.</p>
      ) : scope.items?.length === 1 && scope.branch ? (
        <>
          <span>Chi nhánh làm việc</span>
          <strong>{scope.branch.name}</strong>
        </>
      ) : (
        scope.items && (
          <label>
            Chi nhánh làm việc
            <select
              aria-label="Chọn chi nhánh làm việc"
              value={scope.branch?.id ?? ""}
              onChange={(e) => scope.select(e.target.value)}
            >
              <option value="">Chọn chi nhánh</option>
              {scope.items.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </label>
        )
      )}
    </section>
  );
}
export function useRoomChoices(scope: ReturnType<typeof useRoomWorkspace>) {
  const branch = scope.branch?.canViewRooms ? String(scope.branch.id) : "",
    owner = scope.owner;
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<{
    branch: string;
    owner: typeof owner;
    items?: RoomDirectoryItem[];
    error?: string;
  } | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    if (branch && owner)
      void (async () => {
        const items: RoomDirectoryItem[] = [];
        for (let page = 1; !abort.signal.aborted; page++) {
          const data = await listRooms(branch, page, "", "", abort.signal);
          items.push(...data.items);
          if (page * data.pageSize >= data.totalCount) break;
        }
        if (!abort.signal.aborted)
          setResult({
            branch,
            owner,
            items: [...new Map(items.map((r) => [r.id, r])).values()],
          });
      })().catch(() => {
        if (!abort.signal.aborted)
          setResult({ branch, owner, error: "Chưa tải được danh sách phòng." });
      });
    return () => abort.abort();
  }, [branch, owner, retry]);
  const visible =
    result?.branch === branch && result?.owner === owner ? result : null;
  return {
    items: visible?.items,
    error: visible?.error,
    reload: () => setRetry((v) => v + 1),
  };
}
export function NamedFilter({
  name,
  label,
  items,
  value = "",
}: {
  name: string;
  label: string;
  items: { id: number; name: string }[];
  value?: string;
}) {
  return (
    <label>
      {label}
      <select aria-label={label} name={name} defaultValue={value}>
        <option value="">Tất cả</option>
        {items.map((o) => (
          <option key={o.id} value={o.id}>
            {roomLabel(o.name)}
          </option>
        ))}
      </select>
    </label>
  );
}
export function RoomNumberField({
  items,
  selected,
  onSelect,
}: {
  items: RoomDirectoryItem[];
  selected: string;
  onSelect: (id: string) => void;
}) {
  const [text, setText] = useState(
    items.find((r) => String(r.id) === selected)?.roomNumber ?? "",
  );
  const exact = items.find((r) => r.roomNumber === text.trim());
  return (
    <label>
      Số phòng
      <input
        aria-label="Số phòng"
        type="search"
        list="turnover-rooms"
        placeholder="Nhập số phòng, ví dụ 203"
        value={text}
        required
        onChange={(e) => {
          const value = e.target.value;
          setText(value);
          const match = items.find((r) => r.roomNumber === value.trim());
          if (match) onSelect(String(match.id));
        }}
        onInvalid={(e) =>
          e.currentTarget.setCustomValidity("Chọn một phòng trong danh sách.")
        }
        onInput={(e) => e.currentTarget.setCustomValidity("")}
      />
      <input type="hidden" name="roomId" value={exact?.id ?? ""} />
      <datalist id="turnover-rooms">
        {items
          .filter((r) => r.roomNumber.startsWith(text.trim()))
          .map((r) => (
            <option key={r.id} value={r.roomNumber}>
              {r.floor.name}
            </option>
          ))}
      </datalist>
    </label>
  );
}
export function roomLabel(value: unknown): string {
  const labels: Record<string, string> = {
    CLEANING_IN_PROGRESS: "Đang vệ sinh",
    REWORK_REQUIRED: "Cần làm lại",
    cleaning: "Vệ sinh",
    rework: "Làm lại",
    CHECKOUT: "Sau trả phòng",
    STAYOVER: "Phòng có khách",
    DEEP_CLEANING: "Tổng vệ sinh",
    CONDITIONAL: "Đạt có điều kiện",
  };
  return labels[String(value)] ?? displayCode(value);
}
export const hotelTime = (value: string | null | undefined) =>
  !value || !Number.isFinite(Date.parse(value))
    ? "—"
    : new Intl.DateTimeFormat("vi-VN", {
        timeZone: "Asia/Ho_Chi_Minh",
        dateStyle: "short",
        timeStyle: "short",
      }).format(
        new Date(/[zZ]$|[+-]\d{2}:\d{2}$/.test(value) ? value : value + "Z"),
      );
export const hotelDate = (value: string) =>
  value
    ? new Intl.DateTimeFormat("vi-VN", {
        timeZone: "Asia/Ho_Chi_Minh",
        dateStyle: "short",
      }).format(new Date(value))
    : "—";
export const uniqueOptions = (items: { id: number; name: string }[]) => [
  ...new Map(items.map((i) => [i.id, i])).values(),
];
