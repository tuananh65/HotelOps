import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["permissions", "system"];
const workflowUCs = ["UC-008", "UC-009"];
export default function RoleDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("roles").find((r) => r.id === id);
  return (
    <DetailPage
      id={11}
      overview={
        record && (
          <Summary
            items={[
              {
                label: "Permission grants",
                value: record.permissions?.length || 0,
              },
              {
                label: "Role type",
                value: record.system ? "System role" : "Custom role",
              },
              {
                label: "Active users",
                value: db.users.filter(
                  (u) =>
                    u.roles?.includes(record.name) && u.status === "Active",
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
          entity="roles"
          recordId={id}
          title={t("Role Detail · related activity")}
        />
      }
    />
  );
}
