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
                      (l: {
                        quantity: number | null;
                        variance: number | null;
                      }) =>
                        l.quantity !== null &&
                        l.variance !== null &&
                        l.variance !== 0,
                    ).length || 0,
                },
                {
                  label: "Net variance",
                  value: record.lines?.some(
                    (l: { variance: number | null }) => l.variance === null,
                  )
                    ? "N/A"
                    : (record.lines?.reduce(
                        (sum: number, l: { variance: number }) =>
                          sum + l.variance,
                        0,
                      ) ?? "N/A"),
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
                        quantity: number | null;
                        variance: number | null;
                      }) => (
                        <tr key={line.item}>
                          <td>{name("items", line.item)}</td>
                          <td>{line.system}</td>
                          <td>{line.quantity ?? "N/A"}</td>
                          <td
                            className={
                              line.variance !== null && line.variance !== 0
                                ? "overdue-text"
                                : ""
                            }
                          >
                            {line.variance ?? "N/A"}
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
