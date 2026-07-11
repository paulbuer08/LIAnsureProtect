import { act, render, screen } from "@testing-library/react";
import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { TransientStatusMessage } from "./TransientStatusMessage";

function StatusMessageHarness() {
  const [isVisible, setIsVisible] = useState(true);

  return isVisible ? (
    <TransientStatusMessage onDismiss={() => setIsVisible(false)}>
      Draft details updated.
    </TransientStatusMessage>
  ) : null;
}

describe("TransientStatusMessage", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("fades and is removed from the DOM after five seconds", () => {
    vi.useFakeTimers();
    render(<StatusMessageHarness />);

    const message = screen.getByRole("status");
    expect(message).toHaveTextContent("Draft details updated.");

    act(() => vi.advanceTimersByTime(4_500));
    expect(message).toHaveClass("opacity-0");

    act(() => vi.advanceTimersByTime(500));
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });
});
