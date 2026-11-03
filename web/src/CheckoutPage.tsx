import { FormEvent, useEffect, useRef, useState } from "react";
import {
  createOrder,
  fetchOrder,
  fetchTickets,
  OrderResponse,
  Ticket
} from "./api";
import ReceiptLedger from "./ReceiptLedger";

type PaymentMethod = "Card" | "QR";

export default function CheckoutPage() {
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [ticketId, setTicketId] = useState(1);
  const [quantity, setQuantity] = useState(1);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>("Card");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [receipt, setReceipt] = useState<OrderResponse | null>(null);
  const [pendingMessage, setPendingMessage] = useState<string | null>(null);
  const requestIdRef = useRef<string | null>(null);

  const selected = tickets.find((t) => t.id === ticketId) ?? null;
  const total = selected ? selected.priceAed * quantity : 0;

  async function refreshTickets() {
    const next = await fetchTickets();
    setTickets(next);
    if (!next.some((t) => t.id === ticketId) && next[0]) {
      setTicketId(next[0].id);
    }
  }

  useEffect(() => {
    refreshTickets().catch((err: Error) => setError(err.message));
  }, []);

  useEffect(() => {
    if (!receipt || receipt.status !== "Pending") return;

    let cancelled = false;
    setPendingMessage("QR payment pending — waiting for confirmation…");

    const timer = window.setInterval(async () => {
      try {
        const latest = await fetchOrder(receipt.id);
        if (cancelled) return;
        setReceipt(latest);
        if (latest.status === "Succeeded") {
          setPendingMessage(null);
          window.clearInterval(timer);
          await refreshTickets();
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Polling failed");
      }
    }, 1000);

    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [receipt?.id, receipt?.status]);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (submitting) return;

    setError(null);
    setReceipt(null);
    setPendingMessage(null);
    setSubmitting(true);

    if (!requestIdRef.current) {
      requestIdRef.current = crypto.randomUUID();
    }

    try {
      const order = await createOrder({
        requestId: requestIdRef.current,
        ticketId,
        quantity,
        paymentMethod
      });
      setReceipt(order);
      requestIdRef.current = null;
      if (order.status === "Succeeded") {
        await refreshTickets();
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Checkout failed";
      setError(message);
      if (message.toLowerCase().includes("insufficient")) {
        await refreshTickets().catch(() => undefined);
      }
      // Keep requestIdRef so a network retry reuses the same idempotency key.
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <header className="hero">
        <p className="brand">E-Ticketing System</p>
        <h1>Buy tickets</h1>
        <p className="lede">Simulated Card and QR checkout against the live API.</p>
      </header>

      <section className="catalog" aria-label="Ticket catalog">
        {tickets.map((ticket) => (
          <button
            key={ticket.id}
            type="button"
            className={ticket.id === ticketId ? "ticket selected" : "ticket"}
            onClick={() => setTicketId(ticket.id)}
          >
            <strong>{ticket.name}</strong>
            <span>AED {ticket.priceAed}</span>
            <span>{ticket.remainingQuota} remaining</span>
          </button>
        ))}
      </section>

      <form className="checkout" onSubmit={onSubmit}>
        <label>
          Quantity
          <input
            type="number"
            min={1}
            value={quantity}
            disabled={submitting}
            onChange={(e) => setQuantity(Math.max(1, Number(e.target.value) || 1))}
          />
        </label>

        <fieldset disabled={submitting}>
          <legend>Payment method</legend>
          <label className="radio">
            <input
              type="radio"
              name="payment"
              checked={paymentMethod === "Card"}
              onChange={() => setPaymentMethod("Card")}
            />
            Card (immediate)
          </label>
          <label className="radio">
            <input
              type="radio"
              name="payment"
              checked={paymentMethod === "QR"}
              onChange={() => setPaymentMethod("QR")}
            />
            QR (confirms after ~8s)
          </label>
        </fieldset>

        <p className="total">Total: AED {total}</p>

        <button type="submit" disabled={submitting || !selected || selected.remainingQuota < 1}>
          {submitting ? "Processing…" : "Buy tickets"}
        </button>
      </form>

      {error && <p className="error" role="alert">{error}</p>}
      {pendingMessage && <p className="pending">{pendingMessage}</p>}

      {receipt && (
        <section className="receipt" aria-live="polite">
          <h2>{receipt.status === "Succeeded" ? "Purchase confirmed" : "Purchase pending"}</h2>
          <p>Order {receipt.id}</p>
          <p>Total AED {receipt.totalAed}</p>
          <p>Transaction {receipt.payment?.transactionId}</p>
          {receipt.payment?.confirmedAtUtc && (
            <p>Confirmed {new Date(receipt.payment.confirmedAtUtc).toISOString()}</p>
          )}
          <ReceiptLedger order={receipt} />
        </section>
      )}
    </>
  );
}
