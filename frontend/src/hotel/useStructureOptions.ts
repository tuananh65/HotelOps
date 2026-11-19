import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { object, safeId } from "./contracts";

export type StructureOption = {
  id: number;
  name: string;
  code: string;
  floorId?: number | null;
};
export type OptionKind = "branches" | "floors" | "areas" | "roomTypes";

// Read only, scoped to the authenticated session and selected branch.
export function useStructureOptions(
  kind: OptionKind,
  branch = "",
  enabled = true,
) {
  const { state } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.session
      : null;
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    items?: StructureOption[];
    error?: string;
  }>();
  const key = `${kind}:${branch}:${revision}`;
  const available =
    enabled && !!owner && (kind === "branches" || !!safeId(Number(branch)));
  useEffect(() => {
    if (!available) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const items: StructureOption[] = [];
        let page = 1,
          total = 0,
          seen = 0;
        do {
          const path =
            kind === "branches"
              ? "/api/branches/structure-options"
              : kind === "roomTypes"
                ? `/api/room-types?branchId=${branch}&isActive=true&page=${page}&pageSize=100&sort=code`
                : `/api/branches/${branch}/structure?nodeType=branch&collection=${kind}&isActive=true&page=${page}&pageSize=100`;
          const response = await fetch(path, {
            signal: abort.signal,
            credentials: "include",
            cache: "no-store",
            redirect: "error",
            headers: { Accept: "application/json" },
          });
          if (!response.ok)
            throw Error(
              response.status === 403
                ? "Bạn chưa có quyền xem danh sách này."
                : "Không tải được danh sách. Vui lòng thử lại.",
            );
          const data: unknown = await response.json();
          const collection =
            kind === "branches"
              ? null
              : object(data) && (kind === "floors" || kind === "areas")
                ? data[kind]
                : data;
          const rows =
            kind === "branches"
              ? data
              : object(collection)
                ? collection.items
                : null;
          if (!Array.isArray(rows))
            throw Error("Danh sách trả về không hợp lệ.");
          total =
            kind === "branches"
              ? rows.length
              : object(collection)
                ? Number(collection.totalCount ?? collection.total)
                : NaN;
          if (!Number.isSafeInteger(total) || total < 0 || total > 10000)
            throw Error(
              "Danh sách quá lớn hoặc không hợp lệ. Vui lòng thu hẹp phạm vi.",
            );
          for (const row of rows) {
            const value =
              kind === "roomTypes" && object(row) ? row.details : row;
            if (
              !object(value) ||
              !safeId(value.id) ||
              typeof value.name !== "string" ||
              typeof value.code !== "string"
            )
              throw Error("Thông tin lựa chọn không hợp lệ.");
            if (value.isActive === false || value.active === false) continue;
            if (items.some((x) => x.id === value.id))
              throw Error("Danh sách đã thay đổi. Vui lòng tải lại.");
            items.push({
              id: value.id as number,
              name: value.name,
              code: value.code,
              ...(kind === "areas"
                ? { floorId: value.floorId as number | null }
                : {}),
            });
          }
          seen += rows.length;
          if (!rows.length && seen < total)
            throw Error("Chưa tải đủ danh sách. Vui lòng thử lại.");
          page++;
        } while (seen < total && !abort.signal.aborted);
        if (!abort.signal.aborted) setResult({ owner, key, items });
      } catch (error) {
        if (!abort.signal.aborted)
          setResult({
            owner,
            key,
            error:
              error instanceof Error
                ? error.message
                : "Không tải được danh sách.",
          });
      }
    })();
    return () => abort.abort();
  }, [owner, key, kind, branch, available]);
  const visible =
    available && result?.owner === owner && result.key === key
      ? result
      : undefined;
  return {
    items: visible?.items,
    error: visible?.error,
    loading: available && !visible,
    reload: () => setRevision((n) => n + 1),
  };
}
