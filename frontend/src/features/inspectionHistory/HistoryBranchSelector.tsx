import { userMessage } from "../../i18n/userMessage";
import { SearchableSelect } from "../../components/SearchableSelect";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import "../../screens/housekeeping/operational-read.css";

type Branch = { id: number; name: string; isPrimary: boolean };
export function HistoryBranchSelector({
  selected = "",
  autoSelect = false,
  assignment = false,
}: {
  selected?: string;
  autoSelect?: boolean;
  assignment?: boolean;
}) {
  const { state, actions } = useAuth();
  const navigate = useNavigate();
  const destination = assignment ? "/inspections/assignment" : "/inspection-history";
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [revision, retry] = useState(0);
  const [result, setResult] = useState<{
    owner: typeof owner;
    items?: Branch[];
    error?: string;
  }>();
  useEffect(() => {
    const abort = new AbortController();
    setResult(undefined);
    if (owner)
      void (async () => {
        try {
          const response = await fetch(assignment ? "/api/inspections/assignment-branches" : "/api/inspection-history/branches", {
            credentials: "same-origin",
            cache: "no-store",
            redirect: "error",
            signal: abort.signal,
          });
          if (response.status === 401) {
            if (!abort.signal.aborted) actions.invalidateSession(owner);
            return;
          }
          if (!response.ok) throw Error();
          const items: Branch[] = await response.json();
          if (
            !Array.isArray(items) ||
            items.some(
              (b) =>
                !b ||
                !Number.isSafeInteger(b.id) ||
                b.id <= 0 ||
                typeof b.name !== "string" ||
                typeof b.isPrimary !== "boolean",
            )
          )
            throw Error();
          if (!abort.signal.aborted) {
            setResult({ owner, items });
            const preferred =
              items.find((b) => b.isPrimary) ??
              (items.length === 1 ? items[0] : null);
            if (autoSelect && preferred)
              navigate(`${destination}?branchId=${preferred.id}`, {
                replace: true,
              });
          }
        } catch {
          if (!abort.signal.aborted)
            setResult({
              owner,
              error: "Chưa tải được chi nhánh được phép truy cập.",
            });
        }
      })();
    return () => abort.abort();
  }, [owner, actions, revision, autoSelect, navigate, assignment, destination]);
  const current = owner && result?.owner === owner ? result : null;
  return (
    <div className="ops-card ops-branch">
      <label>
        Chi nhánh
        <SearchableSelect
          aria-label="Chi nhánh"
          value={selected}
          disabled={!current?.items?.length}
          onChange={(e) =>
            navigate(`${destination}?branchId=${e.target.value}`)
          }
        >
          <option value="">Chọn chi nhánh</option>
          {current?.items?.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </SearchableSelect>
      </label>
      {!current && <p role="status">Đang tải chi nhánh…</p>}
      {current?.items?.length === 0 && (
        <p>Chưa có chi nhánh được cấp quyền sử dụng chức năng này.</p>
      )}
      {current?.error && (
        <p role="alert">
          {userMessage(current.error)}{" "}
          <button onClick={() => retry((v) => v + 1)}>Thử lại</button>
        </p>
      )}
    </div>
  );
}
