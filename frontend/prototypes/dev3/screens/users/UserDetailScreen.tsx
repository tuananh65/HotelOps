import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = [
  "username",
  "email",
  "roles",
  "department",
];
const workflowUCs = ["UC-005", "UC-006", "UC-007", "UC-010", "UC-011"];
export default function UserDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("users").find((r) => r.id === id);
  return (
    <DetailPage
      id={9}
      overview={
        record && (
          <Summary
            items={[
              { label: "Login account", value: record.username },
              { label: "Assigned roles", value: record.roles?.join(" · ") },
              {
                label: "Password status",
                value: record.temporary ? "Change required" : "Permanent",
              },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="users"
          recordId={id}
          title={t("User Detail · related activity")}
        />
      }
    />
  );
}
