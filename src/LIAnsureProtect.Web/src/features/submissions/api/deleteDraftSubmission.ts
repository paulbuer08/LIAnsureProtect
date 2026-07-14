const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function deleteDraftSubmission(accessToken: string, submissionId: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions/${submissionId}`, {
    method: "DELETE",
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  await ensureSuccess(response, { notFoundMessage: "Draft submission was not found." });
}
import { ensureSuccess } from "../../../lib/apiClient";
