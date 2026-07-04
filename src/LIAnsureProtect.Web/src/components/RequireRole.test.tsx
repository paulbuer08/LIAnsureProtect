import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { describe, expect, it, vi } from "vitest";

import { RequireRole } from "./RequireRole";

const useAuth0 = vi.fn();

vi.mock("@auth0/auth0-react", () => ({
  useAuth0: () => useAuth0(),
}));

function renderWithRoles(roles: unknown, allowed: string[]) {
  useAuth0.mockReturnValue({
    user: { "https://liansureprotect.local/roles": roles },
  });

  return render(
    <MemoryRouter>
      <RequireRole allowedRoles={allowed}>
        <p>Role-protected content</p>
      </RequireRole>
    </MemoryRouter>,
  );
}

describe("RequireRole", () => {
  it("renders children when the user holds an allowed role", () => {
    renderWithRoles(["ClaimsAdjuster"], ["ClaimsAdjuster", "Admin"]);

    expect(screen.getByText("Role-protected content")).toBeInTheDocument();
  });

  it("blocks users without an allowed role", () => {
    renderWithRoles(["Customer"], ["ClaimsAdjuster", "Admin"]);

    expect(screen.queryByText("Role-protected content")).not.toBeInTheDocument();
    expect(screen.getByText(/do not have access/i)).toBeInTheDocument();
  });

  it("blocks users with no role claim at all", () => {
    renderWithRoles(undefined, ["ClaimsAdjuster"]);

    expect(screen.queryByText("Role-protected content")).not.toBeInTheDocument();
  });
});
