import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = ["warehouse", "supplier", "due"];
const workflowUCs = ["UC-101"];
export default function ReplenishmentDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("replenishments").find((r) => r.id === id);
  return (
    <DetailPage
      id={65}
      overview={
        record && (
          <Summary
            items={[
              { label: "Supplier", value: record.supplier },
              {
                label: "Requested quantity",
                value:
                  record.lines?.reduce(
                    (n: number, l: { quantity: number }) =>
                      n + Number(l.quantity),
                    0,
                  ) || 0,
              },
              {
                label: "Received quantity",
                value:
                  record.lines?.reduce(
                    (n: number, l: { received?: number }) =>
                      n + Number(l.received || 0),
                    0,
                  ) || 0,
              },
            ]}
          />
        )
      }
      informationFields={informationFields}
      workflowUCs={workflowUCs}
      related={
        <RelatedRecords
          entity="replenishments"
          recordId={id}
          title={t("Replenishment Detail · related activity")}
        />
      }
    />
  );
}
