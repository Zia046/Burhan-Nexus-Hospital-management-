/* Laboratory report and slip: print buttons, preview of the selected tests, auto print after the copy was recorded. */
(function () {
  "use strict";
  const body = document.body;
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
