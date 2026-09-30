// SuperDictate website: scroll reveals and the recreated-UI demos.
// No innerHTML anywhere: the CSP enforces Trusted Types, so text only goes in as text.
"use strict";

document.documentElement.classList.add("js");

const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

/** Resolves after `ms`, or rejects as soon as the scene is stopped. */
function wait(ms, signal) {
  return new Promise((resolve, reject) => {
    if (signal.aborted) return reject(new DOMException("stopped", "AbortError"));
    const timer = setTimeout(resolve, ms);
    signal.addEventListener("abort", () => {
      clearTimeout(timer);
      reject(new DOMException("stopped", "AbortError"));
    }, { once: true });
  });
}

/**
 * Plays `run` in a loop while `el` is on screen and stops it when it scrolls away,
 * so nothing animates off-screen. With reduced motion, `still` shows the end state instead.
 */
function scene(el, run, still) {
  if (!el) return;
  if (reduceMotion) {
    still();
    return;
  }
  let controller = null;
  const observer = new IntersectionObserver(([entry]) => {
    if (entry.isIntersecting && !controller) {
      controller = new AbortController();
      loop(controller.signal);
    } else if (!entry.isIntersecting && controller) {
      controller.abort();
      controller = null;
    }
  }, { threshold: 0.3 });
  observer.observe(el);

  async function loop(signal) {
    try {
      for (;;) await run(signal);
    } catch (error) {
      if (error.name !== "AbortError") throw error;
    }
  }
}

/* ---------- Reveal on scroll ---------- */

const reveals = document.querySelectorAll(".reveal");
if (reduceMotion || !("IntersectionObserver" in window)) {
  reveals.forEach((el) => el.classList.add("in"));
} else {
  const revealer = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (!entry.isIntersecting) continue;
      entry.target.classList.add("in");
      revealer.unobserve(entry.target);
    }
  }, { rootMargin: "0px 0px -8% 0px", threshold: 0.12 });
  reveals.forEach((el) => revealer.observe(el));
}

/* ---------- Hero: press Right Alt, speak, press again, the sentence is pasted ---------- */

(() => {
  const stage = document.getElementById("hero-stage");
  if (!stage) return;
  const capsule = stage.querySelector(".capsule");
  const label = capsule.querySelector(".cap-label");
  const langChip = capsule.querySelector(".cap-lang");
  const key = stage.querySelector(".key");
  const body = stage.querySelector(".doc-body");
  const caret = body.querySelector(".caret");
  const samples = [...stage.querySelectorAll(".samples li")].map((li) => ({ lang: li.dataset.lang, text: li.textContent }));

  function line(sample) {
    const p = document.createElement("p");
    p.className = "doc-line";
    const chip = document.createElement("span");
    chip.className = "doc-line-lang";
    chip.textContent = sample.lang;
    p.append(chip, document.createTextNode(sample.text));
    return p;
  }

  function setState(state) {
    capsule.dataset.state = state;
    label.textContent = capsule.dataset[state];
  }

  async function press(signal) {
    key.classList.add("is-down");
    await wait(160, signal);
    key.classList.remove("is-down");
  }

  // The note starts with one sentence in it, so the window never opens empty.
  const seed = line(samples[0]);
  seed.classList.add("is-seed");
  body.insertBefore(seed, caret);

  let index = 1;
  scene(stage, async (signal) => {
    const sample = samples[index++ % samples.length];
    await wait(500, signal);
    await press(signal);
    langChip.textContent = sample.lang;
    setState("listening");
    capsule.classList.add("is-on");
    await wait(2600, signal);
    await press(signal);
    setState("processing");
    await wait(900, signal);
    body.insertBefore(line(sample), caret);
    setState("pasted");
    await wait(1100, signal);
    capsule.classList.remove("is-on");
    await wait(1400, signal);
    // Keep the note tidy: never more than three pasted lines.
    const lines = body.querySelectorAll(".doc-line");
    if (lines.length >= 3) lines[0].remove();
  }, () => {
    body.insertBefore(line(samples[1]), caret);
    langChip.textContent = samples[1].lang;
    setState("listening");
    capsule.classList.add("is-on");
  });
})();

/* ---------- Steps: the key keeps being pressed ---------- */

(() => {
  const key = document.querySelector(".key-press");
  if (!key) return;
  scene(key.closest(".steps"), async (signal) => {
    await wait(1400, signal);
    key.classList.add("is-down");
    await wait(180, signal);
    key.classList.remove("is-down");
  }, () => {});
})();

/* ---------- Any app: the same dictation lands in four apps ---------- */

