import { describe, expect, it } from "vitest";

import type { CurrentUser } from "../../lib/currentUserApi";
import type { NotificationInboxItem } from "./types";
import { resolveNotificationAction } from "./notificationActionResolver";

const underwriter: CurrentUser = {
  userId: "underwriter-1",
  email: "underwriter@example.com",
  roles: ["Underwriter"],
  capabilities: ["Quotes.Underwrite", "Notifications.Read"],
};

function evidenceNotification(scope: "personal" | "team"): NotificationInboxItem {
  return {
    notificationId: `evidence-${scope}`,
    scope,
    audience: scope === "team" ? "underwriting-operations" : "customer-or-broker",
    type: "evidence_request.responded",
    title: "Evidence response received",
    subjectReferenceType: "evidence-request",
    subjectReferenceId: "evidence-1",
    attributes: { evidenceRequestId: "evidence-1", quoteId: "quote-1" },
    occurredAtUtc: "2026-07-16T09:00:00Z",
    isRead: false,
    readAtUtc: null,
  };
}

describe("resolveNotificationAction", () => {
  it("routes underwriting evidence work to the workbench deep link", () => {
    expect(resolveNotificationAction(evidenceNotification("team"), underwriter)).toEqual({
      label: "Review evidence response",
      to: "/underwriting/quote-referrals?quoteId=quote-1&evidenceRequestId=evidence-1",
    });
  });

  it("does not expose a customer evidence route to an underwriter", () => {
    expect(resolveNotificationAction(evidenceNotification("personal"), underwriter)).toBeNull();
  });
});
