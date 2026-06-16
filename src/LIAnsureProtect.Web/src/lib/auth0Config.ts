const auth0Domain = import.meta.env.VITE_AUTH0_DOMAIN;
const auth0ClientId = import.meta.env.VITE_AUTH0_CLIENT_ID;
const auth0Audience = import.meta.env.VITE_AUTH0_AUDIENCE;
const auth0CallbackUrl = import.meta.env.VITE_AUTH0_CALLBACK_URL;

if (!auth0Domain) {
  throw new Error("Missing VITE_AUTH0_DOMAIN.");
}

if (!auth0ClientId) {
  throw new Error("Missing VITE_AUTH0_CLIENT_ID.");
}

if (!auth0Audience) {
  throw new Error("Missing VITE_AUTH0_AUDIENCE.");
}

if (!auth0CallbackUrl) {
  throw new Error("Missing VITE_AUTH0_CALLBACK_URL.");
}

export const auth0Config = {
  domain: auth0Domain,
  clientId: auth0ClientId,
  audience: auth0Audience,
  callbackUrl: auth0CallbackUrl,
};
