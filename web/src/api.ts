export type Ticket = {
  id: number;
  name: string;
  priceAed: number;
  capacity: number;
  remainingQuota: number;
};

export type OrderSummary = {
  id: string;
  createdAtUtc: string;
  ticketType: string;
  quantity: number;
  totalAed: number;
  paymentMethod: string;
  status: string;
};

export type OrderResponse = {
  id: string;
  requestId: string;
  ticketId: number;
  quantity: number;
  unitPriceAed: number;
  totalAed: number;
  paymentMethod: string;
  status: string;
  createdAtUtc: string;
  payment: {
    transactionId: string;
    status: string;
    amountAed: number;
    dueAtUtc: string | null;
    confirmedAtUtc: string | null;
  } | null;
  ledger: Array<{
    account: string;
    side: string;
    amountAed: number;
  }>;
};

const apiBase = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

async function readError(response: Response): Promise<string> {
  try {
    const body = await response.json();
    if (body?.error) return String(body.error);
    if (body?.errors) {
      const values = Object.values(body.errors as Record<string, string[]>).flat();
      if (values.length) return values.join(" ");
    }
    if (body?.title) return String(body.title);
  } catch {
    /* ignore */
  }
  return `Request failed (${response.status})`;
}

export async function fetchTickets(): Promise<Ticket[]> {
  const response = await fetch(`${apiBase}/api/tickets`);
  if (!response.ok) throw new Error(await readError(response));
  return response.json();
}

export async function createOrder(payload: {
  requestId: string;
  ticketId: number;
  quantity: number;
  paymentMethod: string;
}): Promise<OrderResponse> {
  const response = await fetch(`${apiBase}/api/orders`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.ok) throw new Error(await readError(response));
  return response.json();
}

export async function fetchOrders(): Promise<OrderSummary[]> {
  const response = await fetch(`${apiBase}/api/orders`);
  if (!response.ok) throw new Error(await readError(response));
  return response.json();
}

export async function fetchOrder(id: string): Promise<OrderResponse> {
  const response = await fetch(`${apiBase}/api/orders/${id}`);
  if (!response.ok) throw new Error(await readError(response));
  return response.json();
}
