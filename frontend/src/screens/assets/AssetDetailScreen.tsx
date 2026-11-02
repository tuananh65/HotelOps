import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["code", "room", "category", "condition"];
const workflowUCs = [
  "UC-066",
  "UC-067",
  "UC-068",
  "UC-069",
  "UC-070",
  "UC-071",
  "UC-074",
];
export default function AssetDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("assets").find((r) => r.id === id);
  return (
    <DetailPage
      id={49}
      overview={
        record && (
          <Summary
            items={[
              { label: "Equipment condition", value: record.condition },
              {
                label: "Linked incidents",
                value: db.incidents.filter((r) => r.asset === record.id).length,
              },
              {
                label: "Preventive schedules",
                value: db.schedules.filter((r) => r.asset === record.id).length,
              },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="assets"
          recordId={id}
          title={t("Asset Detail · related activity")}
        />
      }
    />
  );
}
