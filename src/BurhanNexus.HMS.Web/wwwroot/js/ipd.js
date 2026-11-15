/* Burhan Nexus – IPD: bed list filters, delivery baby rows, template-free helpers. No inline scripts (CSP). */
(function () {
  "use strict";
  document.querySelectorAll("[data-bed-select]").forEach(box => {
    const list = box.querySelector("[data-bed-list]");
    const filters = box.querySelectorAll("[data-bed-filter]");
    const apply = () => {
      const ward = box.querySelector("[data-bed-filter=ward]").value, type = box.querySelector("[data-bed-filter=type]").value;
      list.querySelectorAll("option[data-ward]").forEach(o => { o.hidden = (ward && o.dataset.ward !== ward) || (type && o.dataset.type !== type); });
    };
    filters.forEach(f => f.addEventListener("change", apply));
  });
  // Delivery: show the NICU cot selector only for babies placed in the NICU.
  document.querySelectorAll("[data-baby]").forEach(row => {
    const placement = row.querySelector("[data-placement]");
    const nicu = row.querySelector("[data-nicu-fields]");
    if (!placement || !nicu) return;
    const refresh = () => { nicu.style.display = placement.value === "Nicu" ? "" : "none"; };
    placement.addEventListener("change", refresh);
    refresh();
  });
})();
