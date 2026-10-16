import { useParams } from "react-router-dom";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { DetailPage, RelatedRecords, Summary, useDB } from "../../ui";
const informationFields: string[] = [
  "creator",
  "department",
  "warehouse",
  "due",
];
const workflowUCs = [
  "UC-090",
  "UC-092",
  "UC-093",
  "UC-094",
  "UC-095",
  "UC-096",
  "UC-097",
  "UC-098",
  "UC-099",
];
export default function MaterialRequestDetailScreen() {
  useLanguage();
  const { id } = useParams();
  const db = useDB();
  const record = service.visible("materials").find((r) => r.id === id);
  return (
    <DetailPage
      id={63}
      overview={
        record && (
          <Summary
            items={[
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
                label: "Approved quantity",
                value:
                  record.lines?.reduce(
                    (n: number, l: { approved?: number }) =>
                      n + Number(l.approved || 0),
                    0,
                  ) || 0,
              },
              {
                label: "Issued quantity",
                value:
                  record.lines?.reduce(
                    (n: number, l: { issued?: number }) =>
                      n + Number(l.issued || 0),
                    0,
                  ) || 0,
              },
              {
                label: "Returned quantity",
                value:
                  record.lines?.reduce(
                    (n: number, l: { returned?: number }) =>
                      n + Number(l.returned || 0),
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
          entity="materials"
          recordId={id}
          title={t("Material Request Detail · related activity")}
        />
      }
    />
  );
}
