import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router";
import { Auth0Provider } from "@auth0/auth0-react";

import { auth0Config } from "./lib/auth0Config";
import App from "./App.tsx";
import "./index.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <Auth0Provider
        domain={auth0Config.domain}
        clientId={auth0Config.clientId}
        authorizationParams={{
          audience: auth0Config.audience,
          redirect_uri: auth0Config.callbackUrl,
        }}
      >
        <App />
      </Auth0Provider>
    </BrowserRouter>
  </StrictMode>,
);

