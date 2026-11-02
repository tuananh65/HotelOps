import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["code", "category", "unit", "minimum"];
const workflowUCs = ["UC-076", "UC-077", "UC-078"];
export default function ItemDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("items").find((r) => r.id === id);
  const quantities = record ? db.warehouses.map((w) => service.balance(db, w.id, record.id)) : [];
  return (
    <DetailPage
      id={55}
      overview={
        record && (
          <Summary
            items={[
              { label: "Base unit", value: record.unit },
              {
                label: "Quantity across warehouses",
                value: quantities.length && quantities.every(Number.isFinite)
                  ? quantities.reduce((sum, value) => sum + value, 0)
                  : "N/A",
              },
              { label: "Minimum stock", value: record.minimum },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="items"
          recordId={id}
          title={t("Item Detail · related activity")}
        />
      }
    />
  );
}
