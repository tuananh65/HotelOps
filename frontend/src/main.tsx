import React from "react";
import { createRoot } from "react-dom/client";
import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
  useLocation,
  useParams,
} from "react-router-dom";
import { ScreenBoundary } from "./components/ScreenBoundary";
import { domainFor } from "./domain";
import { t, useLanguage } from "./i18n";
import { DesignSystem } from "./routes/DesignSystem";
import { requiredRoutes } from "./routes/routeConfig";
import { ScreenIndex } from "./routes/ScreenIndex";
import * as service from "./services";
import "./styles.css";
import { Shell, Toast, useDB } from "./ui";
import "./styles/operations.css";
import { initializeTheme } from "./components/ThemeToggle";
import "./styles/theme.css";
initializeTheme();
function Screen({ id }: { id: number }) {
  useDB();
  const params = useParams();
  const location = useLocation();
  if (id === 1 || id === 2) {
    const Component = requiredRoutes[id - 1].Component;
    return <Component />;
  }
  if (params.id) {
    const key =
      id === 50 ? "assets" : id === 36 ? "inspections" : domainFor(id).key;
    const resolved = service.resolveDemoRecordId(key, params.id);
    if (resolved !== params.id)
      return (
        <Navigate
          replace
          to={
            location.pathname.replace(
              encodeURIComponent(params.id),
              encodeURIComponent(resolved),
            ) + location.search
          }
        />
      );
  }
  const Component = requiredRoutes.find((s) => s.id === id)!.Component;
  const content = (
    <ScreenBoundary key={id}>
      <Component />
    </ScreenBoundary>
  );
  return id === 3 ? content : <Shell screen={id}>{content}</Shell>;
}
function App() {
  useLanguage();
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/dev/screens" replace />} />
        {requiredRoutes.map((s) => (
          <Route
            key={s.id}
            path={s.route}
            element={<Screen key={s.id} id={s.id} />}
          />
        ))}
        {<Route path="/dev/screens" element={<ScreenIndex />} />}
        <Route path="/dev/design-system" element={<DesignSystem />} />
        <Route
          path="*"
          element={
            <div className="empty">
              <h1>{t("Page not found")}</h1>
              <a href="/">{t("Return to HotelOps")}</a>
            </div>
          }
        />
      </Routes>
      <Toast />
    </BrowserRouter>
  );
}
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
