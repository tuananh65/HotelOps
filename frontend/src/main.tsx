import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./auth/AuthProvider";
import { AuthRouteHost } from "./auth/AuthRouteHost";
import "./styles.css";
import "./styles/operations.css";
import "./styles/theme.css";

// The exact same host occupies the provider's single child position in both branches.
// AuthRouteHost enforces identity boundaries even when rendered as the fallback.
const host = <AuthRouteHost />;
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider fallback={host}>{host}</AuthProvider>
    </BrowserRouter>
  </React.StrictMode>,
);
