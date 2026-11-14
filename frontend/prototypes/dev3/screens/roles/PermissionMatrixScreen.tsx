import { Check } from "lucide-react";
import { useCases } from "../../domain";
import { t, useLanguage } from "../../i18n";
import { PageHeader, useDB } from "../../ui";
export default function PermissionMatrixScreen() {
  useLanguage();
  const db = useDB();
  return (
    <>
      <PageHeader id={12} />
      <section className="panel table-scroll">
        <table className="permission-matrix">
          <thead>
            <tr>
              <th>{t("Permission")}</th>
              {db.roles.map((r) => (
                <th key={r.id}>{t(r.name)}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Object.values(useCases).map((uc) => (
              <tr key={uc.id}>
                <td>
                  {uc.id}
                  <small>{t(uc.name)}</small>
                </td>
                {db.roles.map((r) => (
                  <td key={r.id}>
                    {r.permissions?.includes(uc.id) ? (
                      <Check size={16} aria-label={t("Granted")} />
                    ) : (
                      <span aria-label={t("Not granted")}>×</span>
                    )}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}
