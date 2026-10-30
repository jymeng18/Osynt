import { growthMessage, milestones, plantCount } from "./progression.js";

export function createUi(actions) {
  const byId = (id) => document.getElementById(id);
  const notifyHost = (message) => window.chrome?.webview?.postMessage(message);
  let state = { successes: 0, atmosphere: "day", reducedMotion: false },
    toastTimeout;
  byId("day").addEventListener("click", () =>
    actions.preferences("day", state.reducedMotion),
  );
  byId("dusk").addEventListener("click", () =>
    actions.preferences("dusk", state.reducedMotion),
  );
  byId("motion").addEventListener("click", () =>
    actions.preferences(state.atmosphere, !state.reducedMotion),
  );
  byId("zoom-in").addEventListener("click", () => actions.zoom(1.2));
  byId("zoom-out").addEventListener("click", () => actions.zoom(1 / 1.2));
  byId("reset-camera").addEventListener("click", actions.reset);
  byId("close-plant").addEventListener(
    "click",
    () => (byId("plant-card").hidden = true),
  );
  byId("back").addEventListener("click", () => notifyHost({ type: "back" }));
  byId("open-milestones").addEventListener("click", () =>
    byId("milestones").showModal(),
  );
  byId("close-milestones").addEventListener("click", () =>
    byId("milestones").close(),
  );
  byId("milestones").addEventListener("close", () =>
    byId("open-milestones").focus(),
  );
  document.addEventListener("keydown", (event) => {
    // Keep dialog keyboard interaction local; Escape uses the native close behavior.
    if (byId("milestones").open) return;
    if ((event.ctrlKey || event.metaKey) && event.key === "Enter") {
      event.preventDefault();
      notifyHost({ type: "send" });
    }
    if (event.key === "Escape") {
      if (!byId("plant-card").hidden) byId("plant-card").hidden = true;
      else notifyHost({ type: "back" });
    }
  });
  return {
    notifyHost,
    update(next, celebrate = false) {
      state = next;
      document.body.classList.toggle("dusk", state.atmosphere === "dusk");
      document.body.classList.toggle("reduced-motion", state.reducedMotion);
      for (const mode of ["day", "dusk"]) {
        byId(mode).classList.toggle("active", state.atmosphere === mode);
        byId(mode).setAttribute(
          "aria-pressed",
          String(state.atmosphere === mode),
        );
      }
      byId("motion").setAttribute("aria-pressed", String(state.reducedMotion));
      byId("success-count").textContent = state.successes.toLocaleString();
      byId("plant-count").textContent = plantCount(
        state.successes,
      ).toLocaleString();
      byId("level").textContent = String(
        1 + Math.floor(Math.sqrt(state.successes / 5)),
      ).padStart(2, "0");
      const nextMilestone = milestones.find(([at]) => at > state.successes);
      const at =
        nextMilestone?.[0] ?? (Math.floor(state.successes / 100) + 1) * 100;
      byId("next-name").textContent = nextMilestone?.[1] ?? "More plants";
      byId("next-description").textContent =
        nextMilestone?.[2] ?? "Successful requests add and grow plants.";
      byId("remaining").textContent = `${at - state.successes} to go`;
      byId("milestone-fraction").textContent = `${state.successes} / ${at}`;
      byId("milestone-progress").style.width =
        `${Math.min(100, (state.successes / at) * 100)}%`;
      byId("milestone-list").replaceChildren(
        ...milestones.map(([required, name, detail]) => {
          const row = document.createElement("div");
          row.setAttribute("role", "listitem");
          row.className = "flex items-center gap-3 rounded-lg px-3 py-2.5";
          const icon = document.createElement("span");
          icon.className =
            "grid h-7 w-7 shrink-0 place-items-center rounded-full bg-[#edf2e3] text-xs text-fern";
          icon.textContent =
            state.successes >= required ? "✓" : String(required);
          const text = document.createElement("div"),
            title = document.createElement("strong"),
            description = document.createElement("p");
          title.className = "text-xs font-medium";
          title.textContent = name;
          description.className = "mt-0.5 text-xs text-[#68735f]";
          description.textContent = detail;
          const progress = document.createElement("p");
          progress.className = "mt-1 text-xs text-[#526b40]";
          progress.textContent =
            state.successes >= required
              ? "Unlocked"
              : `${required - state.successes} more successful ${required - state.successes === 1 ? "request" : "requests"}`;
          text.append(title, description, progress);
          row.append(icon, text);
          return row;
        }),
      );
      if (celebrate) {
        byId("growth-title").textContent = growthMessage(state.successes);
        byId("growth").classList.add("visible");
        clearTimeout(toastTimeout);
        toastTimeout = setTimeout(
          () => byId("growth").classList.remove("visible"),
          4300,
        );
      }
    },
    select(plant) {
      byId("plant-card").hidden = !plant;
      if (plant) {
        byId("plant-name").textContent = plant.name;
        byId("plant-detail").textContent = plant.detail;
      }
    },
    ready() {
      byId("loading").classList.add("done");
      notifyHost({ type: "ready" });
    },
    error(error) {
      byId("loading").classList.add("done");
      byId("render-error").hidden = false;
      byId("render-error-detail").textContent =
        `The 3D renderer couldn't start. Your progress is saved. ${error.message}`;
      notifyHost({ type: "renderer-error", message: error.message });
    },
  };
}
