import { ThemeToggle } from "../components/ThemeToggle";
import { Link } from "react-router-dom";
import { LanguageSwitcher, t, useLanguage } from "../i18n";
import { Logo, StatusBadge } from "../ui";
const families = {
  White: [0],
  Navy: [500, 600, 700, 800, 900, 950],
  Blue: [50, 100, 500, 600, 700],
  Gray: [50, 100, 200, 300, 500, 600, 700, 900, 950],
  Green: [50, 100, 700],
  Amber: [50, 100, 700],
  Red: [50, 100, 700],
};
export function DesignSystem() {
  useLanguage();
  return (
    <main className="screen-index">
      <div className="library-nav">
        <Logo />
        <LanguageSwitcher />
        <ThemeToggle />
        <Link to="/dev/screens">{t("Screen library")}</Link>
      </div>
      <header className="library-hero">
        <span className="eyebrow">HOTELOPS · OPERATIONS DESIGN TOKENS</span>
        <h1>{t("Design system")}</h1>
        <p>HotelOps · React + TypeScript · Figma handoff</p>
      </header>
      <section className="panel">
        <h2>Enterprise interaction &amp; semantic roles</h2>
        <div className="swatch-grid">
          {[
            "interaction-primary",
            "accent-blue-100",
            "accent-blue-600",
            "accent-blue-700",
            "accent-green-100",
            "accent-green-600",
            "accent-green-700",
            "accent-amber-100",
            "accent-amber-600",
            "accent-amber-700",
            "accent-red-100",
            "accent-red-600",
            "accent-red-700",
            "accent-cyan-100",
            "accent-cyan-600",
            "accent-cyan-700",
            "accent-purple-100",
            "accent-purple-600",
            "accent-purple-700",

            "interaction-link",
            "interaction-selected",
            "chart-primary",
            "status-assigned-surface",
            "status-progress-surface",
            "status-success-surface",
            "status-warning-surface",
            "status-danger-surface",
            "status-disabled-surface",
            "kpi-info-surface",
            "kpi-success-surface",
            "kpi-warning-surface",
            "kpi-danger-surface",
            "kpi-neutral-surface",
          ].map((token) => (
            <div key={token}>
              <div
                className="swatch"
                style={{ background: `var(--${token})` }}
              />
              <code>--{token}</code>
            </div>
          ))}
        </div>
      </section>
      {Object.entries(families).map(([family, steps]) => (
        <section className="panel" key={family}>
          <h2>{family}</h2>
          <div className="swatch-grid">
            {steps.map((step) => (
              <div key={step}>
                <div
                  className="swatch"
                  style={{
                    background:
                      family === "White"
                        ? "var(--white)"
                        : `var(--${family.toLowerCase()}-${step})`,
                  }}
                />
                <code>
                  {family === "White"
                    ? "--white"
                    : `--${family.toLowerCase()}-${step}`}
                </code>
              </div>
            ))}
          </div>
        </section>
      ))}
      <section className="panel">
        <h2>{t("Typography")}</h2>
        <h1>Duc Vuong HotelOps</h1>
        <h2>{t("Operational Reports")}</h2>
        <p>{t("Public frontend prototype")}</p>
        <small>
          Body 14px · Table 14px · Caption 12px · Heading 24px · Desktop
          controls 36px
        </small>
      </section>
      <section className="panel">
        <h2>{t("Status")}</h2>
        <div className="token-examples">
          {[
            "Active",
            "Pending",
            "Assigned",
            "Accepted",
            "Paused",
            "Cancelled",
            "In Progress",
            "Completed",
            "Failed",
            "Blocked",
            "Inactive",
          ].map((status) => (
            <StatusBadge key={status} status={status} />
          ))}
        </div>
        <h2>{t("Actions")}</h2>
        <div className="token-examples">
          <button className="primary">{t("Save")}</button>
          <button>{t("Cancel")}</button>
          <button disabled>{t("Saving…")}</button>
        </div>
        <h2>{t("Spacing")}</h2>
        <div className="token-examples">
          {[4, 8, 12, 16, 24, 32, 48, 64].map((size) => (
            <div key={size}>
              <div
                style={{
                  width: size,
                  height: 16,
                  background: "var(--blue-600)",
                }}
              />
              {size}px
            </div>
          ))}
        </div>
      </section>
    </main>
  );
}
