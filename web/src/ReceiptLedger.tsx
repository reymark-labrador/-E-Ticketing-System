import { OrderResponse } from "./api";

function isLedgerBalanced(order: OrderResponse): boolean {
  const amount = order.payment?.amountAed;
  if (amount == null) return false;

  const debits = order.ledger.filter((e) => e.side === "Debit");
  const credits = order.ledger.filter((e) => e.side === "Credit");
  return (
    debits.length === 1 &&
    credits.length === 1 &&
    debits[0].amountAed === amount &&
    credits[0].amountAed === amount
  );
}

export function formatAed(amount: number): string {
  return amount.toFixed(2);
}

type ReceiptLedgerProps = {
  order: OrderResponse;
};

export default function ReceiptLedger({ order }: ReceiptLedgerProps) {
  const debitTotal = order.ledger
    .filter((e) => e.side === "Debit")
    .reduce((sum, e) => sum + e.amountAed, 0);
  const creditTotal = order.ledger
    .filter((e) => e.side === "Credit")
    .reduce((sum, e) => sum + e.amountAed, 0);
  const awaitingConfirmation = order.status === "Pending";
  const balanced = isLedgerBalanced(order);

  return (
    <div className="ledger-block">
      <div className="ledger-panel" role="region" aria-label="Payment ledger">
        <dl className="ledger-meta">
          <div>
            <dt>Transaction ID</dt>
            <dd>{order.payment?.transactionId ?? "—"}</dd>
          </div>
          <div>
            <dt>Status</dt>
            <dd>{order.payment?.status ?? order.status}</dd>
          </div>
          {order.payment?.confirmedAtUtc && (
            <div>
              <dt>Confirmation time</dt>
              <dd>{new Date(order.payment.confirmedAtUtc).toISOString()}</dd>
            </div>
          )}
        </dl>

        {awaitingConfirmation ? (
          <p className="ledger-awaiting">Awaiting payment confirmation.</p>
        ) : (
          <>
            <table className="ledger-table">
              <caption className="visually-hidden">
                Ledger entries in AED for this payment
              </caption>
              <thead>
                <tr>
                  <th scope="col">Account</th>
                  <th scope="col">Debit</th>
                  <th scope="col">Credit</th>
                </tr>
              </thead>
              <tbody>
                {order.ledger.map((entry, index) => (
                  <tr key={`${entry.account}-${entry.side}-${index}`}>
                    <th scope="row">{entry.account}</th>
                    <td>
                      {entry.side === "Debit" ? formatAed(entry.amountAed) : "—"}
                    </td>
                    <td>
                      {entry.side === "Credit" ? formatAed(entry.amountAed) : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <th scope="row">Total (AED)</th>
                  <td>{formatAed(debitTotal)}</td>
                  <td>{formatAed(creditTotal)}</td>
                </tr>
              </tfoot>
            </table>
            {balanced && (
              <p className="ledger-balanced" role="status">
                Balanced
              </p>
            )}
          </>
        )}
      </div>
    </div>
  );
}
