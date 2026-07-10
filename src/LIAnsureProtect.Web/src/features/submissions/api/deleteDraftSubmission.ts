const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5223";

export async function deleteDraftSubmission(accessToken: string, submissionId: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/submissions/${submissionId}`, {
    method: "DELETE",
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!response.ok) {
    throw new Error((await response.text()) || "Unable to delete the draft submission.");
  }
}
