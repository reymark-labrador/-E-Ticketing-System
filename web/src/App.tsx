import { MouseEvent, useEffect, useState } from "react";
import CheckoutPage from "./CheckoutPage";
import DashboardPage from "./DashboardPage";

function currentPath(): string {
  const path = window.location.pathname.replace(/\/+$/, "") || "/";
  return path;
}

export default function App() {
  const [path, setPath] = useState(currentPath);

  useEffect(() => {
    const onPopState = () => setPath(currentPath());
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, []);

  function navigate(event: MouseEvent<HTMLAnchorElement>, to: string) {
    if (
      event.defaultPrevented ||
      event.button !== 0 ||
      event.metaKey ||
      event.altKey ||
      event.ctrlKey ||
      event.shiftKey
    ) {
      return;
    }
    event.preventDefault();
    if (currentPath() === to) return;
    window.history.pushState({}, "", to);
    setPath(to);
  }

  const onDashboard = path === "/dashboard";

  return (
    <div className={onDashboard ? "page page-wide" : "page"}>
      <nav className="site-nav" aria-label="Primary">
        <a
          href="/"
          className={!onDashboard ? "active" : undefined}
          aria-current={!onDashboard ? "page" : undefined}
          onClick={(event) => navigate(event, "/")}
        >
          Checkout
        </a>
        <a
          href="/dashboard"
          className={onDashboard ? "active" : undefined}
          aria-current={onDashboard ? "page" : undefined}
          onClick={(event) => navigate(event, "/dashboard")}
        >
          Dashboard
        </a>
      </nav>

      {onDashboard ? <DashboardPage /> : <CheckoutPage />}
    </div>
  );
}
