// End-to-end UI walk-through of Phase 1 with screenshots (runs in CI against the real server + PostgreSQL).
import { chromium } from "playwright";
import fs from "node:fs";

const base = process.env.BASE_URL || "http://127.0.0.1:5080";
const out = process.env.OUT_DIR || "screenshots";
fs.mkdirSync(out, { recursive: true });
const log = [];
const step = (m) => { log.push(m); console.log(m); };
const shot = async (page, name, full = true) => { await page.screenshot({ path: `${out}/${name}.png`, fullPage: full }); step(`screenshot ${name}`); };

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });
const page = await ctx.newPage();
const errors = [];
page.on("pageerror", e => errors.push("pageerror: " + e.message));
page.on("console", m => { if (m.type() === "error") errors.push("console: " + m.text()); });

try {
  // ---- Setup wizard ----
  await page.goto(base + "/");
  step("landed on " + page.url());
  await shot(page, "01-setup-step1-empty");
  await page.fill("#HospitalName", "Al-Shifa Teaching Hospital");
  await page.fill("#RegistrationNumber", "KP-HC-2026-0457");
  await page.fill("#ContactNumber", "091-5701234");
  await page.fill("#Address", "University Road, Near Board Bazaar");
  await page.fill("#City", "Peshawar");
  await page.selectOption("#Province", "Khyber Pakhtunkhwa");
  await page.fill("#Email", "info@alshifa-hospital.pk");
  await page.fill("#Website", "www.alshifa-hospital.pk");
  await page.setInputFiles("#Logo", "sample-logo.png");
  await shot(page, "02-setup-step1-filled");
  await page.click("[data-wizard-next]");
  await page.click("label.choice:has-text('Private Hospital')");
  await shot(page, "03-setup-step2-type");
  await page.click("[data-wizard-next]");
  await page.fill("#AdminFullName", "Ziaullah Khan");
  await page.fill("#AdminUsername", "admin");
  await page.fill("#AdminPassword", "Hospital#2026");
  await page.fill("#AdminConfirmPassword", "Hospital#2026");
  await shot(page, "04-setup-step3-admin");
  await page.click("[data-wizard-next]");
  await shot(page, "05-setup-step4-preferences");
  await page.click("[data-wizard-next]");
  await shot(page, "06-setup-step5-review");
  await Promise.all([page.waitForNavigation(), page.click("[data-wizard-finish]")]);
  step("after setup: " + page.url());

  // ---- Login ----
  await shot(page, "07-login", false);
  await page.fill("#Username", "admin");
  await page.fill("#Password", "wrong-password1");
  await Promise.all([page.waitForNavigation(), page.click("button[type=submit]")]);
  await shot(page, "08-login-error", false);
  await page.fill("#Username", "admin");
  await page.fill("#Password", "Hospital#2026");
  await page.click("details.workstation summary");
  await page.fill("#Workstation", "Admin-Office");
  await Promise.all([page.waitForNavigation(), page.click("button[type=submit]")]);
  step("after login: " + page.url());
  await shot(page, "09-dashboard-admin");

  // ---- Users ----
  await page.goto(base + "/Users/Create");
  await page.fill("#FullName", "Sana Bibi");
  await page.fill("#Designation", "Receptionist");
  await page.fill("#ContactNumber", "0300-1234567");
  await page.fill("#Username", "sana.reception");
  await page.fill("#Password", "Welcome#2026");
  await page.fill("#ConfirmPassword", "Welcome#2026");
  await page.uncheck("#MustChangePassword");
  await page.check("label.check-card:has-text('Reception') input");
  await shot(page, "10-user-create");
  await Promise.all([page.waitForNavigation(), page.click("button[type=submit]:has-text('Create user')")]);
  const userUrl = page.url();
  step("user page: " + userUrl);
  await shot(page, "11-user-edit");
  const userId = userUrl.split("/").pop();

  await page.goto(base + "/Roles/Edit");
  await page.fill("#Name", "Receptionist");
  await page.fill("#Description", "Front desk registration and OPD tokens");
  for (const key of ["Dashboard.View", "Patients.View", "Patients.Add", "Patients.Print", "Opd.View", "Opd.Add", "Opd.Print"])
    await page.check(`input[value='${key}']`);
  await shot(page, "12-responsibility-edit");
  await Promise.all([page.waitForNavigation(), page.click("button[type=submit]:has-text('Save responsibility')")]);
  await shot(page, "13-responsibilities");

  await page.goto(userUrl);
  await page.check("label.check-card:has-text('Receptionist') input");
  await Promise.all([page.waitForNavigation(), page.click("button:has-text('Save responsibilities')")]);
  await page.goto(`${base}/Users/Permissions/${userId}`);
  await page.selectOption("select[name='P[Emergency.View]']", "grant");
  await page.selectOption("select[name='P[Opd.Delete]']", "deny");
  await Promise.all([page.waitForNavigation(), page.click("button:has-text('Save permissions')")]);
  await shot(page, "14-permissions");
  await page.goto(base + "/Users");
  await shot(page, "15-users");
  await page.goto(base + "/Departments");
  await shot(page, "16-departments");

  // ---- Settings ----
  for (const [url, name] of [["/Settings", "17-settings-hospital"], ["/Settings/Preferences", "18-settings-preferences"], ["/Settings/Printing", "19-settings-printing"],
                             ["/Settings/HospitalType", "20-settings-type"], ["/Settings/Security", "21-settings-security"], ["/Settings/License", "22-license"]]) {
    await page.goto(base + url);
    await shot(page, name);
  }
  await page.goto(base + "/Settings/PrintPreview?doc=report");
  await shot(page, "23-print-report");
  await page.goto(base + "/Settings/PrintPreview?doc=receipt");
  await shot(page, "24-print-receipt");

  // ---- Backup & audit ----
  await page.goto(base + "/Backup");
  await page.fill("#Notes", "Before go-live");
  await Promise.all([page.waitForNavigation(), page.click("button:has-text('Create backup now')")]);
  await shot(page, "25-backup");
  const restoreLink = await page.$("a[href^='/Backup/Restore/']");
  if (restoreLink) { await Promise.all([page.waitForNavigation(), restoreLink.click()]); await shot(page, "26-restore-warning"); }
  await page.goto(base + "/Audit");
  await shot(page, "27-audit");
  await page.goto(base + "/Users/LoginHistory");
  await shot(page, "28-login-history");

  // ---- Second computer: receptionist ----
  const ctx2 = await browser.newContext({ viewport: { width: 1366, height: 800 } });
  const p2 = await ctx2.newPage();
  await p2.goto(base + "/Account/Login");
  await p2.fill("#Username", "sana.reception");
  await p2.fill("#Password", "Welcome#2026");
  await Promise.all([p2.waitForNavigation(), p2.click("button[type=submit]")]);
  await shot(p2, "29-dashboard-receptionist");
  await p2.goto(base + "/Users");
  step("receptionist /Users → " + p2.url());
  await shot(p2, "30-access-denied", false);

  // ---- Tablet ----
  const tab = await browser.newContext({ viewport: { width: 820, height: 1180 } });
  const p3 = await tab.newPage();
  await p3.goto(base + "/Account/Login");
  await shot(p3, "31-tablet-login", false);
  await p3.fill("#Username", "admin");
  await p3.fill("#Password", "Hospital#2026");
  await Promise.all([p3.waitForNavigation(), p3.click("button[type=submit]")]);
  await shot(p3, "32-tablet-dashboard");
  await p3.click("[data-sidebar-toggle]");
  await p3.waitForTimeout(500);
  await shot(p3, "33-tablet-menu", false);
} catch (e) {
  errors.push("SCRIPT FAILURE: " + (e && e.stack || e));
  try { await shot(page, "zz-failure"); } catch {}
} finally {
  fs.writeFileSync(`${out}/log.txt`, log.join("\n") + "\n\nERRORS:\n" + errors.join("\n"));
  await browser.close();
}
