import { ThemeToggle } from "../components/ThemeToggle";
import { useState } from "react";
import { Link } from "react-router-dom";
import { LanguageSwitcher, t, useLanguage, vi } from "../i18n";
import { Logo, notify, useDB } from "../ui";
import { demoPath } from "./demoPaths";
import { requiredRoutes } from "./routeConfig";
export function ScreenIndex() {
  useLanguage();
  useDB();
  const [search, setSearch] = useState("");
  const [feature, setFeature] = useState("");
  const [type, setType] = useState("");
  const [module, setModule] = useState("");
  const code = (value: string) => value.match(/FE-\d+/)?.[0] || value;
  const kind = (value: string) =>
    /Detail|Result/.test(value)
      ? "Detail"
      : /Assignment|Create|Report Incident|Inspection$|Reinspection|Password|Sign In/.test(
            value,
          )
        ? "Form"
        : /Dashboard|Board|Reports|Matrix/.test(value)
          ? "Dashboard"
          : "List";
  const rows = requiredRoutes.filter(
    (s) =>
      (!feature || code(s.feature) === feature) &&
      (!type || kind(s.name) === type) &&
      (!module || s.feature === module) &&
      `${s.id} ${s.name} ${vi[s.name]} ${s.description} ${vi[s.description]} ${s.ucs.join(" ")} ${s.route} ${s.feature}`
        .toLowerCase()
        .includes(search.toLowerCase()),
  );
  async function copy(route: string) {
    try {
      await navigator.clipboard.writeText(
        new URL(route, window.location.origin).href,
      );
      notify("Link copied");
    } catch {
      notify(new URL(route, window.location.origin).href);
    }
  }
  return (
    <main className="screen-index public-library">
      <div className="library-nav">
        <Logo />
        <div className="token-examples">
          <LanguageSwitcher />
          <ThemeToggle />
          <Link to="/dashboard">{t("Dashboard")}</Link>
          <Link to="/dev/design-system">{t("Design system")}</Link>
        </div>
      </div>
      <header className="library-hero">
        <span className="eyebrow">HOTEL OPS · UI/UX SCREEN DIRECTORY</span>
        <h1>{t("Screen library")}</h1>
        <p>
          {t("Review all 80 screens with synthetic hotel operations data.")}
        </p>
        <div className="library-count">
          <strong>80 / 80</strong>
          <span>{t("Public frontend prototype")} · 15 FE · 140 UC</span>
        </div>
      </header>
      <section className="panel library-filters">
        <input
          aria-label={t("Search screens")}
          placeholder={t("Search screens")}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select
          aria-label="FE"
          value={feature}
          onChange={(e) => setFeature(e.target.value)}
        >
          <option value="">{t("All features")}</option>
          {[...new Set(requiredRoutes.map((s) => code(s.feature)))]
            .sort()
            .map((f) => (
              <option key={f}>{f}</option>
            ))}
        </select>
        <select
          aria-label={t("All modules")}
          value={module}
          onChange={(e) => setModule(e.target.value)}
        >
          <option value="">{t("All modules")}</option>
          {[...new Set(requiredRoutes.map((s) => s.feature))].map((f) => (
            <option key={f} value={f}>
              {t(f.split(" (")[0])}
            </option>
          ))}
        </select>
        <select
          aria-label={t("All types")}
          value={type}
          onChange={(e) => setType(e.target.value)}
        >
          <option value="">{t("All types")}</option>
          {["List", "Detail", "Form", "Dashboard"].map((f) => (
            <option key={f} value={f}>
              {t(f)}
            </option>
          ))}
        </select>
        <span>{rows.length} / 80</span>
      </section>
      <div className="directory-scroll">
        <table className="directory-table">
          <thead>
            <tr>
              <th>#</th>
              <th>{t("Screen")}</th>
              <th>FE</th>
              <th>UC</th>
              <th>{t("Type")}</th>
              <th>{t("Route")}</th>
              <th>{t("Actions")}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((s) => (
              <tr className="screen-card" key={s.id}>
                <td>{String(s.id).padStart(2, "0")}</td>
                <td>
                  <h2>{t(s.name)}</h2>
                  <small>{t(s.name) === s.name ? vi[s.name] : s.name}</small>
                </td>
                <td title={t(s.feature)}>{code(s.feature)}</td>
                <td title={s.ucs.join(", ")}>
                  <div className="uc-chips">
                    {s.ucs.slice(0, 3).map((uc) => (
                      <span key={uc}>{uc}</span>
                    ))}
                    {s.ucs.length > 3 && <span>+{s.ucs.length - 3}</span>}
                  </div>
                </td>
                <td>{t(kind(s.name))}</td>
                <td>
                  <code>{demoPath(s.id)}</code>
                </td>
                <td>
                  <div className="card-actions">
                    <Link
                      className="primary"
                      data-screen-id={s.id}
                      to={demoPath(s.id)}
                    >
                      {t("Open")}
                    </Link>
                    <button onClick={() => copy(demoPath(s.id))}>
                      {t("Copy link")}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </main>
  );
}
