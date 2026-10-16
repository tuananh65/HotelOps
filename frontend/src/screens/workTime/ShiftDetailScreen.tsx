import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = [
  "date",
  "start",
  "end",
  "department",
  "assignees",
];
const workflowUCs = ["UC-138"];
export default function ShiftDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("shifts").find((r) => r.id === id);
  return (
    <DetailPage
      id={72}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Shift hours",
                value: `${record.start} · ${record.end}`,
              },
              {
                label: "Assigned employees",
                value: record.assignees?.length || 0,
              },
              { label: "Work date", value: record.date },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="shifts"
          recordId={id}
          title={t("Shift Detail · related activity")}
        />
      }
    />
  );
}
