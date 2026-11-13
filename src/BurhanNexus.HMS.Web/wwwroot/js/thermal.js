/* Thermal slip printing: browser dialog, LAN printer through the server, or the local Burhan Nexus Print Agent. */
(function () {
  "use strict";
  const body = document.body;
  if (!body.hasAttribute("data-thermal")) return;
  const d = body.dataset;
  const status = document.querySelector("[data-thermal-status]");
  const buttons = document.querySelectorAll("[data-thermal-print],[data-thermal-browser]");
  const token = (document.querySelector("input[name=__RequestVerificationToken]") || {}).value || "";

  function setStatus(text, kind) {
    if (!status) return;
    status.textContent = text;
    status.className = "t-status" + (kind ? " " + kind : "");
  }
  function busy(on) { buttons.forEach(b => { b.disabled = on; }); }

  async function post(url, extra) {
    const form = new URLSearchParams(extra || {});
    const res = await fetch(url, {
      method: "POST", credentials: "same-origin",
      headers: { "X-CSRF-TOKEN": token, "Content-Type": "application/x-www-form-urlencoded" },
      body: form.toString()
    });
    let json = {};
    try { json = await res.json(); } catch (e) { /* not json */ }
    if (!res.ok) throw new Error(json.error || ("Request failed (" + res.status + ")"));
    return json;
  }

  // Page size = paper width x slip height, so the roll is not fed with blank paper.
  function fitPage() {
    const slip = document.querySelector(".t-slip");
    if (!slip || body.hasAttribute("data-no-fit")) return;
    const mm = Math.ceil(slip.getBoundingClientRect().height * 25.4 / 96) + 4;
    let st = document.getElementById("thermal-page");
    if (!st) { st = document.createElement("style"); st.id = "thermal-page"; document.head.appendChild(st); }
    st.textContent = "@page { size: " + d.width + "mm " + mm + "mm; margin: 0; }";
  }
  fitPage();

  async function browserPrint() {
    const r = await post(d.recordUrl, { width: d.width, method: "browser" });
    setStatus("Copy " + r.copy + " sent to the print dialog. Choose your thermal printer and paper " + d.width + " mm.", "ok");
    fitPage();
    window.print();
  }

  async function agentPrint() {
    const job = await post(d.escposUrl);
    const base = (d.agent || "http://127.0.0.1:9123").replace(/\/+$/, "");
    const useTcp = !job.printer && job.host;
    let res;
    try {
      res = await fetch(base + (useTcp ? "/print/tcp" : "/print/raw"), {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify(useTcp ? { host: job.host, port: job.port, data: job.data, jobName: job.job }
                                    : { printerName: job.printer, data: job.data, jobName: job.job })
      });
    } catch (e) {
      throw new Error("The Print Agent is not running on this computer (" + base + "). Start it, or use “Print with browser dialog”.");
    }
    let json = {};
    try { json = await res.json(); } catch (e) { /* ignore */ }
    if (!res.ok || !json.ok) throw new Error("Print Agent: " + (json.error || ("error " + res.status)));
    const r = await post(d.recordUrl, { width: d.width, method: "agent" });
    setStatus("Copy " + r.copy + " printed on " + (job.printer || job.host) + " (token unchanged).", "ok");
  }

  async function lanPrint() {
    const r = await post(d.lanUrl);
    setStatus("Copy " + r.copy + " sent to the LAN printer.", "ok");
  }

  async function run(forceBrowser) {
    if (d.canPrint !== "1") return;
    busy(true);
    setStatus("Printing…");
    try {
      if (forceBrowser || d.mode === "Browser") await browserPrint();
      else if (d.mode === "LanDirect") await lanPrint();
      else await agentPrint();
    } catch (e) {
      setStatus(e.message, "err");
    } finally {
      busy(false);
    }
  }

  document.querySelectorAll("[data-thermal-print]").forEach(b => b.addEventListener("click", () => run(false)));
  document.querySelectorAll("[data-thermal-browser]").forEach(b => b.addEventListener("click", () => run(true)));
  if (d.autoprint === "1") window.addEventListener("load", () => setTimeout(() => run(false), 300));
})();
