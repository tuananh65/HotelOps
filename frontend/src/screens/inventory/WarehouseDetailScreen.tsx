import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["branch", "description"];
const workflowUCs = ["UC-075"];
export default function WarehouseDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("warehouses").find((r) => r.id === id);
  return (
    <DetailPage
      id={52}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Stocked item types",
                value: db.items.filter(
                  (i) => service.balance(db, record.id, i.id) > 0,
                ).length,
              },
              {
                label: "Below minimum",
                value: db.items.filter(
                  (i) =>
                    service.balance(db, record.id, i.id) <
                    Number(service.minimumStock(i, record.id) || 0),
                ).length,
              },
              {
                label: "Draft transactions",
                value: db.transactions.filter(
                  (r) => r.warehouse === record.id && r.status === "Draft",
                ).length,
              },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="warehouses"
          recordId={id}
          title={t("Warehouse Detail · related activity")}
        />
      }
    />
  );
}
