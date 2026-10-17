import React from "react";
import { createRoot } from "react-dom/client";
import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
  useParams,
} from "react-router-dom";
import { ScreenBoundary } from "./components/ScreenBoundary";
import { SessionGate, ScreenDataGate } from "./components/ApiGate";
import { t, useLanguage } from "./i18n";
import { DesignSystem } from "./routes/DesignSystem";
import { requiredRoutes } from "./routes/routeConfig";
import { ScreenIndex } from "./routes/ScreenIndex";
import "./styles.css";
import { Shell, Toast, useDB } from "./ui";
import "./styles/operations.css";
import { initializeTheme } from "./components/ThemeToggle";
import "./styles/theme.css";
initializeTheme();
function Screen({ id }: { id: number }) {
  useDB();
  const params = useParams();
  if (id === 1 || id === 2) {
    const Component = requiredRoutes[id - 1].Component;
    return <Component />;
  }
  const Component = requiredRoutes.find((s) => s.id === id)!.Component;
  const content = (
    <ScreenBoundary key={id}>
      <ScreenDataGate screenId={id} recordId={params.id}>
        <Component />
      </ScreenDataGate>
    </ScreenBoundary>
  );
  return (
    <SessionGate screenId={id}>
      {id === 3 ? content : <Shell screen={id}>{content}</Shell>}
    </SessionGate>
  );
}
function App() {
  useLanguage();
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/profile" replace />} />
        {requiredRoutes.map((s) => (
          <Route
            key={s.id}
            path={s.route}
            element={<Screen key={s.id} id={s.id} />}
          />
        ))}
        {
          <Route
            path="/dev/screens"
            element={
              <SessionGate>
                <ScreenIndex />
              </SessionGate>
            }
          />
        }
        <Route
          path="/dev/design-system"
          element={
            <SessionGate>
              <DesignSystem />
            </SessionGate>
          }
        />
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
