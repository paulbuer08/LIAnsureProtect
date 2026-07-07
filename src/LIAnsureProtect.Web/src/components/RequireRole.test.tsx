import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { describe, expect, it, vi } from "vitest";

import { useCurrentUser } from "../hooks/useCurrentUser";
import { RequireRole } from "./RequireRole";

vi.mock("../hooks/useCurrentUser", () => ({
  useCurrentUser: vi.fn(),
}));

function renderGuard(allowed: string[]) {
  return render(
    <MemoryRouter>
      <RequireRole allowedRoles={allowed}>
        <p>Role-protected content</p>
      </RequireRole>
    </MemoryRouter>,
  );
}

describe("RequireRole", () => {
  it("renders children when the caller holds an allowed role", () => {
    vi.mocked(useCurrentUser).mockReturnValue({
      data: { userId: "adjuster-1", email: null, roles: ["ClaimsAdjuster"] },
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useCurrentUser>);

    renderGuard(["ClaimsAdjuster", "Admin"]);

    expect(screen.getByText("Role-protected content")).toBeInTheDocument();
  });

  it("shows a loading state while the caller's roles are being fetched", () => {
    vi.mocked(useCurrentUser).mockReturnValue({
      data: undefined,
      isPending: true,
      isError: false,
    } as unknown as ReturnType<typeof useCurrentUser>);

    renderGuard(["ClaimsAdjuster"]);

    expect(screen.queryByText("Role-protected content")).not.toBeInTheDocument();
    expect(screen.getByText(/checking access/i)).toBeInTheDocument();
  });

  it("blocks a caller who lacks an allowed role", () => {
    vi.mocked(useCurrentUser).mockReturnValue({
      data: { userId: "customer-1", email: null, roles: ["Customer"] },
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useCurrentUser>);

    renderGuard(["ClaimsAdjuster", "Admin"]);

    expect(screen.queryByText("Role-protected content")).not.toBeInTheDocument();
    expect(screen.getByText(/do not have access/i)).toBeInTheDocument();
  });

  it("gives a specific message when the caller has no roles assigned at all", () => {
    vi.mocked(useCurrentUser).mockReturnValue({
      data: { userId: "nobody-1", email: null, roles: [] },
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useCurrentUser>);

    renderGuard(["ClaimsAdjuster"]);

    expect(screen.queryByText("Role-protected content")).not.toBeInTheDocument();
    expect(screen.getByText(/no roles assigned/i)).toBeInTheDocument();
  });

  it("shows an error state when the roles lookup fails", () => {
    vi.mocked(useCurrentUser).mockReturnValue({
      data: undefined,
      isPending: false,
      isError: true,
    } as unknown as ReturnType<typeof useCurrentUser>);

    renderGuard(["ClaimsAdjuster"]);

    expect(screen.queryByText("Role-protected content")).not.toBeInTheDocument();
    expect(screen.getByText(/could not verify/i)).toBeInTheDocument();
  });
});
