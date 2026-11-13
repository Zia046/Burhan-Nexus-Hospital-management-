(function () {
  "use strict";
  document.querySelectorAll("[data-print]").forEach(b => b.addEventListener("click", () => window.print()));
})();
