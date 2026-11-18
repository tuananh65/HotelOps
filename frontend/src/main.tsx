import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./auth/AuthProvider";
import { AuthRouteHost } from "./auth/AuthRouteHost";
import { CacaoShell } from "./shared-host/CacaoShell";
import "./styles.css";
import "./styles/operations.css";

// Use the shared authenticated route tree instead of the unprotected operations shell.
// The server still enforces cookie/session, CSRF and branch-scoped authorization.
const host = (
  <CacaoShell>
    <AuthRouteHost />
  </CacaoShell>
);

createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider fallback={host}>{host}</AuthProvider>
    </BrowserRouter>
  </React.StrictMode>,
);

import "./inventory/inventory.css";
import "./dev2/dev2.css";
import "./styles/cacao.css";
import "./styles/design-system.css";
