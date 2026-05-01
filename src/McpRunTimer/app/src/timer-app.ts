import { App } from "@modelcontextprotocol/ext-apps";

const el = (id: string) => document.getElementById(id)!;

interface TimerData {
  runId?: string;
  elapsed?: string;
  elapsedSeconds?: number;
  state?: string;
  startedAt?: string;
}

function applyTheme(theme: string | undefined): void {
  document.documentElement.dataset.theme = theme || "dark";
}

function parseToolResult(text: string): { state: string; display: string; started?: string } {
  // start_run returns plain text
  if (text.includes("Timer started")) {
    const match = text.match(/at (\d{2}:\d{2}:\d{2})/);
    return { state: "running", display: "0.0s", started: match?.[1] };
  }
  // stop_run returns plain text
  if (text.includes("Run complete") || text.includes("Total:")) {
    const match = text.match(/Total:\s*(.+)/m);
    return { state: "completed", display: match?.[1]?.trim() || "Done" };
  }
  if (text.includes("No run found") || text.includes("already stopped")) {
    return { state: "idle", display: "--:--" };
  }
  // get_elapsed returns JSON
  try {
    const data: TimerData = JSON.parse(text);
    return {
      state: data.state || "idle",
      display: data.elapsed || "--:--",
      started: data.startedAt ? new Date(data.startedAt).toLocaleTimeString() : undefined,
    };
  } catch {
    // Fallback for unexpected text
    return { state: "idle", display: text.substring(0, 20) };
  }
}

function render(text: string): void {
  const { state, display, started } = parseToolResult(text);

  el("timer").textContent = display;
  el("state").textContent = state;
  el("state").className = `state ${state}`;

  if (state === "running") {
    el("icon").textContent = "🏃";
  } else if (state === "completed") {
    el("icon").textContent = "🎉";
  } else {
    el("icon").textContent = "⏱️";
  }

  if (started || state !== "idle") {
    el("details").style.display = "block";
    if (started) el("started").textContent = started;
    el("status").textContent =
      state === "running" ? "In progress" :
      state === "completed" ? "Finished" : "Ready";
  }
}

// Initialize MCP App
const app = new App({ name: "Run Timer", version: "1.0.0" });

// Handle tool results from the server
app.ontoolresult = (params) => {
  console.log("Tool result content:", params.content);
  const content = params.content as Array<{ type: string; text?: string }>;
  if (content && content.length > 0) {
    const textBlock = content.find((c) => c.type === "text" && c.text);
    if (textBlock && textBlock.text) {
      render(textBlock.text);
    }
  }
};

// Handle host context changes (theme)
app.onhostcontextchanged = (ctx) => {
  if (ctx.theme) applyTheme(ctx.theme);
};

// Connect to host
await app.connect();

// Apply initial theme
applyTheme(app.getHostContext()?.theme);
