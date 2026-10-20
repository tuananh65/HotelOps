import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["type", "priority", "createdAt"];
const workflowUCs = ["UC-130"];
export default function NotificationDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("notifications").find((r) => r.id === id);
  return (
    <DetailPage
      id={7}
      overview={
        record && (
          <Summary
            items={[
              { label: "Delivery type", value: record.type },
              { label: "Priority", value: record.priority },
              { label: "Read status", value: record.status },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="notifications"
          recordId={id}
          title={t("Notification Detail · related activity")}
        />
      }
    />
  );
}
