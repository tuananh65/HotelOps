import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "../auth/AuthProvider";
import { HotelRouteHost } from "./HotelRouteHost";
import "../styles.css";
import "../styles/operations.css";
import "../styles/theme.css";
import "./hotel.css";
const host = <HotelRouteHost />;
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider fallback={host}>{host}</AuthProvider>
    </BrowserRouter>
  </React.StrictMode>,
);
