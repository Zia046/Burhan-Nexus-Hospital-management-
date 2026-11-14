// Burhan Nexus HMS – UI behaviour (no external libraries; CSP: script-src 'self').
(function () {
  "use strict";
  const $ = (s, r) => (r || document).querySelector(s);
  const $$ = (s, r) => Array.from((r || document).querySelectorAll(s));

  // ---- Sidebar (tablets) ----
  $$("[data-sidebar-toggle]").forEach(b => b.addEventListener("click", () => document.body.classList.toggle("sidebar-open")));
  $$("[data-sidebar-close]").forEach(b => b.addEventListener("click", () => document.body.classList.remove("sidebar-open")));

  // ---- Dropdown ----
  $$("[data-dropdown]").forEach(d => {
    const t = $("[data-dropdown-toggle]", d);
    t && t.addEventListener("click", e => { e.stopPropagation(); d.classList.toggle("open"); });
  });
  document.addEventListener("click", () => $$("[data-dropdown].open").forEach(d => d.classList.remove("open")));

  // ---- Show / hide password ----
  $$("[data-toggle-password]").forEach(btn => btn.addEventListener("click", () => {
    const input = btn.parentElement.querySelector("input");
    if (!input) return;
    const show = input.type === "password";
    input.type = show ? "text" : "password";
    const use = btn.querySelector("use");
    if (use) use.setAttribute("href", show ? "#i-eye-off" : "#i-eye");
    btn.setAttribute("aria-label", show ? "Hide password" : "Show password");
  }));

  // ---- Password strength ----
  $$("[data-strength]").forEach(input => {
    const bar = input.closest(".field") && $("[data-strength-bar] span", input.closest(".field"));
    if (!bar) return;
    input.addEventListener("input", () => {
      const v = input.value; let s = 0;
      if (v.length >= 8) s++; if (v.length >= 12) s++;
      if (/[a-z]/.test(v) && /[A-Z]/.test(v)) s++; if (/\d/.test(v)) s++; if (/[^A-Za-z0-9]/.test(v)) s++;
      const pct = Math.min(100, s * 20);
      bar.style.width = pct + "%";
      bar.style.background = pct < 50 ? "#c9302c" : pct < 80 ? "#b7791f" : "#15924a";
    });
  });

  // ---- Confirmations & busy overlay ----
  $$("form[data-confirm]").forEach(f => f.addEventListener("submit", e => {
    if (!window.confirm(f.getAttribute("data-confirm"))) e.preventDefault();
  }));
  $$("button[data-confirm]").forEach(b => b.addEventListener("click", e => {
    if (!window.confirm(b.getAttribute("data-confirm"))) e.preventDefault();
  }));
  $$("form[data-busy]").forEach(f => f.addEventListener("submit", e => {
    if (e.defaultPrevented) return;
    const o = document.createElement("div");
    o.className = "busy-overlay";
    o.innerHTML = '<div><div class="spinner"></div><span></span></div>';
    o.querySelector("span").textContent = f.getAttribute("data-busy");
    document.body.appendChild(o);
  }));
  $$("form").forEach(f => f.addEventListener("submit", e => {
    setTimeout(() => { if (!e.defaultPrevented) $$("button[type=submit]", f).forEach(b => b.disabled = true); }, 0);
  }));

  // ---- Flash messages ----
  $$("[data-autohide]").forEach(a => setTimeout(() => { a.style.transition = "opacity .4s"; a.style.opacity = "0"; setTimeout(() => a.remove(), 450); }, 6000));

  // ---- Live clock in hospital time zone ----
  const clock = $("[data-clock]");
  if (clock) {
    const tz = clock.getAttribute("data-tz") || "Asia/Karachi";
    const text = $("[data-clock-text]", clock);
    let fmt;
    try { fmt = new Intl.DateTimeFormat("en-GB", { timeZone: tz, weekday: "short", day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", second: "2-digit" }); } catch (e) { fmt = null; }
    const tick = () => { if (fmt && text) text.textContent = fmt.format(new Date()); };
    tick(); setInterval(tick, 1000);
  }

  // ---- Session timeout warning ----
  const timeoutMin = parseInt(document.body.getAttribute("data-session-timeout") || "0", 10);
  const modal = $("#session-warning");
  if (timeoutMin > 0 && modal) {
    let deadline, warnTimer, endTimer, countdownTimer;
    const schedule = () => {
      clearTimeout(warnTimer); clearTimeout(endTimer); clearInterval(countdownTimer);
      modal.hidden = true;
      deadline = Date.now() + timeoutMin * 60000;
      warnTimer = setTimeout(showWarning, Math.max(0, timeoutMin * 60000 - 60000));
      endTimer = setTimeout(() => { window.location.href = "/Account/Login?reason=timeout"; }, timeoutMin * 60000 + 3000);
    };
    const showWarning = () => {
      modal.hidden = false;
      const c = $("[data-session-countdown]", modal);
      countdownTimer = setInterval(() => { if (c) c.textContent = Math.max(0, Math.round((deadline - Date.now()) / 1000)); }, 500);
    };
    const btn = $("[data-session-continue]", modal);
    btn && btn.addEventListener("click", () => {
      fetch("/api/me", { credentials: "same-origin" }).then(r => { if (r.ok) schedule(); else window.location.href = "/Account/Login?reason=timeout"; });
    });
    schedule();
  }

  // ---- Dashboard live figures (background requests do not extend the session) ----
  const live = $("[data-dashboard-live]");
  if (live) {
    const get = (o, path) => path.split(".").reduce((a, k) => (a == null ? a : a[k]), o);
    const refresh = () => fetch("/api/dashboard/summary", { credentials: "same-origin", headers: { "X-BNHMS-Background": "1" } })
      .then(r => { if (!r.ok) throw new Error(); return r.json(); })
      .then(data => {
        $$("[data-live]").forEach(el => { const v = get(data, el.getAttribute("data-live")); if (v !== undefined && v !== null) el.textContent = v; });
        $$("[data-live-dot]").forEach(el => { const v = get(data, el.getAttribute("data-live-dot")); el.className = "dot " + (v ? "dot-ok" : "dot-bad"); });
        const sd = $("[data-server-dot]"), st = $("[data-server-text]");
        if (sd) sd.className = "dot dot-ok"; if (st) st.textContent = "Online";
      })
      .catch(() => { const sd = $("[data-server-dot]"), st = $("[data-server-text]"); if (sd) sd.className = "dot dot-bad"; if (st) st.textContent = "Not reachable"; });
    setInterval(refresh, 30000);
  }

  // ---- Permission matrix ----
  const paint = s => { s.classList.toggle("v-grant", s.value === "grant"); s.classList.toggle("v-deny", s.value === "deny"); };
  $$("[data-perm-select]").forEach(s => { paint(s); s.addEventListener("change", () => paint(s)); });
  $$("[data-perm-all]").forEach(b => b.addEventListener("click", () => {
    const v = b.getAttribute("data-perm-all");
    $$("[data-perm-select]:not(:disabled)", b.closest("[data-perm-module]")).forEach(s => { s.value = v; paint(s); });
  }));
  $$("[data-check-all]").forEach(b => b.addEventListener("click", () => {
    const v = b.getAttribute("data-check-all") === "true";
    $$("input[type=checkbox]:not(:disabled)", b.closest("[data-perm-module]")).forEach(c => c.checked = v);
  }));

  // ---- Logo preview ----
  $$("[data-logo-input]").forEach(input => input.addEventListener("change", () => {
    const f = input.files && input.files[0];
    const scope = input.closest(".card, .field, form") || document;
    const img = $("[data-logo-preview]", scope) || $("[data-logo-preview]");
    if (!f || !img) return;
    if (f.size > 2 * 1024 * 1024) { window.alert("The logo must be smaller than 2 MB."); input.value = ""; return; }
    img.src = URL.createObjectURL(f);
  }));


  // ---- Phase 2: patient form helpers ----
  const pad = n => String(n).padStart(2, "0");
  $$("[data-patient-fields]").forEach(box => {
    const dob = $("[data-dob]", box), age = $("[data-age]", box), unit = $("[data-age-unit]", box), hint = $("[data-age-hint]", box);
    const today = new Date();
    if (dob) dob.max = `${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`;
    const calc = () => {
      if (!dob || !dob.value) { if (hint) hint.textContent = "Enter the age if the date of birth is unknown."; return; }
      const d = new Date(dob.value + "T00:00:00");
      if (isNaN(d) || d > today) return;
      let y = today.getFullYear() - d.getFullYear();
      const m0 = today.getMonth() - d.getMonth(), dd = today.getDate() - d.getDate();
      if (m0 < 0 || (m0 === 0 && dd < 0)) y--;
      let months = (today.getFullYear() - d.getFullYear()) * 12 + m0 - (dd < 0 ? 1 : 0);
      const days = Math.floor((today - d) / 86400000);
      if (y >= 1) { age.value = y; unit.value = "Years"; }
      else if (months >= 1) { age.value = months; unit.value = "Months"; }
      else { age.value = days; unit.value = "Days"; }
      if (hint) hint.textContent = "Calculated from the date of birth.";
    };
    dob && dob.addEventListener("change", calc);
    age && age.addEventListener("input", () => { if (dob && dob.value) dob.value = ""; if (hint) hint.textContent = "Age entered manually (date of birth unknown)."; });
    calc();

    const cnic = $("[data-cnic]", box);
    cnic && cnic.addEventListener("input", () => {
      const d = cnic.value.replace(/\D/g, "").slice(0, 13);
      cnic.value = d.length > 12 ? `${d.slice(0, 5)}-${d.slice(5, 12)}-${d.slice(12)}` : d.length > 5 ? `${d.slice(0, 5)}-${d.slice(5)}` : d;
    });

    const unid = $("[data-unidentified]", box);
    const toggleUnid = () => $$("[data-hide-unidentified]", box).forEach(el => el.classList.toggle("hidden-unidentified", !!(unid && unid.checked)));
    unid && unid.addEventListener("change", toggleUnid);
    toggleUnid();
  });

  // ---- Live duplicate check while typing (server decides what is a possible duplicate) ----
  $$("[data-patient-form]").forEach(form => {
    const panel = $("[data-dup-panel]", form);
    if (!panel || panel.children.length) return; // server-side warning already shown
    if (panel.dataset.canView === undefined) return;
    const val = n => { const el = form.querySelector(`[name$='.${n}']`); return el ? el.value.trim() : ""; };
    let timer = null, lastKey = "";
    const run = () => {
      const q = { name: val("FullName"), guardian: val("GuardianName"), cnic: val("Cnic"), mobile: val("MobileNumber") };
      const enough = q.name.length >= 3 || q.cnic.replace(/\D/g, "").length === 13 || q.mobile.replace(/\D/g, "").length >= 10;
      const key = JSON.stringify(q);
      if (!enough || key === lastKey) return;
      lastKey = key;
      fetch("/api/patients/duplicates?" + new URLSearchParams(q), { credentials: "same-origin" })
        .then(r => r.ok ? r.json() : [])
        .then(list => {
          panel.innerHTML = "";
          if (!list.length) return;
          const box = document.createElement("div");
          box.className = "live-dups";
          const title = document.createElement("strong");
          title.textContent = "Possible existing patient found. Please verify before creating a new registration.";
          box.appendChild(title);
          const ul = document.createElement("ul");
          list.forEach(p => {
            const li = document.createElement("li");
            const id = document.createElement("span"); id.className = "mono"; id.textContent = p.patientNumber;
            const txt = document.createElement("span");
            txt.textContent = `${p.fullName}${p.guardianName ? " · " + p.guardianName : ""} · ${p.ageText || ""} ${p.gender} · ${p.mobileNumber || ""} ${p.cnic || ""}`;
            const why = document.createElement("span"); why.className = "tag"; why.textContent = (p.reasons || []).join(", ");
            const open = document.createElement("a"); open.href = "/Patients/Profile/" + p.id; open.target = "_blank"; open.textContent = "Open";
            const opd = document.createElement("a"); opd.href = "/Opd/Register?patientId=" + p.id; opd.textContent = "Use – OPD";
            const er = document.createElement("a"); er.href = "/Emergency/Register?patientId=" + p.id; er.textContent = "Use – Emergency";
            li.append(id, txt, why);
            if (panel.dataset.canView === "1") li.append(open);
            if (panel.dataset.canOpd === "1") li.append(opd);
            if (panel.dataset.canEr === "1") li.append(er);
            ul.appendChild(li);
          });
          box.appendChild(ul);
          panel.appendChild(box);
        }).catch(() => {});
    };
    $$("[data-dup-watch]", form).forEach(el => el.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(run, 600); }));
  });

  // ---- Setup wizard ----
  const wizard = $("[data-wizard]");
  if (wizard) {
    const steps = $$("[data-step]", wizard);
    const links = $$("[data-step-link]");
    const prev = $("[data-wizard-prev]", wizard), next = $("[data-wizard-next]", wizard), finish = $("[data-wizard-finish]", wizard);
    let i = 0;
    const show = n => {
      i = n;
      steps.forEach((s, k) => s.hidden = k !== i);
      const id = steps[i].getAttribute("data-step");
      let passed = true;
      links.forEach(l => {
        const lid = l.getAttribute("data-step-link");
        l.classList.toggle("current", lid === id);
        if (lid === id) passed = false;
        l.classList.toggle("done", passed);
      });
      prev.hidden = i === 0;
      next.hidden = i === steps.length - 1;
      finish.hidden = i !== steps.length - 1;
      if (i === steps.length - 1) buildReview();
      window.scrollTo({ top: 0, behavior: "smooth" });
    };
    const validStep = step => {
      let ok = true, first = null;
      $$("input, select, textarea", step).forEach(el => {
        el.classList.remove("invalid");
        let valid = el.checkValidity();
        const match = el.getAttribute("data-match");
        if (valid && match) valid = el.value === ($("[name='" + match + "']", wizard) || {}).value;
        if (el.type === "radio") valid = !!$("input[name='" + el.name + "']:checked", step);
        if (!valid) { ok = false; el.classList.add("invalid"); first = first || el; }
      });
      if (first) first.focus();
      return ok;
    };
    const buildReview = () => {
      const list = $("[data-review-list]", wizard);
      if (!list) return;
      list.innerHTML = "";
      const seen = new Set();
      $$("[data-review]", wizard).forEach(el => {
        const label = el.getAttribute("data-review");
        if (el.type === "radio") { if (!el.checked) return; }
        if (seen.has(label)) return;
        let value = el.tagName === "SELECT" ? (el.options[el.selectedIndex] || {}).text : el.type === "radio" ? el.getAttribute("data-review-text") : el.value;
        if (!value) return;
        seen.add(label);
        const dt = document.createElement("dt"); dt.textContent = label;
        const dd = document.createElement("dd"); dd.textContent = value;
        list.append(dt, dd);
      });
    };
    next.addEventListener("click", () => { if (validStep(steps[i])) show(i + 1); });
    prev.addEventListener("click", () => show(i - 1));
    links.forEach(l => l.addEventListener("click", () => {
      const idx = steps.findIndex(s => s.getAttribute("data-step") === l.getAttribute("data-step-link"));
      if (idx >= 0 && idx < i) show(idx);
    }));
    wizard.addEventListener("submit", e => {
      for (let k = 0; k < steps.length; k++) { if (!validStep(steps[k])) { e.preventDefault(); show(k); validStep(steps[k]); return; } }
    });
    show(0);
  }
})();

// Print buttons inside the application layout (reports) and report period selector.
(function () {
  "use strict";
  document.querySelectorAll("[data-print]").forEach(b => b.addEventListener("click", () => window.print()));
  document.querySelectorAll("[data-period-select]").forEach(s => s.addEventListener("change", () => {
    const f = s.form;
    f.querySelectorAll("input[name=date],input[name=month],input[name=year]").forEach(i => i.disabled = true);
    f.submit();
  }));
})();

// ---------------- Phase 3: finance screens ----------------
(function () {
  "use strict";
  const money = v => (Math.round(v * 100) / 100).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  // Period presets: choosing a preset ignores the date boxes; editing a date switches to "Custom range".
  document.querySelectorAll("[data-period-custom]").forEach(sel => {
    const form = sel.form;
    sel.addEventListener("change", () => {
      if (sel.value !== "custom") form.querySelectorAll("input[name=from],input[name=to]").forEach(i => i.disabled = true);
      form.submit();
    });
    form.querySelectorAll("input[name=from],input[name=to]").forEach(i => i.addEventListener("change", () => { sel.value = "custom"; }));
  });

  // Submit buttons that must only be pressed once (the server is idempotent as well).
  document.querySelectorAll("form").forEach(f => f.addEventListener("submit", () => {
    f.querySelectorAll("button[data-once]").forEach(b => setTimeout(() => { b.disabled = true; b.textContent = "Saving…"; }, 0));
  }));

  const form = document.querySelector("[data-collect-form]");
  if (!form) return;
  const rows = Array.from(form.querySelectorAll("[data-line]"));
  const total = form.querySelector("[data-total]");
  const method = form.querySelector("[data-method]");
  const refField = form.querySelector("[data-ref-field]");

  function recalc() {
    let sum = 0;
    rows.forEach(r => {
      const opt = r.querySelector("[data-service]").selectedOptions[0];
      const fee = opt && opt.value !== "0" ? parseFloat(opt.dataset.fee || "0") : 0;
      const discEl = r.querySelector("[data-discount]");
      const disc = discEl ? parseFloat(discEl.value || "0") || 0 : 0;
      const net = Math.max(0, fee - disc);
      r.querySelector("[data-fee-cell]").textContent = opt && opt.value !== "0" ? (fee === 0 ? "Free" : money(fee)) : "—";
      const pay = r.querySelector("[data-paynow]");
      pay.placeholder = opt && opt.value !== "0" ? money(net) : "full";
      pay.readOnly = !(opt && opt.dataset.partial === "1");
      if (pay.readOnly) pay.value = "";
      if (opt && opt.value !== "0") sum += pay.value === "" ? net : (parseFloat(pay.value) || 0);
    });
    form.querySelectorAll("[data-balance-pay]").forEach(i => { sum += parseFloat(i.value || "0") || 0; });
    if (total) total.textContent = money(sum);
    if (refField && method) refField.style.display = method.selectedOptions[0] && method.selectedOptions[0].dataset.ref === "1" ? "" : "none";
  }

  rows.forEach(r => {
    const svc = r.querySelector("[data-service]");
    const doc = r.querySelector("[data-doctor]");
    svc.addEventListener("change", () => {
      const opt = svc.selectedOptions[0];
      if (opt && opt.dataset.doctor) doc.value = opt.dataset.doctor;
      recalc();
    });
    r.querySelectorAll("input").forEach(i => i.addEventListener("input", recalc));
  });
  form.querySelectorAll("[data-balance-pay]").forEach(i => i.addEventListener("input", recalc));
  if (method) method.addEventListener("change", recalc);
  const add = form.querySelector("[data-add-line]");
  if (add) add.addEventListener("click", () => {
    const hidden = rows.find(r => r.classList.contains("line-hidden"));
    if (hidden) hidden.classList.remove("line-hidden");
    if (!rows.some(r => r.classList.contains("line-hidden"))) add.style.display = "none";
  });
  recalc();
})();

// ---------------- Phase 4: laboratory ----------------
(function () {
  "use strict";
  const money = v => (Math.round(v * 100) / 100).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  // Simple row filter for tables: <input data-table-filter="#table-id">
  document.querySelectorAll("[data-table-filter]").forEach(input => {
    const table = document.querySelector(input.dataset.tableFilter);
    if (!table) return;
    input.addEventListener("input", () => {
      const q = input.value.trim().toLowerCase();
      table.querySelectorAll("tbody tr").forEach(tr => { tr.style.display = !q || tr.textContent.toLowerCase().includes(q) ? "" : "none"; });
    });
  });

  // Booking: test filter, selected count, total and referral fields.
  const book = document.querySelector("[data-lab-book]");
  if (book) {
    const choices = Array.from(book.querySelectorAll(".test-choice"));
    const filter = book.querySelector("[data-test-filter]");
    const count = book.querySelector("[data-selected-count]");
    const total = book.querySelector("[data-lab-total]");
    const recalc = () => {
      let n = 0, sum = 0;
      choices.forEach(c => {
        const cb = c.querySelector("input[type=checkbox]");
        c.classList.toggle("on", cb.checked);
        if (!cb.checked) return;
        n++;
        const disc = c.querySelector("input[name^=Discounts]");
        sum += Math.max(0, (parseFloat(cb.dataset.price || "0") || 0) - (disc ? parseFloat(disc.value || "0") || 0 : 0));
      });
      if (count) count.textContent = n + " selected";
      if (total) total.textContent = money(sum);
    };
    choices.forEach(c => c.querySelectorAll("input").forEach(i => i.addEventListener(i.type === "checkbox" ? "change" : "input", recalc)));
    if (filter) filter.addEventListener("input", () => {
      const q = filter.value.trim().toLowerCase();
      choices.forEach(c => { c.style.display = !q || (c.dataset.testName || "").includes(q) ? "" : "none"; });
    });
    const ref = book.querySelector("[data-referral]");
    const refresh = () => {
      if (!ref) return;
      book.querySelectorAll("[data-referral-doctor]").forEach(e => e.style.display = ref.value === "HospitalDoctor" ? "" : "none");
      book.querySelectorAll("[data-referral-external]").forEach(e => e.style.display = ref.value === "External" ? "" : "none");
    };
    if (ref) ref.addEventListener("change", refresh);
    refresh();
    recalc();
  }

  // Result entry: Enter moves to the next result box; live high/low hint for numeric values.
  const entry = document.querySelector("[data-entry-form]");
  if (entry) {
    const inputs = Array.from(entry.querySelectorAll(".result-input:not([disabled])"));
    inputs.forEach((el, idx) => {
      el.addEventListener("keydown", e => {
        if (e.key !== "Enter" || e.shiftKey) return;
        e.preventDefault();
        if (idx + 1 < inputs.length) inputs[idx + 1].focus();
        else { const next = entry.querySelector("[data-test=entry-next]"); if (next) next.focus(); }
      });
      if (el.dataset.low !== undefined || el.dataset.high !== undefined) {
        const check = () => {
          const v = parseFloat(el.value);
          const lo = el.dataset.low === "" || el.dataset.low === undefined ? NaN : parseFloat(el.dataset.low);
          const hi = el.dataset.high === "" || el.dataset.high === undefined ? NaN : parseFloat(el.dataset.high);
          el.classList.toggle("is-low", !isNaN(v) && !isNaN(lo) && v < lo);
          el.classList.toggle("is-high", !isNaN(v) && !isNaN(hi) && v > hi);
        };
        el.addEventListener("input", check);
        check();
      }
    });
  }
})();
