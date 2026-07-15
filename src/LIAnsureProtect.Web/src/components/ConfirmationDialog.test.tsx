import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

import { ConfirmationDialog } from "./ConfirmationDialog";

describe("ConfirmationDialog", () => {
  it("keeps supporting information collapsed until the user asks for more details", async () => {
    const user = userEvent.setup();

    render(
      <ConfirmationDialog
        title="Withdraw this application?"
        description="This stops the application journey."
        confirmLabel="Withdraw application"
        information={{
          title: "Why is withdrawal different from deletion?",
          description: "Withdrawal preserves the retained business record.",
        }}
        onCancel={vi.fn()}
        onConfirm={vi.fn()}
      />,
    );

    const disclosure = screen.getByRole("button", { name: "More details" });
    expect(disclosure).toHaveAttribute("aria-expanded", "false");
    expect(
      screen.queryByText("Why is withdrawal different from deletion?"),
    ).not.toBeInTheDocument();

    await user.click(disclosure);

    expect(disclosure).toHaveAttribute("aria-expanded", "true");
    expect(
      screen.getByText("Why is withdrawal different from deletion?"),
    ).toBeVisible();
    expect(
      screen.getByText("Withdrawal preserves the retained business record."),
    ).toBeVisible();
  });
});
