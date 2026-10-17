import { Component, type ErrorInfo, type ReactNode } from "react";
import { t } from "../i18n";

export class ScreenBoundary extends Component<
  { children: ReactNode },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("HotelOps screen failed", error, info.componentStack);
  }
  render() {
    return this.state.failed ? (
      <section className="empty" role="alert">
        <h2>{t("This screen could not load")}</h2>
        <p>{t("Unable to display this screen. Reload to retry.")}</p>
        <button className="primary" onClick={() => window.location.reload()}>
          {t("Reload screen")}
        </button>
      </section>
    ) : (
      this.props.children
    );
  }
}
