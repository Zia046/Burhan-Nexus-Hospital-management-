// Phase 2 walk-through: patients, duplicate warning, OPD (private + government), emergency, slips → screenshots + sample PDFs.
import { chromium } from "playwright";
import fs from "node:fs";

const base = process.env.BASE_URL || "http://127.0.0.1:5080";
const out = process.env.OUT_DIR || "screenshots";
const pdfOut = process.env.PDF_DIR || "samples";
fs.mkdirSync(out, { recursive: true });
fs.mkdirSync(pdfOut, { recursive: true });
const log = [];
const step = m => { log.push(m); console.log(m); };
const shot = async (page, name, full = true) => { await page.screenshot({ path: `${out}/${name}.png`, fullPage: full }); step("screenshot " + name); };
const errors = [];

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
const page = await ctx.newPage();
page.on("pageerror", e => errors.push("pageerror: " + e.message));
page.on("console", m => { if (m.type() === "error") errors.push("console: " + m.text()); });

const go = async (url) => { await page.goto(base + url); };
const submit = async (selector) => { await Promise.all([page.waitForNavigation(), page.click(selector)]); };
const pdf = async (url, file) => {
  const p = await ctx.newPage();
  await p.goto(base + url);
  await p.emulateMedia({ media: "print" });
  await p.pdf({ path: `${pdfOut}/${file}`, preferCSSPageSize: true, printBackground: true });
  await p.screenshot({ path: `${out}/${file.replace(".pdf", "")}.png`, fullPage: true });
  await p.close();
  step("pdf " + file);
};

