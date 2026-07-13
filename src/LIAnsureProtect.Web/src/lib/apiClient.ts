import { z } from "zod";
import { captureSafeEvent } from "./telemetry";

const correlationHeaderName = "X-Correlation-ID";

const problemDetailsSchema = z
  .object({
    title: z.string().optional(),
    status: z.number().optional(),
    detail: z.string().optional(),
    code: z.string().optional(),
    correlationId: z.string().optional(),
    errors: z.record(z.string(), z.array(z.string())).optional(),
  })
  .passthrough();

type ProblemDetails = z.infer<typeof problemDetailsSchema>;

const knownMessages: Record<string, string> = {
  "quote.reassessment.no_changes":
    "Change at least one control answer before creating a reassessment.",
};

export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly correlationId?: string;
  readonly fieldErrors?: Record<string, string[]>;

  constructor(
    message: string,
    status: number,
    code?: string,
    correlationId?: string,
    fieldErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.correlationId = correlationId;
    this.fieldErrors = fieldErrors;
  }
}

function defaultMessage(status: number, notFoundMessage?: string) {
  if (status === 401) return "Your session has expired. Please sign in again.";
  if (status === 403) return "You do not have permission to do that.";
  if (status === 404) return notFoundMessage ?? "The requested record was not found.";
  if (status === 429) return "Too many requests were sent. Please wait a moment and try again.";
  if (status >= 500) return "We could not complete that request. Please try again.";

  return "We could not complete that request. Review the information and try again.";
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  const contentType = response.headers.get("Content-Type") ?? "";
  if (!contentType.includes("json")) return undefined;

  try {
    const parsed: unknown = await response.json();
    const result = problemDetailsSchema.safeParse(parsed);
    return result.success ? result.data : undefined;
  } catch {
    return undefined;
  }
}

export async function ensureSuccess(
  response: Response,
  options: { notFoundMessage?: string } = {},
) {
  if (response.ok) return response;

  const problem = await readProblem(response);
  const correlationId =
    problem?.correlationId ?? response.headers.get(correlationHeaderName) ?? undefined;
  const message =
    (problem?.code ? knownMessages[problem.code] : undefined) ??
    (problem?.code ? problem.detail : undefined) ??
    (problem?.errors
      ? Object.values(problem.errors).flat().find(Boolean)
      : undefined) ??
    (response.status < 500 ? problem?.detail : undefined) ??
    defaultMessage(response.status, options.notFoundMessage);

  captureSafeEvent("api_request_failed", {
    status: response.status,
    code: problem?.code,
    correlationId,
  });

  throw new ApiError(
    correlationId ? `${message} Support ID: ${correlationId}` : message,
    response.status,
    problem?.code,
    correlationId,
    problem?.errors,
  );
}

export async function parseJsonResponse<T>(
  response: Response,
  options: { notFoundMessage?: string } = {},
) {
  await ensureSuccess(response, options);
  return (await response.json()) as T;
}

export function getUserErrorMessage(error: unknown, fallback: string) {
  return error instanceof ApiError ? error.message : fallback;
}
