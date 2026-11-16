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
  document.querySelectorAll("[data-receive-lines]").forEach(table => {
    const tbody = table.querySelector("tbody");
    const prices = table.dataset.prices === "1", salePrice = table.dataset.saleprice === "1";
    const box = table.closest("form").querySelector("[data-med-search-box]");
    const totalEl = table.querySelector("[data-receive-total]");
    const form = table.closest("form");
    const summary = form.querySelector("[data-receive-summary]");
    const reindex = () => {
      let total = 0, qtyTotal = 0;
      [...tbody.rows].forEach((tr, i) => {
        tr.querySelectorAll("[data-name]").forEach(inp => { inp.name = `Lines[${i}].${inp.dataset.name}`; });
        qtyTotal += parseInt(tr.querySelector("[data-name=Quantity]").value || "0", 10) || 0;
        if (prices) {
          const q = parseInt(tr.querySelector("[data-name=Quantity]").value || "0", 10) || 0;
          const c = parseFloat(tr.querySelector("[data-name=UnitCost]").value || "0") || 0;
          const d = parseFloat(tr.querySelector("[data-name=Discount]").value || "0") || 0;
          const line = Math.max(0, q * c - d);
          tr.querySelector("[data-line-total]").textContent = money(line);
          total += line;
        }
      });
      if (totalEl) totalEl.textContent = money(total);
      // Update 11: item count, total quantity and purchase total of the invoice.
      if (summary) {
        summary.querySelector("[data-receive-count]").textContent = String(tbody.rows.length);
        summary.querySelector("[data-receive-qty]").textContent = String(qtyTotal);
        const pt = summary.querySelector("[data-receive-purchase]"); if (pt) pt.textContent = money(total);
      }
    };
    const addLine = (m, init) => {
      // Update 11: purchase and sale price are filled from the medicine (editable); a new line starts with them.
      const auto = init ? {} : { cost: m.cost, sale: m.price };
      const v = k => (init && init[k] !== null && init[k] !== undefined ? init[k] : (auto[k] !== null && auto[k] !== undefined ? auto[k] : ""));
      const cells = [
        el("td", {}, [el("strong", { text: m.name || init.name }), el("input", { type: "hidden", "data-name": "MedicineId", value: m.id }), el("input", { type: "hidden", "data-name": "MedicineName", value: m.name || init.name })]),
        el("td", {}, [el("input", { "data-name": "BatchNumber", maxlength: "50", className: "w-sm", value: v("batch"), placeholder: "optional" })]),
        el("td", {}, [el("input", { type: "month", "data-name": "Expiry", value: v("expiry"), required: true })]),
        el("td", {}, [el("input", { type: "month", "data-name": "Manufactured", value: v("mfg") })]),
        el("td", { className: "num" }, [el("input", { type: "number", min: "1", "data-name": "Quantity", className: "w-xs", value: v("qty"), required: true })])
      ];
      if (prices) {
        cells.push(el("td", { className: "num" }, [el("input", { type: "number", min: "0", step: "0.0001", "data-name": "UnitCost", className: "w-xs", value: v("cost") })]));
        if (salePrice) cells.push(el("td", { className: "num" }, [el("input", { type: "number", min: "0", step: "0.01", "data-name": "SalePrice", className: "w-xs", value: v("sale"), placeholder: m.price ?? "" })]));
        cells.push(el("td", { className: "num" }, [el("input", { type: "number", min: "0", step: "0.01", "data-name": "Discount", className: "w-xs", value: v("discount") })]));
        cells.push(el("td", { className: "num", "data-line-total": "" }));
      }
      const remove = el("button", { type: "button", className: "btn btn-light btn-sm", title: "Remove", text: "✕" });
      remove.addEventListener("click", () => { tr.remove(); reindex(); });
      cells.push(el("td", {}, [remove]));
      const tr = el("tr", { "data-receive-line": m.code || m.id }, cells);
      tbody.appendChild(tr);
      tr.querySelectorAll("input").forEach(i => i.addEventListener("input", reindex));
      reindex();
      if (!init) tr.querySelector("[data-name=Quantity]").focus();
    };
    if (box) attachSearch(box, "", m => addLine(m));
    form.querySelectorAll("[data-add-item]").forEach(b => b.addEventListener("click", () => { const s = form.querySelector("[data-med-search]"); if (s) s.focus(); }));
    let initial = [];
    try { initial = JSON.parse(table.dataset.initial || "[]"); } catch { initial = []; }
    initial.forEach(i => addLine({ id: i.id, name: i.name, code: i.id }, i));
  });
})();
