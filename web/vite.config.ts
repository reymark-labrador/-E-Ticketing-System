import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  appType: "spa",
  server: {
    host: true,
    port: 5173,
    proxy: {
      "/api": "http://localhost:5080"
    }
  },
  preview: {
    host: true,
    port: 5173
  }
});
