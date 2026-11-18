import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

export type UserOption = {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
};
type Result = { items: UserOption[]; error?: string };
const object = (value: unknown): value is Record<string, unknown> =>
  !!value && typeof value === "object" && !Array.isArray(value);
function decode(value: unknown, role = false): UserOption {
  if (!object(value)) throw new Error();
  const id = role ? value.roleId : value.id;
  const code = role ? value.roleCode : value.code;
  const name = role ? value.roleName : value.name;
  if (
    typeof id !== "number" ||
    !Number.isSafeInteger(id) ||
    id <= 0 ||
    typeof code !== "string" ||
    typeof name !== "string" ||
    typeof value.isActive !== "boolean"
  )
    throw new Error();
  return { id, code, name, isActive: value.isActive };
}

/** Lookup data is never used to grant permission; each API enforces its own scope. */
export function useUserOptions(
  kind: "branches" | "departments" | "roles",
  branchId = "",
  activeOnly = false,
) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" ? state.authorityGeneration : null;
  const key = `${kind}:${branchId}:${activeOnly}`;
  const [revision, setRevision] = useState(0);
  const [completed, setCompleted] = useState<{
    owner: typeof owner;
    key: string;
    revision: number;
    result: Result;
  } | null>(null);
  const enabled = kind !== "departments" || /^[1-9]\d*$/.test(branchId);
  useEffect(() => {
    if (!owner || !enabled) return;
    const abort = new AbortController();
    void Promise.resolve().then(async () => {
      if (abort.signal.aborted) return;
      try {
        const items: UserOption[] = [];
        for (let page = 1; ; page++) {
          const url =
            kind === "branches"
              ? "/api/branches/structure-options"
              : kind === "roles"
                ? `/api/roles?page=${page}&pageSize=100${activeOnly ? "&isActive=true" : ""}`
                : `/api/branches/${branchId}/structure?collection=departments&page=${page}&pageSize=100${activeOnly ? "&isActive=true" : ""}`;
          const response = await fetch(url, {
            method: "GET",
            credentials: "include",
            cache: "no-store",
            redirect: "error",
            signal: abort.signal,
          });
          if (response.status === 401) {
            actions.invalidateSession(owner);
            return;
          }
          if (!response.ok) throw new Error();
          const body: unknown = await response.json();
          const collection =
            kind === "departments" && object(body) ? body.departments : body;
          const rows =
            kind === "branches"
              ? collection
              : object(collection)
                ? collection.items
                : null;
          if (!Array.isArray(rows)) throw new Error();
          items.push(...rows.map((row) => decode(row, kind === "roles")));
          if (kind === "branches") break;
          const total = object(collection)
            ? kind === "roles"
              ? collection.total
              : collection.totalCount
            : null;
          if (
            typeof total !== "number" ||
            !Number.isSafeInteger(total) ||
            total < 0 ||
            (rows.length === 0 && items.length < total)
          )
            throw new Error();
          if (items.length >= total) break;
        }
        if (!abort.signal.aborted)
          setCompleted({
            owner,
            key,
            revision,
            result: {
              items: activeOnly ? items.filter((item) => item.isActive) : items,
            },
          });
      } catch {
        if (!abort.signal.aborted)
          setCompleted({
            owner,
            key,
            revision,
            result: {
              items: [],
              error:
                "Không tải được danh sách trong phạm vi quyền của bạn. Vui lòng thử lại.",
            },
          });
      }
    });
    return () => abort.abort();
  }, [owner, actions, kind, branchId, activeOnly, key, revision, enabled]);
  const result =
    completed?.owner === owner &&
    completed.key === key &&
    completed.revision === revision
      ? completed.result
      : null;
  return {
    items: result?.items ?? [],
    error: result?.error,
    loading: !!owner && enabled && !result,
    reload: () => setRevision((value) => value + 1),
  };
}
