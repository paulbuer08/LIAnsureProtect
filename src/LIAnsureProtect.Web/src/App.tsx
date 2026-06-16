import { Route, Routes } from "react-router";

import { RequireAuth } from "./components/RequireAuth";
import { DashboardPage } from "./pages/DashboardPage";
import { HomePage } from "./pages/HomePage";
import { LoginCallbackPage } from "./pages/LoginCallbackPage";
import "./App.css";

function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/callback" element={<LoginCallbackPage />} />
      <Route
        path="/dashboard"
        element={
          <RequireAuth>
            <DashboardPage />
          </RequireAuth>
        }
      />
    </Routes>
  );
}

export default App;
