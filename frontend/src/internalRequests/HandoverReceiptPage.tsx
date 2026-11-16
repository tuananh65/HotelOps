import { useParams } from "react-router-dom";
import { InternalRequestHandoverPanel } from "./HandoverPanel";

export function HandoverReceiptPage() {
  const { requestId, handoverId } = useParams();
  const valid = (value: string | undefined) => !!value && /^[1-9][0-9]*$/.test(value) && Number.isSafeInteger(Number(value));
  if (!valid(requestId) || !valid(handoverId)) return <p role="alert">Invalid handover receipt.</p>;
  return <main>
    <h1>My handover receipt</h1>
    <InternalRequestHandoverPanel key={`${requestId}/${handoverId}`} id={Number(requestId)} handoverId={Number(handoverId)} />
  </main>;
}
