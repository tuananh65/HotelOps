import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = [
  "type",
  "warehouse",
  "source",
  "createdAt",
];
const workflowUCs = ["UC-080", "UC-082", "UC-084", "UC-088"];
export default function StockTransactionDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("transactions").find((r) => r.id === id);
  return (
    <DetailPage
      id={58}
      overview={
        record && (
          <Summary
            items={[
              { label: "Movement type", value: record.type },
              { label: "Document lines", value: record.lines?.length || 0 },
              {
                label: "Total quantity",
                value:
                  record.lines?.reduce(
                    (sum: number, l: { quantity: number }) =>
                      sum + Number(l.quantity),
                    0,
                  ) || 0,
              },
              { label: "Posting status", value: record.status },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="transactions"
          recordId={id}
          title={t("Stock Transaction Detail · related activity")}
        />
      }
    />
  );
}
