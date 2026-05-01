import { App, applyDocumentTheme, applyHostStyleVariables, applyHostFonts } from "@modelcontextprotocol/ext-apps";

// ─── DOM helper ──────────────────────────────────────────────────────────────
const el = <T extends HTMLElement = HTMLElement>(id: string): T =>
  document.getElementById(id) as T;

// ─── Types ───────────────────────────────────────────────────────────────────
interface TimerInfo {
  id: string;
  state: "running" | "completed";
  startedAt: string;          // human-readable for display
  elapsedSeconds: number;     // last known from server
  lastPollTimestamp: number;  // performance.now() when elapsedSeconds was last set
}

interface SessionTimerData {
  timerId: string;
  state: string;
  elapsedSeconds: number;
  startedAt: string;
}

// ─── State ───────────────────────────────────────────────────────────────────
const timers = new Map<string, TimerInfo>();
let selectedTimerId: string | null = null;
let pollIntervalId: number | null = null;
let tickIntervalId: number | null = null;let sessionRefreshId: number | null = null;let polling = false;

// ─── Format ──────────────────────────────────────────────────────────────────
function formatSeconds(s: number): string {
  if (s >= 60) {
    const m = Math.floor(s / 60);
    const sec = Math.floor(s % 60);
    return `${m}m ${sec}s`;
  }
  return `${s.toFixed(1)}s`;
}

// ─── Rendering ───────────────────────────────────────────────────────────────
function getDisplayTime(timer: TimerInfo): string {
  if (timer.state === "running") {
    const delta = (performance.now() - timer.lastPollTimestamp) / 1000;
    return formatSeconds(Math.max(0, timer.elapsedSeconds + delta));
  }
  return formatSeconds(timer.elapsedSeconds);
}

function renderSelected(): void {
  const timer = selectedTimerId ? timers.get(selectedTimerId) : null;
  if (!timer) {
    el("icon").textContent = "⏱";
    el("timer").textContent = "--:--";
    el("state").textContent = "ready";
    el("state").className = "state";
    el("details").style.display = "none";
    updateButtons("idle");
    return;
  }

  const isRunning = timer.state === "running";
  el("icon").textContent = isRunning ? "⏳" : "✅";
  // Only update timer text when tick interval is not driving it
  if (tickIntervalId === null) {
    el("timer").textContent = getDisplayTime(timer);
  }
  el("state").textContent = timer.state;
  el("state").className = `state ${timer.state}`;
  el("details").style.display = "block";
  el("started").textContent = timer.startedAt;
  el("status").textContent = isRunning ? "In progress" : "Finished";
  updateButtons(timer.state);
}

function updateButtons(state: string): void {
  const btnStart = el<HTMLButtonElement>("btn-start");
  const btnStop = el<HTMLButtonElement>("btn-stop");
  if (state === "running") {
    btnStart.disabled = true;
    btnStop.disabled = false;
  } else {
    btnStart.disabled = false;
    btnStop.disabled = true;
  }
}

function updateDropdown(): void {
  const wrapper = el("timer-select-wrapper");
  const select = el<HTMLSelectElement>("timer-select");

  if (timers.size <= 1) {
    wrapper.style.display = "none";
    return;
  }

  wrapper.style.display = "block";
  select.innerHTML = "";
  for (const [id, timer] of timers) {
    const option = document.createElement("option");
    option.value = id;
    option.textContent = `Timer ${id} — ${getDisplayTime(timer)} (${timer.state})`;
    option.selected = id === selectedTimerId;
    select.appendChild(option);
  }
  // Ensure select reflects the current selectedTimerId
  if (selectedTimerId) select.value = selectedTimerId;
}

// ─── Polling (selected timer only) ───────────────────────────────────────────
function startPolling(timerId: string): void {
  stopPolling();
  selectedTimerId = timerId;
  const timer = timers.get(timerId);
  if (!timer || timer.state !== "running") return;

  timer.lastPollTimestamp = performance.now();

  // 1s authoritative server poll
  pollIntervalId = window.setInterval(() => { void pollElapsed(timerId); }, 1000);

  // 100ms display tick for smooth tenths-of-second updates
  tickIntervalId = window.setInterval(() => {
    const t = selectedTimerId ? timers.get(selectedTimerId) : null;
    if (!t || t.state !== "running") return;
    el("timer").textContent = getDisplayTime(t);
    // Refresh dropdown labels periodically (every ~2s via modulo on calls)
    if (Math.round((performance.now() / 100)) % 20 === 0) updateDropdown();
  }, 100);
}

function stopPolling(): void {
  if (pollIntervalId !== null) { clearInterval(pollIntervalId); pollIntervalId = null; }
  if (tickIntervalId !== null) { clearInterval(tickIntervalId); tickIntervalId = null; }
}

