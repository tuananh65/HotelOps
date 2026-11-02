import { useEffect, useState } from "react";
import { useLocation } from "react-router-dom";
import * as service from "../services";

/** Route controller: cancel obsolete reads and prevent rendering stale/partial screen data. */
export function useApiScreen(screenId: number, recordId?: string) {
  const branch = service.branchContext();
  const search = useLocation().search;
  const key = `${screenId}:${recordId ?? ""}:${branch}:${search}`;
  const [attempt, retry] = useState(0);
  const [state, setState] = useState<{
    key: string;
    loading: boolean;
    error?: Error;
  }>({ key: "", loading: true });
  useEffect(() => {
    const controller = new AbortController();
    setState({ key, loading: true });
    service.loadScreen(screenId, recordId, controller.signal, search).then(
      () => {
        if (!controller.signal.aborted) setState({ key, loading: false });
      },
      (error: Error) => {
        if (!controller.signal.aborted)
          setState({ key, loading: false, error });
      },
    );
    return () => controller.abort();
  }, [key, attempt]);
  return {
    ...state,
    loading: state.key !== key || state.loading,
    retry: () => retry((value) => value + 1),
  };
}
