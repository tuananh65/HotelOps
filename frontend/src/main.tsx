import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./auth/AuthProvider";
import { AuthRouteHost } from "./auth/AuthRouteHost";
import "./styles.css";
import "./styles/operations.css";

// Use the shared authenticated route tree instead of the unprotected operations shell.
// The server still enforces cookie/session, CSRF and branch-scoped authorization.
const host = <AuthRouteHost />;

createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider fallback={host}>{host}</AuthProvider>
    </BrowserRouter>
  </React.StrictMode>,
);
