import { useEffect, type ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { isInventoryScreen } from "../controllers/inventoryController";
import { ApiError } from "../api/http";
import { useApiScreen } from "../controllers/useApiScreen";
import { t } from "../i18n";
import * as service from "../services";
import { useDB } from "../ui";

export function ApiState({
  loading,
  error,
  retry,
}: {
  loading?: boolean;
  error?: Error;
  retry?: () => void;
}) {
  return (
    <section
      className="empty"
      role={error ? "alert" : "status"}
      aria-busy={loading || undefined}
    >
      <h2>
        {t(
          loading
            ? "Loading…"
            : error instanceof ApiError && error.kind === "forbidden"
              ? "You do not have permission to perform this action."
              : error instanceof ApiError && error.kind === "notFound"
                ? "Record unavailable"
                : "Unable to load data. Please try again.",
        )}
      </h2>
      {error && <p>{t(error.message)}</p>}
      {retry && (
        <button className="primary" onClick={retry}>
          {t("Retry")}
        </button>
      )}
    </section>
  );
}
export function SessionGate({
  children,
  screenId,
}: {
  children: ReactNode;
  screenId?: number;
}) {
  useDB();
  const location = useLocation();
  const auth = service.authentication();
  useEffect(() => {
    void service.restoreSession();
  }, []);
  if (auth.state === "initial" || auth.state === "loading")
    return <ApiState loading />;
  if (auth.state === "error")
    return (
      <ApiState
        error={auth.error ?? undefined}
        retry={() => void service.restoreSession()}
      />
    );
  if (!service.currentSession())
    return (
      <Navigate
        replace
        to="/login"
        state={{ from: location.pathname + location.search }}
      />
    );
  if (service.currentSession()?.restricted && screenId !== 3)
    return <Navigate replace to="/change-password" />;
  if (
    screenId &&
    ![3, 4].includes(screenId) &&
    !service.canOpenScreen(screenId)
  )
    return (
      <ApiState
        error={
          new ApiError(
            "You do not have permission to perform this action.",
            403,
            "forbidden",
          )
        }
      />
    );
  return children;
}
export function ScreenDataGate({
  children,
  screenId,
  recordId,
}: {
  children: ReactNode;
  screenId: number;
  recordId?: string;
}) {
  useDB();
  const state = useApiScreen(screenId, recordId);
  if (state.loading || state.error) return <ApiState {...state} />;
  return (
    <>
      {isInventoryScreen(screenId) && (
        <p role="status">Chế độ chỉ đọc. Dữ liệu hiển thị theo quyền backend; chức năng ghi chưa được tích hợp. N/A là dữ liệu chưa có từ API.</p>
      )}
      {service.missingDataSources().length > 0 && (
        <p role="status">
          {t("Missing data")}: {service.missingDataSources().join(", ")}
        </p>
      )}
      {children}
    </>
  );
}
