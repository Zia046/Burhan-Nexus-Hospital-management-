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
} catch (e) {
  errors.push("SCRIPT FAILURE: " + (e && e.stack || e));
  try { await shot(page, "p2-zz-failure"); } catch {}
} finally {
  fs.writeFileSync(`${out}/phase2-log.txt`, log.join("\n") + "\n\nERRORS:\n" + errors.join("\n"));
  await browser.close();
}
