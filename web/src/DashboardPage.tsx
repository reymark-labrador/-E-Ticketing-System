import { useEffect, useState } from "react";
import {
  fetchOrder,
  fetchOrders,
  OrderResponse,
  OrderSummary
} from "./api";
import ReceiptLedger, { formatAed } from "./ReceiptLedger";

export default function DashboardPage() {
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<OrderResponse | null>(null);
  const [listLoading, setListLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadOrders() {
      setListLoading(true);
      setListError(null);
      try {
        const next = await fetchOrders();
        if (cancelled) return;
        setOrders(next);
        setSelectedId((current) => current ?? next[0]?.id ?? null);
      } catch (err) {
        if (!cancelled) {
          setListError(err instanceof Error ? err.message : "Failed to load orders");
          setOrders([]);
        }
      } finally {
        if (!cancelled) setListLoading(false);
      }
    }

    loadOrders();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!selectedId) {
      setDetail(null);
      setDetailError(null);
      setDetailLoading(false);
      return;
    }

    let cancelled = false;

    async function loadDetail(id: string) {
      setDetailLoading(true);
      setDetailError(null);
      try {
        const order = await fetchOrder(id);
        if (cancelled) return;
        setDetail(order);
      } catch (err) {
        if (!cancelled) {
          setDetail(null);
          setDetailError(err instanceof Error ? err.message : "Failed to load ledger");
        }
      } finally {
        if (!cancelled) setDetailLoading(false);
      }
    }

    loadDetail(selectedId);
    return () => {
      cancelled = true;
    };
  }, [selectedId]);

  useEffect(() => {
    if (!selectedId || !detail || detail.status !== "Pending") return;

    let cancelled = false;
    const orderId = selectedId;

    const timer = window.setInterval(async () => {
      try {
        const latest = await fetchOrder(orderId);
        if (cancelled) return;

        setDetail(latest);
        setOrders((rows) =>
          rows.map((row) =>
            row.id === latest.id
              ? {
                  ...row,
                  status: latest.status,
                  totalAed: latest.totalAed,
                  paymentMethod: latest.paymentMethod,
                  quantity: latest.quantity
                }
              : row
          )
        );

        if (latest.status !== "Pending") {
          window.clearInterval(timer);
        }
      } catch (err) {
        if (!cancelled) {
          setDetailError(err instanceof Error ? err.message : "Polling failed");
        }
      }
    }, 1000);

    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [selectedId, detail?.status, detail?.id]);

  return (
    <>
      <header className="hero">
        <p className="brand">E-Ticketing System</p>
        <h1>Orders dashboard</h1>
        <p className="lede">Read-only view of recent orders and their payment ledgers.</p>
      </header>

      <section className="dashboard-section" aria-label="Orders">
        <h2>Orders</h2>
        {listLoading && <p className="state">Loading orders…</p>}
        {listError && (
          <p className="error" role="alert">
            {listError}
          </p>
        )}
        {!listLoading && !listError && orders.length === 0 && (
          <p className="state">No orders yet. Complete a checkout to see them here.</p>
        )}
        {!listLoading && !listError && orders.length > 0 && (
          <div className="orders-table-wrap">
            <table className="orders-table">
              <caption className="visually-hidden">Latest 50 orders, newest first</caption>
              <thead>
                <tr>
                  <th scope="col">Order time</th>
                  <th scope="col">Ticket type</th>
                  <th scope="col">Qty</th>
                  <th scope="col">AED total</th>
                  <th scope="col">Payment</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>
              <tbody>
                {orders.map((order) => {
                  const selected = order.id === selectedId;
                  return (
                    <tr
                      key={order.id}
                      className={selected ? "selected" : undefined}
                      aria-selected={selected}
                      onClick={() => setSelectedId(order.id)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter" || event.key === " ") {
                          event.preventDefault();
                          setSelectedId(order.id);
                        }
                      }}
                      tabIndex={0}
                    >
                      <td>{new Date(order.createdAtUtc).toISOString()}</td>
                      <td>{order.ticketType}</td>
                      <td>{order.quantity}</td>
                      <td>{formatAed(order.totalAed)}</td>
                      <td>{order.paymentMethod}</td>
                      <td>{order.status}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="dashboard-section ledger-section" aria-label="Ledger">
        <h2>Ledger</h2>
        {!selectedId && !listLoading && (
          <p className="state">Select an order to inspect its ledger.</p>
        )}
        {selectedId && detailLoading && !detail && (
          <p className="state">Loading ledger…</p>
        )}
        {detailError && (
          <p className="error" role="alert">
            {detailError}
          </p>
        )}
        {detail && (
          <div className="receipt dashboard-receipt" aria-live="polite">
            <p>
              Order {detail.id} · {detail.paymentMethod} · {detail.status}
            </p>
            <ReceiptLedger order={detail} />
          </div>
        )}
      </section>
    </>
  );
}
