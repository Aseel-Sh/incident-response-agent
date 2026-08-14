import { useEffect, useRef } from "react";
import shellMarkup from "./shell.html?raw";

function IncidentConsoleShell() {
  const root = useRef(null);

  useEffect(() => {
    import("./incident-console-controller.js").then(() => {
      if (root.current) root.current.dataset.controllerReady = "true";
    });
  }, []);

  return (
    <div
      data-react-console
      data-controller-ready="false"
      ref={root}
      dangerouslySetInnerHTML={{ __html: shellMarkup }}
    />
  );
}

export default function App() {
  return <IncidentConsoleShell />;
}
