import { useParams } from "react-router-dom";
import { InternalRequestHandoverPanel } from "./HandoverPanel";

export function HandoverReceiptPage() {
  const { requestId, handoverId } = useParams();
  const valid = (value: string | undefined) => !!value && /^[1-9][0-9]*$/.test(value) && Number.isSafeInteger(Number(value));
  if (!valid(requestId) || !valid(handoverId)) return <p role="alert">Xác nhận bàn giao không hợp lệ.</p>;
  return <main>
    <h1>Biên nhận bàn giao của tôi</h1>
    <InternalRequestHandoverPanel key={`${requestId}/${handoverId}`} id={Number(requestId)} handoverId={Number(handoverId)} />
  </main>;
}
