/* Burhan Nexus – radiology report entry: load a report template into the boxes. No inline scripts (CSP). */
(function () {
  "use strict";
  const form = document.querySelector("[data-rad-report]");
  if (!form) return;
  const select = form.querySelector("[data-template-select]");
  const apply = form.querySelector("[data-template-apply]");
  if (!select || !apply) return;
  apply.addEventListener("click", () => {
    const opt = select.options[select.selectedIndex];
    if (!opt || !opt.value) return;
    ["technique", "findings", "impression", "recommendations"].forEach(k => {
      const box = form.querySelector(`[data-field=${k}]`);
      if (box) box.value = opt.dataset[k] || "";
    });
    const name = form.querySelector("[data-template-name]");
    if (name) name.value = opt.textContent.trim();
    const findings = form.querySelector("[data-field=findings]");
    if (findings) findings.focus();
  });
})();
