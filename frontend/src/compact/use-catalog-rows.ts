import { useEffect, useState } from "react";
import { api, fields, type Page, type Row } from "./api";

export async function readCatalogRows(
  resource: string,
  branch: number,
  signal: AbortSignal,
) {
  const rows: Row[] = [];
  for (let page = 1; ; page++) {
    const result = await api<Page>(
      `catalog/${resource}?branchId=${branch}&page=${page}&pageSize=100`,
      undefined,
      "GET",
      signal,
    );
    rows.push(...fields(result.items));
    if (rows.length >= result.total || !result.items.length) return rows;
  }
}

// Fetch all pages before filtering by floor; a floor must not appear empty just
// because its rooms happen to be on another server page.
export function useCatalogRows(
  resource: string | null,
  branch: number,
  revision = 0,
) {
  const key = `${resource}:${branch}:${revision}`;
  const [state, set] = useState<{
    key?: string;
    data?: Row[];
    loading: boolean;
    error?: unknown;
  }>({ loading: !!resource });
  useEffect(() => {
    const controller = new AbortController();
    if (!resource) {
      set({ key, loading: false });
      return () => controller.abort();
    }
    set({ key, loading: true });
    (async () => {
      const rows = await readCatalogRows(resource, branch, controller.signal);
      if (!controller.signal.aborted) set({ key, data: rows, loading: false });
    })().catch((error) => {
      if (!controller.signal.aborted) set({ key, loading: false, error });
    });
    return () => controller.abort();
  }, [key, resource, branch]);
  return state.key === key ? state : { loading: !!resource };
}
