import { describe, expect, it } from "vitest";

import { ApiError, ensureSuccess, getUserErrorMessage } from "./apiClient";

function problemResponse(status: number, body: object, correlationId?: string) {
  const headers = new Headers({ "Content-Type": "application/problem+json" });
  if (correlationId) headers.set("X-Correlation-ID", correlationId);

  return new Response(JSON.stringify(body), { status, headers });
}

describe("apiClient error boundary", () => {
  it("maps a stable business code to friendly guidance and a support ID", async () => {
    const response = problemResponse(
      409,
      {
        title: "Quote cannot be created.",
        detail: "Internal wording that is not the client contract.",
        status: 409,
        code: "quote.reassessment.no_changes",
        correlationId: "corr-123",
      },
    );

    await expect(ensureSuccess(response)).rejects.toMatchObject({
      name: "ApiError",
      status: 409,
      code: "quote.reassessment.no_changes",
      correlationId: "corr-123",
      message:
        "Change at least one control answer before creating a reassessment. Support ID: corr-123",
    });
  });

  it("keeps an expected 4xx business detail readable without exposing JSON", async () => {
    const response = problemResponse(
      409,
      {
        title: "Claim cannot be assigned.",
        detail: "This claim is already assigned to another adjuster.",
        status: 409,
      },
      "corr-456",
    );

    await expect(ensureSuccess(response)).rejects.toMatchObject({
      message:
        "This claim is already assigned to another adjuster. Support ID: corr-456",
    });
  });

  it("hides unexpected 5xx details and uses a safe retry message", async () => {
    const response = problemResponse(500, {
      title: "Unexpected error.",
      detail: "SqlException: password=secret; table=claims",
      status: 500,
    });

    await expect(ensureSuccess(response)).rejects.toMatchObject({
      message: "We could not complete that request. Please try again.",
    });
  });

  it("surfaces validation guidance but hides arbitrary local errors", async () => {
    const validationResponse = problemResponse(400, {
      title: "Validation failed.",
      status: 400,
      errors: { attestedByName: ["Enter the name of the attesting person."] },
    });

    await expect(ensureSuccess(validationResponse)).rejects.toMatchObject({
      message: "Enter the name of the attesting person.",
      fieldErrors: {
        attestedByName: ["Enter the name of the attesting person."],
      },
    });

    expect(
      getUserErrorMessage(
        new Error("API request failed with raw developer details"),
        "Unable to save your changes.",
      ),
    ).toBe("Unable to save your changes.");
    expect(getUserErrorMessage(new ApiError("Safe message", 409), "Fallback")).toBe(
      "Safe message",
    );
  });
});
