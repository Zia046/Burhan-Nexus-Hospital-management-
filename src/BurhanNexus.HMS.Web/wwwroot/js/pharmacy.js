/* Burhan Nexus – pharmacy screens: medicine search, FEFO batch choice, line totals, receiving lines. No inline scripts (CSP). */
(function () {
  "use strict";
  const money = v => (Math.round((v || 0) * 100) / 100).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const el = (tag, attrs, children) => {
    const e = document.createElement(tag);
    Object.entries(attrs || {}).forEach(([k, v]) => {
      if (v === null || v === undefined || v === false) return;
      if (k === "text") e.textContent = v; else if (k === "className") e.className = v; else e.setAttribute(k, v === true ? "" : v);
    });
    (children || []).forEach(c => e.appendChild(typeof c === "string" ? document.createTextNode(c) : c));
    return e;
  };
  const fmtDate = iso => { const [y, m, d] = iso.split("-"); return `${d}/${m}/${y}`; };

  async function pick(params) {
    const r = await fetch("/Pharmacy/Pick?" + new URLSearchParams(params), { headers: { "Accept": "application/json" }, credentials: "same-origin" });
    return r.ok ? r.json() : [];
  }

  // Search box shared by both editors: type → list of medicines → click / Enter adds a line.
  function attachSearch(box, location, onPick) {
    const input = box.querySelector("[data-med-search]");
    const list = box.querySelector("[data-med-results]");
    let timer = null, items = [], active = -1;
    const render = () => {
      list.innerHTML = "";
      items.forEach((m, i) => {
        const row = el("button", { type: "button", className: "med-result" + (i === active ? " on" : ""), "data-med-result": m.code }, [
          el("strong", { text: m.name }), " ",
          el("small", { className: "muted", text: [m.generic, m.code].filter(Boolean).join(" · ") }),
          el("span", { className: "med-avail " + (m.available > 0 ? "" : "abn-text"), text: m.available + " " + m.unit + (m.available === 1 ? "" : "s") })
        ]);
        row.addEventListener("click", () => choose(i));
        list.appendChild(row);
      });
      list.hidden = items.length === 0;
    };
    const choose = i => { const m = items[i]; if (!m) return; onPick(m); input.value = ""; items = []; render(); input.focus(); };
    input.addEventListener("input", () => {
      clearTimeout(timer);
      const q = input.value.trim();
      if (q.length < 2) { items = []; render(); return; }
      timer = setTimeout(async () => { items = await pick({ q, location }); active = items.length ? 0 : -1; render(); }, 200);
    });
    input.addEventListener("keydown", e => {
      if (e.key === "ArrowDown") { active = Math.min(items.length - 1, active + 1); render(); e.preventDefault(); }
      else if (e.key === "ArrowUp") { active = Math.max(0, active - 1); render(); e.preventDefault(); }
      else if (e.key === "Enter") { e.preventDefault(); if (active >= 0) choose(active); }
      else if (e.key === "Escape") { items = []; render(); }
    });
  }

  // ---------------- Issue / sale / transfer lines ----------------
  document.querySelectorAll("[data-med-lines]").forEach(table => {
    const tbody = table.querySelector("tbody");
    const mode = table.dataset.mode, location = table.dataset.location;
    const prices = table.dataset.prices === "1", discount = table.dataset.discount === "1";
    const box = table.closest("form").querySelector("[data-med-search-box]");
    const totalEl = table.querySelector("[data-lines-total]");

    const reindex = () => {
      [...tbody.rows].forEach((tr, i) => tr.querySelectorAll("[data-name]").forEach(inp => { inp.name = `Input.Lines[${i}].${inp.dataset.name}`; }));
      let total = 0;
      [...tbody.rows].forEach(tr => {
        if (!prices) return;
        const qty = parseInt(tr.querySelector("[data-name=Quantity]").value || "0", 10) || 0;
        const sel = tr.querySelector("[data-name=BatchId]");
        const opt = sel.options[sel.selectedIndex];
        const price = parseFloat(opt && opt.dataset.price ? opt.dataset.price : tr.dataset.price || "0") || 0;
        const disc = discount ? parseFloat(tr.querySelector("[data-name=Discount]").value || "0") || 0 : 0;
        const line = Math.max(0, price * qty - disc);
        tr.querySelector("[data-price-cell]").textContent = price ? money(price) : "—";
        tr.querySelector("[data-line-total]").textContent = money(line);
        total += line;
      });
      if (totalEl) totalEl.textContent = money(total);
    };

    const addLine = (m, init) => {
      if ([...tbody.rows].some(r => r.dataset.medicine === String(m.id) && !init)) {
        const q = tbody.querySelector(`tr[data-medicine="${m.id}"] [data-name=Quantity]`); if (q) q.focus(); return;
      }
      const batch = el("select", { "data-name": "BatchId", "data-batch": "" }, [el("option", { value: "", text: m.batches.length ? `Auto (earliest expiry first)` : "No stock" })]);
      m.batches.forEach((b, i) => batch.appendChild(el("option", {
        value: b.id, "data-price": b.price ?? "", "data-available": b.available,
        text: `${b.number} · exp ${fmtDate(b.expiry)} · ${b.available}${i === 0 ? " · FEFO" : ""}${b.near ? " · near expiry" : ""}`
      })));
      if (init && init.batch) batch.value = String(init.batch);
      const qty = el("input", { type: "number", min: "1", "data-name": "Quantity", className: "w-xs", value: init ? init.qty : "", required: true });
      const cells = [
        el("td", {}, [el("strong", { text: m.name }), el("input", { type: "hidden", "data-name": "MedicineId", value: m.id }), el("small", { className: "block muted", text: m.code })]),
        el("td", {}, [batch]),
        el("td", { className: "num", "data-available": "", text: m.available + " " + m.unit }),
        el("td", { className: "num" }, [qty])
      ];
      if (mode === "issue") cells.push(el("td", {}, [el("input", { "data-name": "Instructions", maxlength: "200", placeholder: "e.g. 1 tab 3 times a day", value: init ? init.instructions || "" : "" })]));
      if (prices) {
        cells.push(el("td", { className: "num", "data-price-cell": "" }));
        if (discount) cells.push(el("td", { className: "num" }, [m.discount
          ? el("input", { type: "number", min: "0", step: "0.01", "data-name": "Discount", className: "w-xs", value: init && init.discount ? init.discount : "", title: `max ${m.maxDiscount}%` })
          : el("input", { type: "hidden", "data-name": "Discount", value: "0" })]));
        cells.push(el("td", { className: "num", "data-line-total": "" }));
      }
      const remove = el("button", { type: "button", className: "btn btn-light btn-sm", title: "Remove", text: "✕" });
      remove.addEventListener("click", () => { tr.remove(); reindex(); });
      cells.push(el("td", {}, [remove]));
      const tr = el("tr", { "data-medicine": m.id, "data-line": m.code }, cells);
      tr.dataset.price = m.price ?? "";
      tbody.appendChild(tr);
      tr.querySelectorAll("input, select").forEach(i => i.addEventListener("input", reindex));
      reindex();
      if (!init) qty.focus();
    };

    if (box) attachSearch(box, location, m => addLine(m));
    let initial = [];
    try { initial = JSON.parse(table.dataset.initial || "[]"); } catch { initial = []; }
    if (initial.length) pick({ ids: initial.map(i => i.id).join(","), location }).then(list => {
      initial.forEach(i => { const m = list.find(x => x.id === i.id); if (m) addLine(m, i); });
    });
  });

  // ---------------- Receiving lines ----------------
  // ---------------- Supplier invoice (Update 12): one table, a medicine search inside every row ----------------
  document.querySelectorAll("[data-invoice-lines]").forEach(table => {
    const form = table.closest("form");
    const tbody = table.querySelector("tbody");
    const prices = table.dataset.prices === "1", salePrice = table.dataset.saleprice === "1";
    const summary = form.querySelector("[data-receive-summary]");
    const num = (inp, int) => { if (!inp) return 0; const v = int ? parseInt(inp.value || "0", 10) : parseFloat(inp.value || "0"); return isNaN(v) ? 0 : v; };

    const reindex = () => {
      let total = 0, qty = 0, items = 0;
      [...tbody.rows].forEach((tr, i) => {
        tr.querySelector("[data-row-no]").textContent = String(i + 1);
        tr.querySelectorAll("[data-name]").forEach(inp => { inp.name = `Lines[${i}].${inp.dataset.name}`; });
        const chosen = num(tr.querySelector("[data-name=MedicineId]"), true) > 0;
        const q = num(tr.querySelector("[data-name=Quantity]"), true);
        if (chosen) { items++; qty += q; }
        if (prices) {
          const line = Math.max(0, q * num(tr.querySelector("[data-name=UnitCost]")) - num(tr.querySelector("[data-name=Discount]")));
          tr.querySelector("[data-line-total]").textContent = chosen ? money(line) : "";
          if (chosen) total += line;
        }
      });
      summary.querySelector("[data-receive-count]").textContent = String(items);
      summary.querySelector("[data-receive-qty]").textContent = String(qty);
      if (prices) {
        summary.querySelector("[data-receive-purchase]").textContent = money(total);
        const net = total - num(form.querySelector("[data-invoice-discount]")) + num(form.querySelector("[data-invoice-charges]"));
        summary.querySelector("[data-receive-net]").textContent = money(net);
      }
    };

    // Moves the focus to the next box of the row; from the last box to the next row (a new row is opened at the end).
    const nextBox = (tr, from) => {
      const boxes = [...tr.querySelectorAll("input:not([type=hidden]):not([readonly])")];
      const i = boxes.indexOf(from);
      if (i >= 0 && i < boxes.length - 1) { boxes[i + 1].focus(); boxes[i + 1].select && boxes[i + 1].select(); return; }
      const next = tr.nextElementSibling || addRow();
      next.querySelector("[data-row-med]").focus();
    };

    const setMedicine = (tr, m) => {
      tr.querySelector("[data-name=MedicineId]").value = m.id;
      tr.querySelector("[data-name=MedicineName]").value = m.name;
      const box = tr.querySelector("[data-row-med]");
      box.value = m.name; box.classList.add("chosen"); box.classList.remove("input-error");
      tr.querySelector("[data-row-info]").textContent = [m.generic, m.code, m.available !== undefined ? `in stock ${m.available} ${m.unit || ""}` : null].filter(Boolean).join(" · ");
      tr.dataset.medicine = m.code || m.id;
      tr.dataset.currentCost = m.cost === null || m.cost === undefined ? "" : String(m.cost);
      if (prices) {
        const cost = tr.querySelector("[data-name=UnitCost]");
        if (cost && (cost.value === "" || cost.dataset.auto === "1")) { cost.value = m.cost ?? ""; cost.dataset.auto = "1"; }
        const sale = tr.querySelector("[data-sale]");
        if (sale) {
          if (sale.tagName === "INPUT") { if (sale.value === "" || sale.dataset.auto === "1") { sale.value = m.price ?? ""; sale.dataset.auto = "1"; } }
          else sale.textContent = m.price === null || m.price === undefined ? "—" : money(m.price);
        }
      }
      checkRate(tr);
      reindex();
    };

    // Update 13: a purchase rate different from the medicine's current rate needs its own batch number (rates are never mixed).
    const checkRate = tr => {
      const cost = tr.querySelector("[data-name=UnitCost]"), batch = tr.querySelector("[data-name=BatchNumber]");
      if (!cost || !batch) return;
      const cur = tr.dataset.currentCost, val = cost.value.trim();
      const differs = cur !== undefined && cur !== "" && val !== "" && Number(val) !== Number(cur);
      const need = differs && batch.value.trim() === "";
      batch.required = differs;
      batch.placeholder = differs ? "required – new rate" : "optional";
      batch.classList.toggle("input-error", need);
      batch.setCustomValidity(need ? `Purchase rate ${money(Number(val))} differs from the current rate ${money(Number(cur))}. Enter a separate batch number.` : "");
      batch.title = need ? batch.validationMessage : "";
      tr.toggleAttribute("data-rate-differs", differs);
    };

    const clearMedicine = tr => {
      tr.querySelector("[data-name=MedicineId]").value = "0";
      tr.querySelector("[data-name=MedicineName]").value = "";
      tr.querySelector("[data-row-med]").classList.remove("chosen");
      tr.querySelector("[data-row-info]").textContent = "";
      delete tr.dataset.medicine;
      reindex();
    };

    const attachRowSearch = tr => {
      const input = tr.querySelector("[data-row-med]");
      const list = tr.querySelector("[data-med-results]");
      let timer = null, items = [], active = -1, seq = 0;
      const render = () => {
        list.innerHTML = "";
        items.forEach((m, i) => {
          const row = el("button", { type: "button", className: "med-result" + (i === active ? " on" : ""), "data-med-result": m.code, tabindex: "-1" }, [
            el("strong", { text: m.name }), " ",
            el("small", { className: "muted", text: [m.generic, m.code].filter(Boolean).join(" · ") }),
            el("span", { className: "med-avail", text: (m.available ?? 0) + " in stock" + (prices && m.cost != null ? " · cost " + money(m.cost) : "") })
          ]);
          row.addEventListener("mousedown", e => { e.preventDefault(); choose(i); });
          list.appendChild(row);
        });
        list.hidden = items.length === 0;
      };
      const choose = i => {
        const m = items[i]; if (!m) return;
        items = []; render();
        setMedicine(tr, m);
        const q = tr.querySelector("[data-name=Quantity]"); q.focus(); q.select();
      };
      input.addEventListener("input", () => {
        if (tr.querySelector("[data-name=MedicineId]").value !== "0") clearMedicine(tr);
        clearTimeout(timer);
        const q = input.value.trim();
        if (q.length < 2) { items = []; render(); return; }
        const my = ++seq;
        timer = setTimeout(async () => { const r = await pick({ q, location: "" }); if (my !== seq) return; items = r; active = items.length ? 0 : -1; render(); }, 150);
      });
      input.addEventListener("keydown", e => {
        if (e.key === "ArrowDown" && items.length) { active = Math.min(items.length - 1, active + 1); render(); e.preventDefault(); }
        else if (e.key === "ArrowUp" && items.length) { active = Math.max(0, active - 1); render(); e.preventDefault(); }
        else if (e.key === "Enter") {
          e.preventDefault();
          if (items.length && active >= 0) choose(active);
          else if (tr.querySelector("[data-name=MedicineId]").value !== "0") { const q = tr.querySelector("[data-name=Quantity]"); q.focus(); q.select(); }
        }
        else if (e.key === "Escape") { items = []; render(); }
      });
      input.addEventListener("blur", () => setTimeout(() => { items = []; render(); }, 150));
    };

    const addRow = init => {
      const v = k => (init && init[k] !== null && init[k] !== undefined ? init[k] : "");
      const cells = [
        el("td", { className: "muted", "data-row-no": "" }),
        el("td", { className: "med-cell" }, [
          el("div", { className: "med-search" }, [
            el("input", { "data-row-med": "", autocomplete: "off", placeholder: "Search medicine…", value: v("name"), "aria-label": "Medicine" }),
            el("div", { className: "med-results", "data-med-results": "", hidden: true })
          ]),
          el("small", { className: "muted block", "data-row-info": "" }),
          el("input", { type: "hidden", "data-name": "MedicineId", value: v("id") || "0" }),
          el("input", { type: "hidden", "data-name": "MedicineName", value: v("name") })
        ]),
        el("td", { className: "num" }, [el("input", { type: "number", min: "1", step: "1", "data-name": "Quantity", className: "w-xs", value: v("qty"), "aria-label": "Quantity" })]),
        el("td", {}, [el("input", { type: "month", "data-name": "Expiry", value: v("expiry"), "aria-label": "Expiry" })]),
        el("td", {}, [el("input", { "data-name": "BatchNumber", maxlength: "50", className: "w-sm", value: v("batch"), placeholder: "optional", "aria-label": "Batch" })])
      ];
      if (prices) {
        cells.push(el("td", { className: "num" }, [el("input", { type: "number", min: "0", step: "0.0001", "data-name": "UnitCost", className: "w-xs", value: v("cost"), "aria-label": "Purchase price" })]));
        cells.push(el("td", { className: "num" }, [salePrice
          ? el("input", { type: "number", min: "0", step: "0.01", "data-name": "SalePrice", "data-sale": "", className: "w-xs", value: v("sale"), "aria-label": "Sale price" })
          : el("span", { "data-sale": "", className: "muted", title: "Changing the sale price needs “Manage Medicine Prices”", text: v("sale") === "" ? "" : money(v("sale")) })]));
        cells.push(el("td", { className: "num" }, [el("input", { type: "number", min: "0", step: "0.01", "data-name": "Discount", className: "w-xs", value: v("discount"), "aria-label": "Adjustment" })]));
        cells.push(el("td", { className: "num", "data-line-total": "" }));
      }
      const remove = el("button", { type: "button", className: "btn btn-light btn-sm", title: "Remove row", tabindex: "-1", text: "✕", "data-remove-row": "" });
      cells.push(el("td", {}, [remove]));
      const tr = el("tr", { "data-invoice-row": "" }, cells);
      tbody.appendChild(tr);
      remove.addEventListener("click", () => { tr.remove(); if (tbody.rows.length === 0) addRow(); reindex(); });
      tr.querySelectorAll("input[data-name]:not([type=hidden])").forEach(i => {
        i.addEventListener("input", () => { if (i.dataset.auto) i.dataset.auto = "0"; checkRate(tr); reindex(); });
        i.addEventListener("keydown", e => { if (e.key === "Enter") { e.preventDefault(); nextBox(tr, i); } });
      });
      attachRowSearch(tr);
      if (init && init.id > 0) { tr.querySelector("[data-row-med]").classList.add("chosen"); tr.dataset.medicine = init.id; }
      reindex();
      return tr;
    };

    form.querySelectorAll("[data-add-row]").forEach(b => b.addEventListener("click", () => addRow().querySelector("[data-row-med]").focus()));
    document.addEventListener("keydown", e => { if (e.altKey && (e.key === "n" || e.key === "N")) { e.preventDefault(); addRow().querySelector("[data-row-med]").focus(); } });
    form.querySelectorAll("[data-invoice-discount],[data-invoice-charges]").forEach(i => i.addEventListener("input", reindex));
    // Enter in the header never submits the invoice by accident.
    form.querySelectorAll("[data-invoice-header] input").forEach(i => i.addEventListener("keydown", e => { if (e.key === "Enter") e.preventDefault(); }));
    const sup = form.querySelector("[data-supplier-select]"), supInfo = form.querySelector("[data-supplier-details]");
    if (sup && supInfo) { const show = () => { supInfo.textContent = sup.selectedOptions[0]?.dataset.details || ""; }; sup.addEventListener("change", show); show(); }

    let initial = [];
    try { initial = JSON.parse(table.dataset.initial || "[]"); } catch { initial = []; }
    initial.forEach(i => addRow(i));
    while (tbody.rows.length < 3) addRow();

    // New medicine from inside the invoice – the invoice on screen stays as it is.
    const dlg = document.querySelector("[data-new-medicine]");
    const openBtn = form.querySelector("[data-new-medicine-open]");
    if (dlg && openBtn) {
      const dform = dlg.querySelector("[data-new-medicine-form]");
      const err = dlg.querySelector("[data-new-medicine-error]");
      const dup = dlg.querySelector("[data-new-medicine-dup]");
      openBtn.addEventListener("click", () => { dform.reset(); dup.hidden = true; err.textContent = ""; dlg.showModal(); dlg.querySelector("[name=Name]").focus(); });
      dlg.querySelector("[data-new-medicine-cancel]").addEventListener("click", () => dlg.close());
      dlg.querySelector("[data-new-medicine-save]").addEventListener("click", async () => {
        err.textContent = "";
        const token = form.querySelector("input[name=__RequestVerificationToken]").value;
        const body = new URLSearchParams();
        new FormData(dform).forEach((v, k) => body.append(k, String(v)));
        body.append("__RequestVerificationToken", token);
        const r = await fetch("/Pharmacy/Receive?handler=NewMedicine", {
          method: "POST", body, credentials: "same-origin",
          headers: { "Content-Type": "application/x-www-form-urlencoded", "X-CSRF-TOKEN": token, "Accept": "application/json" }
        });
        const d = r.ok ? await r.json().catch(() => null) : null;
        if (!d) { err.textContent = "Could not create the medicine (permission or connection)."; return; }
        if (d.duplicate) { dup.hidden = false; dlg.querySelector("[data-new-medicine-dup-text]").textContent = "Similar: " + d.duplicate.join("; "); return; }
        if (!d.ok) { err.textContent = (d.errors || []).join(" "); return; }
        dlg.close();
        const empty = [...tbody.rows].find(tr => tr.querySelector("[data-name=MedicineId]").value === "0" && !tr.querySelector("[data-row-med]").value) || addRow();
        setMedicine(empty, { id: d.id, name: d.name, code: d.code, cost: d.cost, price: d.price, available: 0, unit: "" });
        const q = empty.querySelector("[data-name=Quantity]"); q.focus();
      });
    }
  });
})();

// Update 17: medicine issue mode – show the selected mode before saving and only the fields of that mode.
(function () {
  "use strict";
  document.addEventListener("DOMContentLoaded", () => {
    const radios = [...document.querySelectorAll("[data-issue-mode]")];
    if (radios.length === 0) return;
    const label = document.querySelector("[data-mode-label]");
    const apply = () => {
      const sel = radios.find(r => r.checked) || radios[0];
      if (label) label.textContent = sel.dataset.label;
      document.querySelectorAll("[data-mode-only]").forEach(el => { el.hidden = el.dataset.modeOnly !== sel.value; });
    };
    radios.forEach(r => r.addEventListener("change", apply));
    apply();
  });
})();
