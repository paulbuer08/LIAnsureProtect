import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

// Keep tests independent from each developer's gitignored .env.local file.
vi.stubEnv("VITE_AUTH0_DOMAIN", "test.auth0.local");
vi.stubEnv("VITE_AUTH0_CLIENT_ID", "test-client-id");
vi.stubEnv("VITE_AUTH0_AUDIENCE", "https://api.liansureprotect.local");
vi.stubEnv("VITE_AUTH0_CALLBACK_URL", "http://localhost:5173/callback");
vi.stubEnv("VITE_API_BASE_URL", "http://localhost:5223");

afterEach(() => {
  cleanup();
});