async function pollElapsed(timerId: string): Promise<void> {
  if (polling) return;
  polling = true;
  try {
    const result = await app.callServerTool({ name: "get_elapsed", arguments: { timer_id: timerId } });
    const content = result.content as Array<{ type: string; text?: string }> | undefined;
    const text = content?.find((c) => c.type === "text" && c.text)?.text;
    if (!text) return;
    try {
      const data = JSON.parse(text) as { elapsedSeconds?: number; state?: string };
      const timer = timers.get(timerId);
      if (timer) {
        if (typeof data.elapsedSeconds === "number") {
          timer.elapsedSeconds = data.elapsedSeconds;
          timer.lastPollTimestamp = performance.now();
        }
        if (data.state === "completed" && timer.state === "running") {
          timer.state = "completed";
          stopPolling();
          renderSelected();
          updateDropdown();
        }
      }
    } catch { /* not JSON — ignore */ }
  } catch (err) {
    console.error("Poll failed:", err);
  } finally {
    polling = false;
  }
}

// ─── Timer map operations ─────────────────────────────────────────────────────
function applyStartResult(text: string): void {
  const idMatch = text.match(/Your timer ID is:\s*(\w+)/);
  const timeMatch = text.match(/at (\d{2}:\d{2}:\d{2})/);
  if (!idMatch?.[1]) return;
  const id = idMatch[1];
  timers.set(id, {
    id,
    state: "running",
    startedAt: timeMatch?.[1] ?? new Date().toLocaleTimeString(),
    elapsedSeconds: 0,
    lastPollTimestamp: performance.now(),
  });
  startPolling(id);   // sets selectedTimerId = id, starts intervals
  updateDropdown();
  renderSelected();
}

function applyStopResult(text: string): void {
  // Extract timer ID: "Timer ID: abc12345"
  const idMatch = text.match(/Timer ID:\s*(\w+)/);
  const totalMatch = text.match(/Total:\s*(.+)/m);
  const id = idMatch?.[1] ?? selectedTimerId;
  if (id) {
    const timer = timers.get(id);
    if (timer) {
      timer.state = "completed";
      // Convert "Total: 1m 23s" or "Total: 4.2s" to elapsedSeconds
      const totalStr = totalMatch?.[1]?.trim() ?? "";
      const minSec = totalStr.match(/(\d+)m\s*(\d+)s/);
      const secOnly = totalStr.match(/([\d.]+)s/);
      if (minSec) {
        timer.elapsedSeconds = parseInt(minSec[1]) * 60 + parseInt(minSec[2]);
      } else if (secOnly) {
        timer.elapsedSeconds = parseFloat(secOnly[1]);
      }
      timer.lastPollTimestamp = performance.now();
    }
    if (id === selectedTimerId) {
      stopPolling();
      renderSelected();
    }
  } else {
    stopPolling();
    renderSelected();
  }
  updateDropdown();
}

async function loadSessionTimers(): Promise<void> {
  try {
    const result = await app.callServerTool({ name: "get_session_timers", arguments: {} });
    const content = result.content as Array<{ type: string; text?: string }> | undefined;
    const text = content?.find((c) => c.type === "text" && c.text)?.text;
    if (!text) return;

    const data = JSON.parse(text) as { timers: SessionTimerData[] };
    let changed = false;

    for (const t of data.timers) {
      const serverState = t.state === "running" ? "running" : "completed" as const;
      const existing = timers.get(t.timerId);
      if (!existing) {
        // New timer detected (e.g., model called start_timer while UI was open)
        timers.set(t.timerId, {
          id: t.timerId,
          state: serverState,
          startedAt: t.startedAt ? new Date(t.startedAt).toLocaleTimeString() : "—",
          elapsedSeconds: t.elapsedSeconds,
          lastPollTimestamp: performance.now(),
        });
        changed = true;
      } else if (existing.state === "running" && serverState === "completed") {
        // Timer was stopped externally (model called stop_timer)
        existing.state = "completed";
        existing.elapsedSeconds = t.elapsedSeconds;
        existing.lastPollTimestamp = performance.now();
        if (t.timerId === selectedTimerId) stopPolling();
        changed = true;
      }
    }

    if (!changed && timers.size > 0) return; // nothing new to render

    // Auto-select: prefer first running timer, else first timer
    if (!selectedTimerId || !timers.has(selectedTimerId)) {
      const running = [...timers.values()].find((t) => t.state === "running");
      const first = running ?? [...timers.values()][0];
      if (first) {
        if (first.state === "running") {
          startPolling(first.id);
        } else {
          selectedTimerId = first.id;
        }
      }
    } else if (changed) {
      // A new running timer appeared — auto-switch if user has nothing running
      const selectedTimer = timers.get(selectedTimerId);
      if (selectedTimer?.state !== "running") {
        const newRunning = [...timers.values()].find((t) => t.state === "running" && t.id !== selectedTimerId);
        if (newRunning) startPolling(newRunning.id);
      }
    }
    updateDropdown();
    renderSelected();
  } catch (err) {
    console.error("loadSessionTimers failed:", err);
  }
}

// ─── Button handlers ──────────────────────────────────────────────────────────
async function handleStart(): Promise<void> {
  el<HTMLButtonElement>("btn-start").disabled = true;
  try {
    const result = await app.callServerTool({ name: "start_timer", arguments: {} });
    const content = result.content as Array<{ type: string; text?: string }> | undefined;
    const text = content?.find((c) => c.type === "text" && c.text)?.text;
    if (text) applyStartResult(text);
  } catch (err) {
    console.error("Start failed:", err);
    el<HTMLButtonElement>("btn-start").disabled = false;
  }
}

