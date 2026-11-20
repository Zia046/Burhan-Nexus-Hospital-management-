/* Laboratory report and slip: print buttons, preview of the selected tests, auto print after the copy was recorded. */
(function () {
  "use strict";
  const body = document.body;
  // Thermal receipts / slips (lab receipt, lab slip, pharmacy slip): the page is exactly paper width x slip height with no margin,
  // so the roll is not fed with an A4 length of blank paper and the browser has no room to print its date/URL header and footer.
  function fitThermalPage() {
    if (!body.classList.contains("thermal-body") || body.hasAttribute("data-thermal")) return;
    const slips = Array.from(document.querySelectorAll(".t-slip"));
    if (slips.length === 0) return;
    const width = body.classList.contains("w58") ? 58 : 80;
    const mm = Math.ceil(Math.max.apply(null, slips.map(s => s.getBoundingClientRect().height)) * 25.4 / 96) + 4;
    let st = document.getElementById("thermal-page");
    if (!st) { st = document.createElement("style"); st.id = "thermal-page"; document.head.appendChild(st); }
    st.textContent = "@page { size: " + width + "mm " + mm + "mm; margin: 0; }";
  }
  fitThermalPage();
  window.addEventListener("beforeprint", fitThermalPage);
  document.querySelectorAll("[data-print-now]").forEach(b => b.addEventListener("click", () => window.print()));
  if (body.dataset.autoprint === "1") window.addEventListener("load", () => setTimeout(() => window.print(), 300));
  const form = document.querySelector("[data-report-form]");
  if (!form) return;
  const preview = form.querySelector("[data-report-preview]");
  if (preview) preview.addEventListener("click", () => {
    const ids = Array.from(form.querySelectorAll("[data-report-item]:checked")).map(c => c.value);
    const url = new URL(window.location.href);
    url.searchParams.set("items", ids.join(","));
    url.searchParams.delete("print");
    window.location.href = url.toString();
  });
})();
