import { ThemeToggle } from "../components/ThemeToggle";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { LanguageSwitcher, t } from "../i18n";
import { Logo } from "../ui";
export function AuthLayout({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <div className="auth">
      <div className="auth-art">
        <Logo />
        <div>
          <span className="eyebrow">{t("DUC VUONG SAIGON HOTEL")}</span>
          <h1>
            {t("Hotel operations.")}
            <br />
            {t("One connected workspace.")}
          </h1>
          <p>
            {t("Rooms, people and daily work,")}
            <br />
            {t("with clear status and practical controls.")}
          </p>
          <div className="gold-rule" />
        </div>
        <small>BUI VIEN / CAO THANG</small>
      </div>
      <main className="auth-panel">
        <div className="library-nav">
          <LanguageSwitcher />
          <ThemeToggle />
          <Link to="/dev/screens">{t("Screen library")}</Link>
        </div>
        <Link to="/login" className="auth-wordmark">
          {t("HOTEL")}
          <span>{t("OPS")}</span>
        </Link>
        <span className="eyebrow">{t("STAFF PORTAL")}</span>
        <h1>{title}</h1>
        {children}
        <div className="auth-footer">
          {t("Duc Vuong Group · Internal operations")}
        </div>
      </main>
    </div>
  );
}
