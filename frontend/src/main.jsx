import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App.jsx";
import "./styles.css";
import "./console.css";
import "./lifecycle.css";

const savedTheme = localStorage.getItem("incidentops.theme");
document.documentElement.dataset.theme = savedTheme === "dark" ? "dark" : "light";

createRoot(document.getElementById("root")).render(
  <StrictMode>
    <App />
  </StrictMode>
);