try {
  await go("/Account/Login");
  await page.fill("#Username", "admin");
  await page.fill("#Password", "Hospital#2026");
  await submit("button[type=submit]");

  // ---- Doctors (private mode) ----
  await go("/Opd/Setup");
  await page.fill("#dn", "Dr. Sana Khan");
  await page.fill("#dq", "MBBS, FCPS (Gynae)");
  await page.fill("#dsp", "Gynaecology & Obstetrics");
  await submit("button:has-text('Add doctor')");
  await page.fill("#dn", "Dr. Imran Ali");
  await page.fill("#dq", "MBBS, FCPS (Medicine)");
  await page.fill("#dsp", "General Medicine");
  await submit("button:has-text('Add doctor')");
  await shot(page, "p2-01-opd-setup");

  // ---- Register patient ----
  await go("/Patients/Register");
  await page.fill("[name='Patient.FullName']", "Rukhsana Bibi");
  await page.selectOption("[name='Patient.Gender']", "Female");
  await page.selectOption("[name='Patient.GuardianRelation']", "WifeOf");
  await page.fill("[name='Patient.GuardianName']", "Javed Iqbal");
  await page.fill("[name='Patient.DateOfBirth']", "1991-04-12");
  await page.fill("[name='Patient.MobileNumber']", "03451234567");
  await page.fill("[name='Patient.Cnic']", "1710112345678");
  await page.fill("[name='Patient.Address']", "House 12, Street 4, Hayatabad Phase 3");
  await page.fill("[name='Patient.CityOrVillage']", "Peshawar");
  await page.selectOption("[name='Patient.BloodGroup']", "B+");
  await page.waitForTimeout(900);
  await shot(page, "p2-02-register-patient");
  await submit("button[value='profile']");
  step("profile: " + page.url());
  const patientUrl = page.url();

  // ---- Duplicate warning ----
  await go("/Patients/Register");
  await page.fill("[name='Patient.FullName']", "Rukhsana Bibi");
  await page.selectOption("[name='Patient.Gender']", "Female");
  await page.fill("[name='Patient.GuardianName']", "Javed Iqbal");
  await page.fill("[name='Patient.Age']", "35");
  await page.fill("[name='Patient.Address']", "Hayatabad");
  await page.fill("[name='Patient.CityOrVillage']", "Peshawar");
  await page.waitForTimeout(1200);
  await shot(page, "p2-03-live-duplicate-hint");
  await submit("button[value='profile']");
  await shot(page, "p2-04-duplicate-warning");

  // ---- Private hospital: appointment with doctor-wise token + thermal slip ----
  await go(patientUrl.replace(base, ""));
  await submit("a:has-text('New OPD visit')");
  step("private OPD link → " + page.url());
  await page.check(`label.doctor-choice:has-text('Dr. Sana Khan') input`);
  await page.fill("#Input_Complaint", "Lower abdominal pain");
  await shot(page, "pa-05-appointment-book");
  await submit("button[data-test='appt-save']");
  await page.waitForTimeout(800);
  const slipUrl = page.url().split("?")[0];
  step("appointment slip: " + slipUrl);
  await shot(page, "pa-05b-thermal-slip-preview");
  // More bookings so each doctor shows an independent token series.
  const pid = new URL(patientUrl).pathname.split("/").pop();
  for (const [doc, n] of [["Dr. Sana Khan", 3], ["Dr. Imran Ali", 4]]) {
    for (let i = 0; i < n; i++) {
      await go(`/Appointments/Book?patientId=${pid}`);
      await page.check(`label.doctor-choice:has-text('${doc}') input`);
      await submit("button[data-test='appt-save']");
    }
  }
  await go(`/Appointments/Book?patientId=${pid}`);
  await shot(page, "pa-05c-appointment-next-tokens");
  await go("/Appointments");
  await shot(page, "pa-06-appointments-doctor-wise");
  await go("/Appointments/Reports");
  await shot(page, "pa-07-appointment-report-daily");
  await go("/Settings/Printers");
  await shot(page, "pa-08-printer-settings");
  await go("/Settings/Appointments");
  await shot(page, "pa-08b-appointment-settings");
  await pdf(slipUrl.replace(base, "") + "?width=80", "sample-appointment-thermal-80mm.pdf");
  await pdf(slipUrl.replace(base, "") + "?width=58", "sample-appointment-thermal-58mm.pdf");
  await go(slipUrl.replace(base, "").replace("/Print/Appointment/", "/Appointments/Details/"));
  await shot(page, "pa-09-appointment-details");
  await go("/");
  await shot(page, "pa-09b-dashboard-private");

  // ---- Switch to Government mode ----
  await go("/Settings/HospitalType");
  await page.selectOption("#NewType", "Government");
  await page.fill("#Password", "Hospital#2026");
  await page.fill("#Confirmation", "CHANGE");
  await submit("button:has-text('Change hospital type')");

  // ---- Government OPD: new patient through "Register & OPD visit" ----
  await go("/Patients/Register");
  await page.fill("[name='Patient.FullName']", "Fazal Rabi");
  await page.selectOption("[name='Patient.Gender']", "Male");
  await page.fill("[name='Patient.GuardianName']", "Sher Ali Khan");
  await page.fill("[name='Patient.Age']", "52");
  await page.fill("[name='Patient.MobileNumber']", "0312-9876543");
  await page.fill("[name='Patient.Address']", "Village Shabqadar");
  await page.fill("[name='Patient.CityOrVillage']", "Charsadda");
  await submit("button[value='opd']");
  await page.fill("#Visit_ChiefComplaint", "Fever and cough for 3 days");
  await shot(page, "p2-06-opd-register-government");
  await submit("button:has-text('Register')");
  const govUrl = page.url().split("?")[0].replace(base, "");
  await pdf(govUrl + "?paper=A5", "sample-opd-slip-A5.pdf");
  await pdf(govUrl + "?paper=A4", "sample-opd-slip-A4.pdf");

  // Repeat visit for the same patient (same Patient ID, new visit number).
  const visitId = govUrl.split("/").pop();
  await go("/Opd/Visit/" + visitId);
  await shot(page, "p2-07-opd-visit");
  await go("/Opd");
  await shot(page, "p2-08-opd-list");

  // ---- Emergency: unidentified patient ----
  await go("/Emergency/Register");
  await page.check("[data-unidentified]");
  await page.selectOption("[name='Patient.Gender']", "Male");
  await page.fill("[name='Patient.Age']", "30");
  await page.selectOption("#cat", { label: "Road Traffic Accident" });
  await page.fill("#bc", "Head injury, unconscious, bleeding from scalp");
  await page.fill("#bb", "Rescue 1122");
  await page.check("[name='Visit.IsMedicoLegal']");
  await shot(page, "p2-09-emergency-register");
  await submit("button:has-text('Register')");
  const erUrl = page.url().split("?")[0].replace(base, "");
  await pdf(erUrl + "?paper=A4", "sample-emergency-slip-A4.pdf");
  await pdf(erUrl + "?paper=A5", "sample-emergency-slip-A5.pdf");

  // Status update.
  const erId = erUrl.split("/").pop();
  await go("/Emergency/Visit/" + erId);
  await page.check("input[name='NewStatus'][value='UnderTreatment']");
  page.once("dialog", d => d.accept());
  await submit("button:has-text('Update status')");
  await page.check("input[name='NewStatus'][value='Referred']");
  await page.fill("#reason", "Referred to Lady Reading Hospital, Peshawar for CT brain");
  page.once("dialog", d => d.accept());
  await submit("button:has-text('Update status')");
  await shot(page, "p2-10-emergency-visit");
  await go("/Emergency?status=all");
  await shot(page, "p2-11-emergency-list");

  // ---- Search & profile ----
  await go("/Patients");
  await shot(page, "p2-12-patient-search");
  await go(patientUrl.replace(base, ""));
  await shot(page, "p2-13-patient-profile");
  await go("/");
  await shot(page, "p2-14-dashboard");
  await go("/Settings/Registration");
  await shot(page, "p2-15-registration-settings");

  // ---- Phase 3: finance ----
  await go("/Finance/Counters");
  await page.fill("#Form_Code", "LAB");
  await page.fill("#Form_Name", "Laboratory Counter");
  await submit("button[data-test='counter-save']");
  await go("/Finance/Services");
  await page.fill("#Form_Code", "LAB-CBC");
  await page.selectOption("#Form_Kind", "Laboratory");
  await page.fill("#Form_Name", "CBC (Complete Blood Count)");
  await page.fill("#Form_Fee", "800");
  await submit("button[data-test='service-save']");
  await go("/Finance/Services");
  await page.fill("#Form_Code", "XR-CHEST");
  await page.selectOption("#Form_Kind", "XRay");
  await page.fill("#Form_Name", "Chest X-Ray PA view");
  await page.fill("#Form_Fee", "300");
  await submit("button[data-test='service-save']");
  await shot(page, "p3-01-services-fees");
  const pid3 = new URL(patientUrl).pathname.split("/").pop();
  await go(`/Finance/Collect?patientId=${pid3}`);
  const cbc = await page.$$eval("select[name='Lines[0].ServiceItemId'] option", os => (os.find(o => o.textContent.includes("CBC")) || {}).value);
  const xr = await page.$$eval("select[name='Lines[0].ServiceItemId'] option", os => (os.find(o => o.textContent.includes("Chest X-Ray")) || {}).value);
  await page.selectOption("select[name='Lines[0].ServiceItemId']", cbc);
  await page.click("[data-add-line]");
  await page.selectOption("select[name='Lines[1].ServiceItemId']", xr);
  const counterSel = await page.$("#Input_CounterId");
  if (counterSel) await page.selectOption("#Input_CounterId", { index: 1 }).catch(() => {});
  await shot(page, "p3-02-collect-payment");
  await submit("button[data-test='collect-save']");
  await page.waitForTimeout(800);
  const receiptUrl = page.url().split("?")[0].replace(base, "");
  step("receipt: " + receiptUrl);
  await shot(page, "p3-03-receipt-80mm");
  await pdf(receiptUrl + "?width=80", "sample-receipt-80mm.pdf");
  await pdf(receiptUrl + "?width=58", "sample-receipt-58mm.pdf");
  await pdf(receiptUrl + "?format=a4", "sample-receipt-A4.pdf");
  await go(receiptUrl.replace("/Print/Receipt/", "/Finance/Receipts/Details/"));
  await shot(page, "p3-04-receipt-details");
  await go("/Finance");
  await shot(page, "p3-05-my-account");
  await go("/Finance/Receipts");
  await shot(page, "p3-06-receipts");
  await go("/Finance/Dashboard");
  await shot(page, "p3-07-financial-dashboard");
  await go("/Finance/Reports");
  await shot(page, "p3-08-financial-reports");
  await go("/Finance/Counters");
  await shot(page, "p3-09-counters");
  await go("/Finance/Doctors");
  await shot(page, "p3-10-doctor-accounts");
  await go("/Finance/Closing?scope=Hospital");
  await shot(page, "p3-11-daily-closing");
  await go("/Finance/Settings");
  await shot(page, "p3-12-finance-settings");

  // ---- Phase 4: laboratory ----
  await go("/Lab/Settings");
  await page.fill("#Input_LaboratoryName", "Burhan Nexus Clinical Laboratory");
  await page.fill("#Input_SignatoryName", "Dr. Ayesha Malik");
  await page.fill("#Input_SignatoryDesignation", "Consultant Pathologist");
  await page.check("#Input_AllowSelfVerification");
  await submit("button[data-test='save-lab-settings']");
  await shot(page, "p4-01-lab-settings");
  for (const [code, price] of [["CBC", "800"], ["BSR", "250"]]) {
    await go("/Lab/Catalogue");
    await submit(`a[data-test='edit-${code}']`);
    await page.check("#Input_IsActive");
    await page.uncheck("#Input_IsFree");
    await page.fill("#Input_Price", price);
    if (code === "CBC") await shot(page, "p4-03-test-definition");
    await submit("button[data-test='save-test']");
  }
  await go("/Lab/Catalogue");
  await shot(page, "p4-02-test-catalogue");
  await go(`/Lab/Book?patientId=${pid}`);
  for (const code of ["cbc", "bsr"]) await page.check(`label.test-choice[data-test-name$=' ${code}'] input[type=checkbox]`);
  await page.selectOption("#Input_ReferralType", "External");
  await page.fill("#Input_ExternalDoctorName", "Dr. Kamran (City Clinic)");
  await page.fill("#Input_ClinicalNotes", "Fever for 3 days");
  await shot(page, "p4-04-lab-booking");
  await submit("button[data-test='lab-book-save']");
  const orderPath = new URL(page.url()).pathname;
  const orderId = orderPath.split("/").pop();
  step("lab order: " + orderPath);
  await shot(page, "p4-05-lab-visit");
  await pdf(`/Print/LabSlip/${orderId}`, "sample-lab-slip-80mm.pdf");
  await go(`/Finance/Collect?patientId=${pid}&labOrderId=${orderId}`);
  await shot(page, "p4-06-lab-payment-at-cashier");
  await go(orderPath);
  for (let i = 0; i < 4; i++) {
    const btn = await page.$("button[data-test^='collect-']");
    if (!btn) break;
    await Promise.all([page.waitForNavigation(), btn.click()]);
  }
  await go("/Lab/Samples");
  await shot(page, "p4-07-sample-collection");
  await go("/Lab/Pending");
  await shot(page, "p4-08-pending-tests");
  await go(`/Lab/Entry/${orderId}`);
  for (let round = 0; round < 4 && page.url().includes("/Lab/Entry/"); round++) {
    const inputs = await page.$$(".result-input:not([disabled])");
    let k = 0;
    for (const el of inputs) {
      const tag = await el.evaluate(e => e.tagName);
      if (tag === "SELECT") { await el.selectOption({ index: 1 }); continue; }
      const lo = parseFloat(await el.getAttribute("data-low") || "");
      const hi = parseFloat(await el.getAttribute("data-high") || "");
      let v = !isNaN(lo) && !isNaN(hi) ? (lo + hi) / 2 : !isNaN(hi) ? hi * 0.8 : !isNaN(lo) ? lo * 1.2 : 5;
      if (k++ === 1 && !isNaN(hi)) v = hi * 1.25; // one abnormal value for the flag
      await el.fill((Math.round(v * 10) / 10).toString());
    }
    if (round === 0) await shot(page, "p4-09-result-entry");
    const next = await page.$("button[data-test='entry-next']");
    if (!next) break;
    await Promise.all([page.waitForNavigation(), next.click()]);
  }
  await go("/Lab/Verify");
  await shot(page, "p4-10-verification-queue");
  await go(`/Lab/Verify/${orderId}`);
  await shot(page, "p4-11-patient-verification");
  const all = await page.$("button[data-test='verify-all']");
  if (all) await Promise.all([page.waitForNavigation(), all.click()]);
  await go(`/Print/LabReport/${orderId}`);
  await shot(page, "p4-12-lab-report-preview");
  const printBtn = await page.$("button[data-test='lab-report-print']");
  if (printBtn) await Promise.all([page.waitForNavigation(), printBtn.click()]);
  await pdf(`/Print/LabReport/${orderId}`, "sample-lab-report-A4.pdf");
  await go("/Lab/Results");
  await shot(page, "p4-13-lab-results");
  await go(`/Lab/Patient/${pid}`);
  await shot(page, "p4-14-previous-results");
  await go("/Lab");
  await shot(page, "p4-15-lab-dashboard");
  await go("/Lab/Reports");
  await shot(page, "p4-16-lab-reports");

  // ---- Phase 5: pharmacy (Government first, then Private) ----
  const ym = m => { const d = new Date(); d.setDate(1); d.setMonth(d.getMonth() + m); return d.toISOString().slice(0, 7); };
  const addMed = async q => {
    await page.fill("[data-med-search]", "");
    await page.type("[data-med-search]", q, { delay: 30 });
    await page.waitForSelector("[data-med-result]", { timeout: 5000 });
    await page.click("[data-med-result]");
    await page.waitForTimeout(200);
  };
  const newMedicine = async (name, generic, strength, form, unit, min) => {
    await go("/Pharmacy/Medicines/Edit");
    await page.fill("#Input_Name", name);
    await page.fill("#Input_GenericName", generic);
    await page.fill("#Input_Strength", strength);
    await page.selectOption("#Input_DosageFormId", { label: form });
    await page.fill("#Input_Unit", unit);
    await page.fill("#Input_MinimumStock", String(min));
    await submit("button[data-test='save-medicine']");
    return new URL(page.url()).pathname.split("/").pop();
  };
  const paraId = await newMedicine("Paracetamol", "Acetaminophen", "500 mg", "Tablet", "Tablet", 200);
  const amoxId = await newMedicine("Amoxil", "Amoxicillin", "250 mg", "Capsule", "Capsule", 100);
  await newMedicine("Tetanus Toxoid", "TT vaccine", "0.5 ml", "Injection", "Ampoule", 20);
  await go("/Pharmacy/Medicines/Edit");
  await page.fill("#Input_Name", "PARACETAMOL");
  await page.fill("#Input_Strength", "500mg");
  await page.selectOption("#Input_DosageFormId", { label: "Tablet" });
  await submit("button[data-test='save-medicine']");
  await shot(page, "p5-02-duplicate-medicine-warning");
  await go("/Pharmacy/Receive");
  await page.fill("#Input_SourceName", "District Health Office");
  await page.fill("#Input_ReferenceNumber", "DHO/2026/114");
  for (const [q, batch, exp, qty] of [["Parac", "PCM-2401", ym(4), 500], ["Parac", "PCM-2409", ym(14), 1000], ["Amoxil", "AMX-118", ym(10), 300], ["Tetanus", "TT-77", ym(8), 60]]) {
    await addMed(q);
    const row = "[data-receive-lines] tbody tr:last-child";
    await page.fill(`${row} [data-name=BatchNumber]`, batch);
    await page.fill(`${row} [data-name=Expiry]`, exp);
    await page.fill(`${row} [data-name=Quantity]`, String(qty));
  }
  await shot(page, "p5-03-receive-stock-government");
  await submit("button[data-test='save-receipt']");
  await shot(page, "p5-04-goods-received-note");
  await go(`/Pharmacy/Issue?encounterId=${visitId}`);
  await addMed("Parac");
  await page.fill("[data-med-lines] tbody tr:last-child [data-name=Quantity]", "15");
  await page.fill("[data-med-lines] tbody tr:last-child [data-name=Instructions]", "1 tab three times a day after meals");
  await addMed("Amoxil");
  await page.fill("[data-med-lines] tbody tr:last-child [data-name=Quantity]", "21");
  await page.fill("[data-med-lines] tbody tr:last-child [data-name=Instructions]", "1 cap three times a day for 7 days");
  await shot(page, "p5-05-patient-issue-government");
  await submit("button[data-test='save-issue']");
  const issPath = new URL(page.url()).pathname;
  await shot(page, "p5-06-issue-document");
  await pdf(issPath.replace("/Pharmacy/Doc/", "/Print/Pharmacy/"), "sample-pharmacy-issue-slip-80mm.pdf");
  await go("/Pharmacy/Transfer");
  await addMed("Tetanus");
  await page.fill("[data-med-lines] tbody tr:last-child [data-name=Quantity]", "20");
  await addMed("Parac");
  await page.fill("[data-med-lines] tbody tr:last-child [data-name=Quantity]", "100");
  await page.fill("#Input_Reference", "OT weekly supply");
  await shot(page, "p5-07-transfer-to-minor-ot");
  await submit("button[data-test='save-transfer']");
  await pdf(new URL(page.url()).pathname.replace("/Pharmacy/Doc/", "/Print/Pharmacy/"), "sample-pharmacy-transfer-slip-80mm.pdf");
  await go("/Pharmacy/Stock");
  await shot(page, "p5-08-batch-stock");
  await go(`/Pharmacy/Medicines/${paraId}`);
  await shot(page, "p5-09-medicine-history");
  await go("/Pharmacy/Medicines");
  await shot(page, "p5-01-medicine-catalogue");
  await go("/Pharmacy/Adjustments");
  await shot(page, "p5-10-stock-adjustments");
  await go("/Pharmacy/Reports?report=department-stock");
  await shot(page, "p5-11-report-minor-ot-stock");
  await go("/Pharmacy");
  await shot(page, "p5-12-dashboard-government");

  // Private hospital: prices, supplier purchase, walk-in sale with receipt.
  await go("/Settings/HospitalType");
  await page.selectOption("#NewType", "Private");
  await page.fill("#Password", "Hospital#2026");
  await page.fill("#Confirmation", "CHANGE");
  await submit("button:has-text('Change hospital type')");
  for (const [id, cost, sale] of [[paraId, "1.20", "2.00"], [amoxId, "5.50", "8.00"]]) {
    await go(`/Pharmacy/Medicines/Edit/${id}`);
    await page.fill("#Input_PurchasePrice", cost);
    await page.fill("#Input_SalePrice", sale);
    await submit("button[data-test='save-medicine']");
    if (page.url().includes("/Edit")) { await page.check("[name='Input.ConfirmDuplicate']"); await submit("button[data-test='save-medicine']"); }
  }
  await go("/Pharmacy/Suppliers/Edit");
  await page.fill("#Input_Name", "Peshawar Medical Distributors");
  await page.fill("#Input_ContactPerson", "Haji Gul");
  await page.fill("#Input_Phone", "091-2561234");
  await page.fill("#Input_City", "Peshawar");
  await submit("button[data-test='save-supplier']");
  await go("/Pharmacy/Receive");
  await page.selectOption("#Input_SupplierId", { label: "Peshawar Medical Distributors" });
  await page.fill("#Input_ReferenceNumber", "PMD-88231");
  await addMed("Amoxil");
  const prow = "[data-receive-lines] tbody tr:last-child";
  await page.fill(`${prow} [data-name=BatchNumber]`, "AMX-131");
  await page.fill(`${prow} [data-name=Expiry]`, ym(16));
  await page.fill(`${prow} [data-name=Quantity]`, "200");
  await page.fill(`${prow} [data-name=UnitCost]`, "5.50");
  await shot(page, "p5-13-purchase-private");
  await submit("button[data-test='save-receipt']");
  await go("/Pharmacy/Sale");
  await page.fill("#Input_CustomerName", "Walk-in customer");
  await addMed("Parac");
  await page.fill("[data-med-lines] tbody tr:last-child [data-name=Quantity]", "20");
  await addMed("Amoxil");
  await page.fill("[data-med-lines] tbody tr:last-child [data-name=Quantity]", "10");
  await page.fill("[data-med-lines] tbody tr:last-child [data-name=Discount]", "5");
  await page.fill("#Input_DiscountReason", "Regular customer");
  await shot(page, "p5-14-retail-sale");
  await submit("button[data-test='save-sale']");
  await shot(page, "p5-15-sale-document");
  await pdf(new URL(page.url()).pathname.replace("/Pharmacy/Doc/", "/Print/Pharmacy/"), "sample-pharmacy-sale-receipt-80mm.pdf");
  await go("/Pharmacy/Reports?report=gross-profit&period=today");
  await shot(page, "p5-16-gross-profit");
  await go("/Pharmacy?period=today");
  await shot(page, "p5-17-dashboard-private");

  // ---- Phase 6: radiology (X-Ray & Ultrasound) ----
  for (const [code, fee] of [["XR-CXR-PA", "800"], ["US-ABD", "1500"]]) {
    await go("/Radiology/Services");
    await submit(`a[data-test='edit-${code}']`);
    await page.check("[name='Input.IsActive'][type=checkbox]");
    await page.uncheck("[name='Input.IsFree'][type=checkbox]");
    await page.fill("#Input_Fee", fee);
    await submit("button[data-test='rad-save-service']");
  }
  await go("/Radiology/Services");
  await shot(page, "p6-01-services-and-templates");
  await go("/Radiology/Settings");
  await page.check("[name='Input.AllowSelfVerification'][type=checkbox]");
  await page.fill("#Input_DefaultReportingDoctor", "Dr. Sana Gul");
  await page.fill("#Input_DefaultDesignation", "Consultant Radiologist");
  await shot(page, "p6-10-radiology-settings");
  await submit("button[data-test='rad-save-settings']");
  await go(`/Radiology/Book?encounterId=${visitId}`);
  await page.check("[data-rad-service='XR-CXR-PA']");
  await page.check("[data-rad-service='US-ABD']");
  await page.fill("#Input_ClinicalIndication", "Cough and fever for 2 weeks; right upper abdominal pain (handwritten advice)");
  await shot(page, "p6-02-booking");
  await submit("button[data-test='rad-book-save']");
  await shot(page, "p6-03-order");
  const examHref = await page.getAttribute("[data-order-exams] tr[data-exam='XR-CXR-PA'] a", "href");
  await go("/Radiology/Worklist/XRay");
  await shot(page, "p6-04-xray-worklist");
  await go("/Radiology/Worklist/Ultrasound");
  await shot(page, "p6-09-ultrasound-worklist");
  await go(examHref);
  await submit("button[data-test='rad-arrive']");
  await submit("button[data-test='rad-start']");
  await page.fill("[data-exam-actions] input[name=PerformedBy]", "Tech Imran");
  await page.fill("[data-exam-actions] input[name=TechnicalNotes]", "PA erect, good inspiration");
  await shot(page, "p6-05-examination-in-progress");
  await submit("button[data-test='rad-complete']");
  const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==", "base64");
  await page.setInputFiles("[data-upload] input[type=file]", { name: "chest-pa.png", mimeType: "image/png", buffer: png });
  await page.fill("[data-upload] input[name=Description]", "Chest PA film (scanned)");
  await submit("button[data-test='rad-upload']");
  await submit("a[data-test='rad-write-report']");
  await page.fill("#Input_Findings", "Trachea is central.\nPatchy consolidation in the right lower zone.\nLeft lung field is clear.\nCostophrenic angles are clear.\nCardiac size is within normal limits.");
  await page.fill("#Input_Impression", "Right lower zone consolidation – likely pneumonia.");
  await page.fill("#Input_Recommendations", "Clinical correlation and follow-up film after treatment.");
  await shot(page, "p6-06-report-entry");
  await submit("button[data-test='rad-submit']");
  await go("/Radiology/Verify");
  await shot(page, "p6-07-verification-queue");
  await go(examHref);
  await submit("button[data-test='rad-verify']");
  await shot(page, "p6-08-examination-verified");
  await go(examHref.replace("/Radiology/Exam/", "/Print/RadiologyReport/"));
  await submit("button[data-test='rad-report-print']");
  await pdf(examHref.replace("/Radiology/Exam/", "/Print/RadiologyReport/"), "sample-radiology-report-A4.pdf");
  await go("/Radiology?period=today");
  await shot(page, "p6-11-radiology-dashboard");
  await go("/Radiology/Reports?report=services&period=today");
  await shot(page, "p6-12-statistics");
  await go(`/Radiology/Reporting`);
  await shot(page, "p6-14-reporting-list");
  await go(examHref);
  const histHref = await page.getAttribute("a[href^='/Radiology/Patient/']", "href");
  await go(histHref);
  await shot(page, "p6-13-patient-radiology-history");

  // ---- Phase 7: IPD, wards & beds, Gynae / ANC, Labour Room, delivery, NICU, nursing & MAR ----
  for (const [room, code, type] of [["Male General Ward / Room 1", "MGW-01", "Standard"], ["Male General Ward / Room 1", "MGW-02", "Standard"],
                                    ["Labour Room / Room 1", "LR-01", "Labour"], ["NICU / Room 1", "NICU-01", "Incubator"], ["NICU / Room 1", "NICU-02", "Cot"]]) {
    await go("/Ipd/Wards");
    await page.selectOption("#Bed_RoomId", { label: room });
    await page.fill("#Bed_Code", code);
    await page.selectOption("#Bed_Type", type);
    await submit("button[data-test='save-bed']");
  }
  await shot(page, "p7-01-wards-rooms-beds");
  await go("/Patients/Register");
  await page.fill("[name='Patient.FullName']", "Ahmad Khan");
  await page.selectOption("[name='Patient.Gender']", "Male");
  await page.selectOption("[name='Patient.GuardianRelation']", "SonOf");
  await page.fill("[name='Patient.GuardianName']", "Gul Khan");
  await page.fill("[name='Patient.DateOfBirth']", "1978-02-10");
  await page.fill("[name='Patient.MobileNumber']", "03339876543");
  await page.fill("[name='Patient.Address']", "Mohallah Sethi, Ring Road");
  await page.fill("[name='Patient.CityOrVillage']", "Peshawar");
  await page.waitForTimeout(900);
  await submit("button[value='profile']");
  step("ipd patient: " + page.url());
  const ahmadId = new URL(page.url()).pathname.split("/").pop();
  await go(`/Ipd/Admit?patientId=${ahmadId}`);
  await page.selectOption("select[data-bed-list]", { index: 1 });
  await page.fill("#Input_AdmissionDiagnosis", "Fever with abdominal pain (as written by the doctor)");
  await page.fill("#Input_Allergies", "Penicillin");
  await page.fill("#Input_AttendantName", "Gul Khan");
  await shot(page, "p7-02-admission-form");
  await submit("button[data-test='ipd-admit-save']");
  const admUrl = new URL(page.url()).pathname;
  await shot(page, "p7-03-inpatient-chart");
  await go(admUrl + "?tab=notes");
  await page.click("[data-record-form='Vitals'] summary");
  await page.fill("[data-record-form='Vitals'] [name='Record.Values[temp]']", "38.6");
  await page.fill("[data-record-form='Vitals'] [name='Record.Values[pulse]']", "104");
  await page.fill("[data-record-form='Vitals'] [name='Record.Values[bp_sys]']", "118");
  await submit("button[data-test='save-Vitals']");
  await page.click("[data-record-form='ProgressNote'] summary");
  await page.fill("[data-record-form='ProgressNote'] [name='Record.Values[condition]']", "Febrile, conscious, tolerating orally");
  await page.fill("[data-record-form='ProgressNote'] [name='Record.Values[plan]']", "Continue treatment as per consultant");
  await submit("button[data-test='save-ProgressNote']");
  await shot(page, "p7-04-progress-and-vitals");
  await go(admUrl + "?tab=mar");
  await page.click("[data-order-form] summary");
  await page.selectOption("[name='Order.MedicineId']", paraId);
  await page.fill("[name='Order.Dose']", "1 tab");
  await page.fill("[name='Order.Route']", "Oral");
  await page.fill("[name='Order.Frequency']", "TDS");
  await page.fill("[name='Order.Times']", "08:00, 14:00, 20:00");
  await page.fill("[name='Order.DurationDays']", "3");
  await page.fill("[name='Order.Prescriber']", "Dr. Imran Ali");
  await submit("button[data-test='save-order']");
  await page.click("[data-dose] details summary");
  for (const c of ["ConfirmPatient", "ConfirmMedicine", "ConfirmDose", "ConfirmRoute", "ConfirmTime"]) await page.check(`[data-dose] details[open] [name='Dose.${c}']`);
  await shot(page, "p7-05-mar-record-dose");
  await submit("[data-dose] details[open] button[data-test='save-dose']");
  await shot(page, "p7-06-mar");
  await go(admUrl + "?tab=nursing");
  await page.click("[data-handovers] details summary");
  await page.fill("[name='Handover.PatientCondition']", "Febrile at 14:00, otherwise stable");
  await page.fill("[name='Handover.MedicationDue']", "20:00 Paracetamol");
  await submit("button[data-test='save-handover']");
  await shot(page, "p7-07-nursing-handover");
  await go("/Ipd/Beds");
  await shot(page, "p7-08-bed-map");

  // Maternity: pregnancy → ANC → Labour Room → partograph → twins delivery → NICU.
  const rukhsanaId = new URL(patientUrl).pathname.split("/").pop();
  await go(`/Ipd/Gynae/Patient/${rukhsanaId}`);
  const lmp = new Date(Date.now() - 268 * 86400000).toISOString().slice(0, 10);
  await page.fill("[name='Preg.Lmp']", lmp);
  await page.fill("[name='Preg.Gravida']", "3");
  await page.fill("[name='Preg.Para']", "2");
  await submit("button[data-test='save-pregnancy']");
  await page.fill("[data-record-form='AncVisit'] [name='Record.Values[bp_sys]']", "112");
  await page.fill("[data-record-form='AncVisit'] [name='Record.Values[bp_dia]']", "74");
  await page.fill("[data-record-form='AncVisit'] [name='Record.Values[fhr]']", "142");
  await page.fill("[data-record-form='AncVisit'] [name='Record.Values[fundal_height]']", "36");
  await submit("button[data-test='save-AncVisit']");
  await shot(page, "p7-09-pregnancy-anc-timeline");
  await go(`/Ipd/Labour/Admit?patientId=${rukhsanaId}`);
  await page.selectOption("select[data-bed-list]", { index: 1 });
  await page.fill("[name='Labour.MembraneStatus']", "Intact");
  await page.fill("[name='Labour.PresentingComplaints']", "Labour pains since morning");
  await submit("button[data-test='labour-admit']");
  const labourUrl = new URL(page.url()).pathname;
  for (const [cx, fhr] of [["4", "140"], ["6", "138"], ["8", "136"]]) {
    await go(labourUrl + "?tab=labour");
    await page.click("[data-record-form='LabourObservation'] summary");
    await page.fill("[data-record-form='LabourObservation'] [name='Record.Values[cervix]']", cx);
    await page.fill("[data-record-form='LabourObservation'] [name='Record.Values[fhr]']", fhr);
    await page.fill("[data-record-form='LabourObservation'] [name='Record.Values[contractions]']", "3");
    await submit("button[data-test='save-LabourObservation']");
  }
  await shot(page, "p7-10-labour-room-observations");
  await pdf(labourUrl.replace("/Ipd/Admission/", "/Print/Ipd/partograph/"), "sample-ipd-partograph-A4.pdf");
  await go("/Ipd/Labour");
  await shot(page, "p7-11-labour-room-dashboard");
  await go(labourUrl.replace("/Ipd/Admission/", "/Ipd/Delivery/New/") + "?babies=2");
  await page.selectOption("[name='Input.Babies[0].Sex']", "Female");
  await page.fill("[name='Input.Babies[0].BirthWeightGrams']", "2700");
  await page.fill("[name='Input.Babies[0].Apgar1']", "8");
  await page.fill("[name='Input.Babies[0].Apgar5']", "9");
  await page.selectOption("[name='Input.Babies[1].Sex']", "Male");
  await page.fill("[name='Input.Babies[1].BirthWeightGrams']", "1850");
  await page.fill("[name='Input.Babies[1].Apgar1']", "6");
  await page.selectOption("[name='Input.Babies[1].Placement']", "Nicu");
  await page.selectOption("[name='Input.Babies[1].NicuBedId']", { index: 1 });
  await page.fill("[name='Input.Babies[1].NicuReason']", "Low birth weight (as assessed by the doctor)");
  await shot(page, "p7-12-delivery-entry-twins");
  await submit("button[data-test='save-delivery']");
  await shot(page, "p7-13-delivery-record");
  await pdf(new URL(page.url()).pathname.replace("/Ipd/Delivery/", "/Print/Ipd/delivery/"), "sample-ipd-delivery-record-A4.pdf");
  await go("/Ipd/Nicu");
  await shot(page, "p7-14-nicu-dashboard");
  await go("/Ipd/Gynae");
  await shot(page, "p7-15-gynae-anc");
  await go("/Ipd/Nursing");
  await shot(page, "p7-16-nursing-dashboard");

  // Discharge of the general patient → discharge summary and final bill.
  await go(admUrl + "?tab=discharge");
  await page.selectOption("[name='Discharge.Type']", "Routine");
  await page.fill("[name='Discharge.FinalDiagnosis']", "Enteric fever (as documented by the doctor)");
  await page.fill("[name='Discharge.ConditionAtDischarge']", "Stable, afebrile");
  await page.fill("[name='Discharge.TreatmentSummary']", "Treated as per the consultant's orders.");
  await page.fill("[name='Discharge.FollowUp']", "OPD after one week with reports");
  page.once("dialog", d => d.accept());
  await submit("button[data-test='discharge']");
  await shot(page, "p7-17-discharged");
  await pdf(admUrl.replace("/Ipd/Admission/", "/Print/Ipd/discharge/"), "sample-ipd-discharge-summary-A4.pdf");
  await pdf(admUrl.replace("/Ipd/Admission/", "/Print/Ipd/bill/"), "sample-ipd-final-bill-A4.pdf");
  await pdf(admUrl.replace("/Ipd/Admission/", "/Print/Ipd/admission/"), "sample-ipd-admission-form-A4.pdf");
  await pdf(admUrl.replace("/Ipd/Admission/", "/Print/Ipd/mar/"), "sample-ipd-mar-A4.pdf");
  await go("/Ipd");
  await shot(page, "p7-18-ipd-dashboard");
} catch (e) {
  errors.push("SCRIPT FAILURE: " + (e && e.stack || e));
  try { await shot(page, "p2-zz-failure"); } catch {}
} finally {
  fs.writeFileSync(`${out}/phase2-log.txt`, log.join("\n") + "\n\nERRORS:\n" + errors.join("\n"));
  await browser.close();
}
