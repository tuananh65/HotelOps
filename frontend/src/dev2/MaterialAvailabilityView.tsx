import { useState } from "react";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaterialAvailabilityView({ data }: { data: Values }) {
  const warehouses = Array.isArray(data.warehouses) ? data.warehouses.filter(isObject) : [];
  const [selected, setSelected] = useState(String(data.warehouseId ?? ""));
  const warehouse = warehouses.find(w => String(w.warehouseId) === selected) ?? warehouses[0];
  const lines = warehouse && Array.isArray(warehouse.lines) ? warehouse.lines.filter(isObject) : [];
  return <section aria-label="Material availability by warehouse">
    <h2>Material availability</h2>
    <p>Preview only; no stock is reserved. In-transit materials cannot be issued until received.</p>
    <label>Warehouse
      <select value={String(warehouse?.warehouseId ?? "")} onChange={e => setSelected(e.target.value)} disabled={!warehouses.length}>
        {warehouses.map(w => <option key={String(w.warehouseId)} value={String(w.warehouseId)}>{String(w.warehouseName)}</option>)}
      </select>
    </label>
    {warehouse && String(warehouse.warehouseId) !== String(data.warehouseId) &&
      <p>This is a comparison warehouse. The request's issuing warehouse has not changed.</p>}
    {!warehouses.length ? <p>No warehouse availability was returned. Reload the request before issuing.</p> :
      <table>
        <caption>Availability for {String(warehouse?.warehouseName)}</caption>
        <thead><tr><th scope="col">Material</th><th scope="col">Remaining approved</th><th scope="col">Available stock</th>
          <th scope="col">In transit</th><th scope="col">Fulfillable</th><th scope="col">Guidance</th></tr></thead>
        <tbody>{lines.map(line => {
          const shortage = typeof line.fulfillableQty === "number" && typeof line.remainingApprovedQty === "number"
            && line.fulfillableQty < line.remainingApprovedQty;
          return <tr key={String(line.requestLineId)}>
            <th scope="row">{String(line.itemName)}</th>
            <td>{line.remainingApprovedQty == null ? "N/A" : `${line.remainingApprovedQty} ${line.unitName}`}</td>
            <td>{String(line.availableBaseQty)} {String(line.baseUnitName)}</td>
            <td>{line.inTransitBaseQty == null ? "N/A" : `${line.inTransitBaseQty} ${line.baseUnitName}`}
              {line.transitUnavailableReason != null && <p>{String(line.transitUnavailableReason)}</p>}</td>
            <td>{line.fulfillableQty == null ? "N/A" : `${line.fulfillableQty} ${line.unitName}`}</td>
            <td>{line.unavailableReason != null && <p>{String(line.unavailableReason)}</p>}
              {shortage && <p><strong>Cannot fully issue.</strong> Consider partial fulfillment or a replenishment request.</p>}
              {line.sharedItemStock === true && <p>Shared stock with other lines of this item. These individual previews cannot be added together; issue validation checks the combined quantity.</p>}</td>
          </tr>;
        })}</tbody>
      </table>}
    <p>Fulfillable quantities use current unit conversions. Posting checks stock, permissions and request version again.</p>
    {data.requestId != null && <Link to={`/operations/material-requests/${encodeURIComponent(String(data.requestId))}`}>Return to request for fulfillment</Link>}
  </section>;
}
