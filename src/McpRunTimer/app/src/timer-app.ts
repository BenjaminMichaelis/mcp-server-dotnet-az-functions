import { App } from "@modelcontextprotocol/ext-apps";

const el = (id: string) => document.getElementById(id)!;

interface TimerData {
  // Tool result is a string like "Running for 12.3s" or "Run complete!..."
  // We parse what we can from the text
  elapsed?: string;
  state?: string;
  startedAt?: string;
  completedAt?: string;
  checkedAt?: string;
}

function applyTheme(theme: string | undefined): void {
  document.documentElement.dataset.theme = theme || "dark";
}

function parseToolResult(text: string): { state: string; display: string; started?: string } {
  if (text.includes("Timer started")) {
    const match = text.match(/at (\d{2}:\d{2}:\d{2})/);
    return { state: "running", display: "0.0s", started: match?.[1] };
  }
  if (text.includes("Running for")) {
    const match = text.match(/Running for (.+)/);
    return { state: "running", display: match?.[1] || "..." };
  }
  if (text.includes("Run complete") || text.includes("Total:")) {
    const match = text.match(/Total:\s*(.+)/m);
    return { state: "completed", display: match?.[1]?.trim() || "Done" };
  }
  if (text.includes("No run in progress")) {
    return { state: "idle", display: "--:--" };
  }
  // Try JSON (resource format)
  try {
    const data: TimerData = JSON.parse(text);
    return {
      state: data.state || "idle",
      display: data.elapsed || "--:--",
      started: data.startedAt ? new Date(data.startedAt).toLocaleTimeString() : undefined,
    };
  } catch {
    return { state: "idle", display: "--:--" };
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
const app = new App({ name: "Run Timer" });

app.on("toolresult", (event) => {
  const content = event.result?.content;
  if (content && Array.isArray(content) && content.length > 0) {
    const textBlock = content.find((c: { type: string }) => c.type === "text");
    if (textBlock && "text" in textBlock) {
      render(textBlock.text as string);
    }
  }
});

app.on("themechange", (event) => {
  applyTheme(event.theme);
});

app.ready();
