import { useEffect, useRef } from "react";
import DetailEnhancements from "./DetailEnhancements.jsx";
import ConsoleLayout from "./ConsoleLayout.jsx";

export default function IncidentConsoleShell() {
  const root = useRef(null);

  useEffect(() => {
    import("./incident-console-controller.js").then(() => {
      if (root.current) root.current.dataset.controllerReady = "true";
    });
  }, []);

  return <div data-react-console data-controller-ready="false" ref={root}><ConsoleLayout /><DetailEnhancements /></div>;
}
