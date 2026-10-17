import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { PageHeader, notify } from "../../ui";
export default function ProfileScreen() {
  useLanguage();
  const user = service.currentUser()!;
  const nav = useNavigate();
  const [busy, setBusy] = useState(false);
  return (
    <>
      <PageHeader id={4} />
      <section className="panel form-panel">
        <span className="eyebrow">{t("STAFF PROFILE")}</span>
        <h2>{t(user.name)}</h2>
        <dl className="metadata">
          {[
            ["Username", user.username],
            ["Email", user.email],
            ["Branch", user.branch],
            ["Department", user.department],
            ["Roles", user.roles.join(" · ")],
            ["Status", user.status],
          ].map(([k, v]) => (
            <div key={k}>
              <dt>{t(k)}</dt>
              <dd>{t(v)}</dd>
            </div>
          ))}
        </dl>
        <div className="form-actions">
          <Link className="secondary" to="/change-password">
            {t("Change password")}
          </Link>
          <button
            className="primary"
            disabled={busy}
            onClick={async () => {
              setBusy(true);
              try {
                await service.logout();
                nav("/login");
              } catch (error) {
                notify((error as Error).message);
              } finally {
                setBusy(false);
              }
            }}
          >
            {t("Logout")}
          </button>
        </div>
      </section>
    </>
  );
}
