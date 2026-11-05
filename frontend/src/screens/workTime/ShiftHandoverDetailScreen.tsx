import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, name, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["sender", "receiver", "createdAt"];
const workflowUCs = ["UC-140"];
export default function ShiftHandoverDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("handovers").find((r) => r.id === id);
  return (
    <DetailPage
      id={75}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Outgoing employee",
                value: name("users", record.sender),
              },
              {
                label: "Incoming employee",
                value: name("users", record.receiver),
              },
              { label: "Snapshot items", value: record.snapshot?.length || 0 },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="handovers"
          recordId={id}
          title={t("Shift Handover Detail · related activity")}
        />
      }
    />
  );
}
