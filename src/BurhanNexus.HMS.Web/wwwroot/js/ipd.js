/* Burhan Nexus – IPD: bed list filters, delivery baby rows, template-free helpers. No inline scripts (CSP). */
(function () {
  "use strict";
  // Update 11: Ward → rooms of that ward → vacant beds of that room.
  document.querySelectorAll("[data-bed-select]").forEach(box => {
    const ward = box.querySelector("[data-bed-ward]"), room = box.querySelector("[data-bed-room]"), list = box.querySelector("[data-bed-list]");
    if (!ward || !room || !list) return;
    const showRooms = () => {
      room.querySelectorAll("option[data-ward]").forEach(o => { o.hidden = o.disabled = !ward.value || o.dataset.ward !== ward.value; });
      if (room.selectedOptions[0] && room.selectedOptions[0].disabled) room.value = "";
      room.disabled = !ward.value;
    };
    const showBeds = () => {
      list.querySelectorAll("option[data-room]").forEach(o => { o.hidden = o.disabled = !room.value || o.dataset.room !== room.value; });
      if (list.selectedOptions[0] && list.selectedOptions[0].disabled) list.value = "";
      list.disabled = !room.value && !list.value;
    };
    ward.addEventListener("change", () => { showRooms(); showBeds(); });
    room.addEventListener("change", showBeds);
    showRooms(); showBeds();
  });
  // Update 11: the date picker closes as soon as a date is picked.
  document.querySelectorAll("input[data-close-on-pick]").forEach(i => i.addEventListener("change", () => i.blur()));
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
