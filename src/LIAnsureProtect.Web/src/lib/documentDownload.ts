/**
 * Downloads a private document through an authenticated fetch. The download endpoints are
 * JWT-guarded, so a bare <a href> cannot reach them — the bytes are fetched with the bearer
 * token and handed to the browser as a blob save instead.
 */
export async function downloadDocumentWithToken(
  url: string,
  accessToken: string,
  fileName: string,
) {
  const response = await fetch(url, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  });

  if (!response.ok) {
    throw new Error(`Document download failed with ${response.status}.`);
  }

  saveBlobAs(await response.blob(), fileName);
}

function saveBlobAs(blob: Blob, fileName: string) {
  const objectUrl = URL.createObjectURL(blob);

  try {
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    URL.revokeObjectURL(objectUrl);
  }
}