(() => {
  const stage = document.getElementById("apps-stage");
  if (!stage) return;
  const apps = [...stage.querySelectorAll(".app")];
  const fill = (target) => {
    target.textContent = target.dataset.text;
    target.classList.add("is-typed");
  };
  const clear = (target) => {
    target.textContent = "";
    target.classList.remove("is-typed");
  };

  // Every app starts filled; in turn, each one is dictated into again.
  scene(stage, async (signal) => {
    for (const app of apps) {
      const target = app.querySelector(".app-target");
      app.classList.add("is-active");
      clear(target);
      await wait(1100, signal);
      fill(target);
      await wait(1700, signal);
      app.classList.remove("is-active");
    }
    await wait(1200, signal);
  }, () => {});
})();

/* ---------- Mic button: drag it into place, click, talk, click ---------- */

(() => {
  const stage = document.getElementById("mic-stage");
  if (!stage) return;
  const button = stage.querySelector(".micbtn-drag");
  const cursor = stage.querySelector(".cursor");
  const text = stage.querySelector(".reply-text");

  async function click(signal) {
    button.classList.add("is-pressed");
    await wait(150, signal);
    button.classList.remove("is-pressed");
  }

  scene(stage, async (signal) => {
    text.textContent = "";
    text.classList.remove("is-typed");
    button.classList.remove("is-parked", "is-recording");
    cursor.classList.remove("at-button", "at-parked");
    await wait(800, signal);
    cursor.classList.add("at-button");
    await wait(1200, signal);
    // Drag: button and cursor travel together.
    button.classList.add("is-parked");
    cursor.classList.remove("at-button");
    cursor.classList.add("at-parked");
    await wait(1300, signal);
    await click(signal);
    button.classList.add("is-recording");
    await wait(2400, signal);
    await click(signal);
    button.classList.remove("is-recording");
    await wait(700, signal);
    text.textContent = text.dataset.text;
    text.classList.add("is-typed");
    await wait(3000, signal);
    cursor.classList.remove("at-parked");
    await wait(600, signal);
  }, () => {
    button.classList.add("is-parked");
    text.textContent = text.dataset.text;
  });
})();

/* ---------- AI cleanup: mark what goes, then show what's pasted ---------- */

(() => {
  const stage = document.getElementById("cleanup-stage");
  if (!stage) return;
  const before = stage.querySelector(".clean-before");

  scene(stage, async (signal) => {
    stage.classList.remove("is-done");
    before.classList.remove("is-marking");
    await wait(1200, signal);
    before.classList.add("is-marking");
    await wait(1600, signal);
    stage.classList.add("is-done");
    await wait(4200, signal);
  }, () => {
    before.classList.add("is-marking");
    stage.classList.add("is-done");
  });
})();

/* ---------- Languages: each one lights up as it's detected ---------- */

(() => {
  const stage = document.getElementById("langs-stage");
  if (!stage) return;
  const rows = [...stage.querySelectorAll("li")];
  let index = 0;
  scene(stage, async (signal) => {
    rows.forEach((row) => row.classList.remove("is-on"));
    rows[index++ % rows.length].classList.add("is-on");
    await wait(1800, signal);
  }, () => rows[0].classList.add("is-on"));
})();

/* ---------- Setup: install the runtime, download a model, Ready ---------- */

(() => {
  const stage = document.getElementById("setup-stage");
  if (!stage) return;
  const window_ = stage.querySelector(".aw");
  const status = stage.querySelector(".aw-status-text");
  const jobs = [...stage.querySelectorAll(".aw-row")].filter((row) => row.querySelector(".aw-bar"));

  function show(row, phase, progress) {
    const line = row.querySelector(".aw-status-line");
    const button = row.querySelector(".aw-btn");
    row.classList.toggle("is-busy", phase === "busy");
    row.classList.toggle("is-done", phase === "done");
    line.textContent = line.dataset[phase];
    if (button.dataset[phase]) button.textContent = button.dataset[phase];
    row.querySelector(".aw-bar i").style.width = `${Math.round(progress * 100)}%`;
  }

  function reset() {
    jobs.forEach((row) => show(row, "idle", 0));
    window_.classList.remove("is-ready");
    status.textContent = status.dataset.needs;
  }

  scene(stage, async (signal) => {
    reset();
    await wait(1200, signal);
    for (const row of jobs) {
      for (let step = 0; step <= 20; step++) {
        show(row, "busy", step / 20);
        await wait(110, signal);
      }
      show(row, "done", 1);
      await wait(600, signal);
    }
    window_.classList.add("is-ready");
    status.textContent = status.dataset.ready;
    await wait(3600, signal);
  }, () => {
    jobs.forEach((row) => show(row, "done", 1));
    window_.classList.add("is-ready");
    status.textContent = status.dataset.ready;
  });
})();