async function handleStop(): Promise<void> {
  if (!selectedTimerId) return;
  el<HTMLButtonElement>("btn-stop").disabled = true;
  const timerId = selectedTimerId;
  try {
    const result = await app.callServerTool({ name: "stop_timer", arguments: { timer_id: timerId } });
    const content = result.content as Array<{ type: string; text?: string }> | undefined;
    const text = content?.find((c) => c.type === "text" && c.text)?.text;
    if (text) applyStopResult(text);
  } catch (err) {
    console.error("Stop failed:", err);
    el<HTMLButtonElement>("btn-stop").disabled = false;
  }
}

// ─── MCP App lifecycle ────────────────────────────────────────────────────────
const app = new App({ name: "Timer", version: "1.0.0" });

// Handle tool input arriving from the host (model-initiated calls).
// Note: params.arguments is available but there is no tool name field.
// Heuristic: timer_id in args → stop_timer; no args → start_timer or open_timer.
app.ontoolinput = (params) => {
  const timerId = params.arguments?.["timer_id"] as string | undefined;
  if (timerId) {
    // stop_timer is being called — optimistically disable Stop for that timer
    if (selectedTimerId === timerId) {
      el<HTMLButtonElement>("btn-stop").disabled = true;
    }
  } else {
    // start_timer or open_timer — disable Start button until result arrives
    el<HTMLButtonElement>("btn-start").disabled = true;
  }
};

// Handle tool results pushed by the host (model-initiated calls).
// CallToolResult has only a content array — no tool name.
// Dispatch by matching the result text content.
app.ontoolresult = (params) => {
  const content = params.content as Array<{ type: string; text?: string }> | undefined;
  const text = content?.find((c) => c.type === "text" && c.text)?.text;
  if (!text) return;

  if (text.includes("Timer started")) {
    // start_timer result
    applyStartResult(text);
  } else if (text.includes("Timer complete") || text.includes("Total:")) {
    // stop_timer result
    applyStopResult(text);
  } else {
    // Try JSON — could be open_timer {"state":"idle"} or an error
    try {
      const parsed = JSON.parse(text) as Record<string, unknown>;
      if (parsed["state"] === "idle") {
        // open_timer result — render idle state and refresh session timer list
        if (timers.size === 0) renderSelected();
        void loadSessionTimers();
      }
    } catch {
      // Unknown text — re-enable buttons so user isn't stuck
      renderSelected();
    }
  }
};

// Handle host context changes: theme, CSS variables, fonts, safe area insets
app.onhostcontextchanged = (ctx) => {
  if (ctx.theme) applyDocumentTheme(ctx.theme);
  if (ctx.styles?.variables) applyHostStyleVariables(ctx.styles.variables);
  if (ctx.styles?.css?.fonts) applyHostFonts(ctx.styles.css.fonts);
  if (ctx.safeAreaInsets) {
    const { top, right, bottom, left } = ctx.safeAreaInsets;
    document.body.style.padding = `${top}px ${right}px ${bottom}px ${left}px`;
  }
};

app.onteardown = async () => {
  stopPolling();
  if (sessionRefreshId !== null) clearInterval(sessionRefreshId);
  return {};
};

// Connect — ALL handlers must be registered before this call
await app.connect();

// Apply initial host context
const initCtx = app.getHostContext();
if (initCtx?.theme) applyDocumentTheme(initCtx.theme);
if (initCtx?.styles?.variables) applyHostStyleVariables(initCtx.styles.variables);
if (initCtx?.styles?.css?.fonts) applyHostFonts(initCtx.styles.css.fonts);

// Load any timers already running in this session
await loadSessionTimers();

// If no session timers, show idle state
if (timers.size === 0) renderSelected();

// Background session refresh — detects model-initiated start_timer/stop_timer calls.
// Runs every 2.5s; merges new timers and state changes into the local map.
sessionRefreshId = window.setInterval(() => { void loadSessionTimers(); }, 2500);

// ─── Event bindings ───────────────────────────────────────────────────────────
el("btn-start").addEventListener("click", () => { void handleStart(); });
el("btn-stop").addEventListener("click", () => { void handleStop(); });

el<HTMLSelectElement>("timer-select").addEventListener("change", (e) => {
  const newId = (e.target as HTMLSelectElement).value;
  if (newId === selectedTimerId) return;
  stopPolling();
  selectedTimerId = newId;
  const timer = timers.get(newId);
  if (timer?.state === "running") {
    startPolling(newId);
  }
  renderSelected();
});

// Pause polling when scrolled offscreen, resume when visible
const observer = new IntersectionObserver((entries) => {
  entries.forEach((entry) => {
    if (entry.isIntersecting) {
      if (selectedTimerId) {
        const timer = timers.get(selectedTimerId);
        if (timer?.state === "running" && pollIntervalId === null) {
          startPolling(selectedTimerId);
        }
      }
    } else {
      stopPolling();
    }
  });
});
observer.observe(document.body);

