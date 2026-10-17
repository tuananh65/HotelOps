import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import {
  date,
  DetailPage,
  name,
  RelatedRecords,
  Summary,
  useDB,
} from "../../ui";
const informationFields: string[] = ["warehouse", "cutoff", "creator"];
const workflowUCs = ["UC-085", "UC-086", "UC-087"];
export default function StocktakeDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("stocktakes").find((r) => r.id === id);
  return (
    <DetailPage
      id={60}
      overview={
        record && (
          <>
            <Summary
              items={[
                { label: "Count lines", value: record.lines?.length || 0 },
                {
                  label: "Discrepancies",
                  value:
                    record.lines?.filter(
                      (l: { quantity: number; system: number }) =>
                        Number(l.quantity) !== Number(l.system),
                    ).length || 0,
                },
                {
                  label: "Net variance",
                  value:
                    record.lines?.reduce(
                      (sum: number, l: { quantity: number; system: number }) =>
                        sum + Number(l.quantity) - Number(l.system),
                      0,
                    ) || 0,
                },
              ]}
            />
            <section className="panel">
              <h3>{t("Physical count and variance")}</h3>
              <p className="quiet">
                {t("Compare counted quantities with the frozen balance at")}{" "}
                {date(record.cutoff)}
                {t(". Record counts before submitting reconciliation.")}
              </p>
              <div className="table-scroll">
                <table>
                  <thead>
                    <tr>
                      <th>{t("Item")}</th>
                      <th>{t("System at cutoff")}</th>
                      <th>{t("Physical count")}</th>
                      <th>{t("Variance")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {record.lines?.map(
                      (line: {
                        item: string;
                        system: number;
                        quantity: number;
                      }) => (
                        <tr key={line.item}>
                          <td>{name("items", line.item)}</td>
                          <td>{line.system}</td>
                          <td>{line.quantity}</td>
                          <td
                            className={
                              Number(line.quantity) !== Number(line.system)
                                ? "overdue-text"
                                : ""
                            }
                          >
                            {Number(line.quantity) - Number(line.system)}
                          </td>
                        </tr>
                      ),
                    )}
                  </tbody>
                </table>
              </div>
            </section>
          </>
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="stocktakes"
          recordId={id}
          title={t("Stocktake Detail · related activity")}
        />
      }
    />
  );
}
