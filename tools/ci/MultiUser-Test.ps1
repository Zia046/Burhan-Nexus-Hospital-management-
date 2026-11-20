<#
  CI: two "hospital computers" work at the same time on ONE central HMS server (installed by Server Setup.exe).
  Completes the setup wizard, logs in from two separate sessions (Reception-PC and Lab-PC), registers patients
  from both computers in parallel and checks that every patient is in the one central database exactly once and
  visible from the other computer. Requires PowerShell 7.
#>
param([string]$Server = "http://localhost:5000", [string]$Psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe", [string]$PgPassword = "BCGH", [int]$PerComputer = 10)
$ErrorActionPreference = "Stop"

function Token([string]$html) {
    if ($html -match 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"') { return $Matches[1] }
    if ($html -match 'name="__RequestVerificationToken"[^>]*value="([^"]+)"') { return $Matches[1] }
    throw "no anti-forgery token in page"
}
function PostForm($session, [string]$getUrl, [string]$postUrl, [hashtable]$fields) {
    $page = Invoke-WebRequest -Uri "$Server$getUrl" -WebSession $session -SkipHttpErrorCheck
    $fields["__RequestVerificationToken"] = Token $page.Content
    # Redirects are followed; success = the page we end up on (a failed form shows the same form again).
    return Invoke-WebRequest -Uri "$Server$postUrl" -Method Post -Body $fields -WebSession $session -SkipHttpErrorCheck
}
function Landed($response) { return $response.BaseResponse.RequestMessage.RequestUri.AbsolutePath }

# 1. Setup wizard (fresh server)
$setup = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$r = PostForm $setup "/Setup" "/Setup" @{
    HospitalName = "Burhan LAN Test Hospital"; HospitalType = "Private"; RegistrationNumber = "REG-LAN"; Address = "Main Road"; City = "Daggar"; Province = "Khyber Pakhtunkhwa"
    ContactNumber = "0939-123456"; Email = "info@lan-test.pk"; Website = ""; AdminFullName = "System Admin"; AdminUsername = "admin"
    AdminPassword = "Hospital#2026"; AdminConfirmPassword = "Hospital#2026"; CurrencyCode = "PKR"; TimeZoneId = "Asia/Karachi"
    DateFormat = "dd/MM/yyyy"; TimeFormat = "hh:mm tt"; DefaultLanguage = "en"; ReceiptPaperSize = "80mm"; ReportPaperSize = "A4"
}
"setup wizard: $($r.StatusCode) -> $(Landed $r)"
if ((Landed $r) -ne "/Account/Login") { throw "setup wizard failed: $($r.Content.Substring(0, [Math]::Min(2000, $r.Content.Length)))" }

# 2. Two computers log in at the same time (separate sessions = separate computers)
$sessions = @{}
foreach ($pc in "Reception-PC", "Lab-PC") {
    $s = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $l = PostForm $s "/Account/Login" "/Account/Login" @{ Username = "admin"; Password = "Hospital#2026"; Workstation = $pc }
    "login from ${pc}: $($l.StatusCode) -> $(Landed $l)"
    if ((Landed $l) -ne "/") { throw "login failed on $pc" }
    $sessions[$pc] = $s
}

# 3. Both computers register patients in parallel
$results = $sessions.GetEnumerator() | ForEach-Object -ThrottleLimit 2 -Parallel {
    $pc = $_.Key; $s = $_.Value; $server = $using:Server; $n = $using:PerComputer
    $page = Invoke-WebRequest -Uri "$server/Patients/Register" -WebSession $s
    if ($page.Content -notmatch 'name="__RequestVerificationToken"[^>]*value="([^"]+)"') { throw "no token" }
    $token = $Matches[1]
    1..$n | ForEach-Object {
        $name = "LAN $($pc.Split('-')[0]) Patient $_"
        $res = Invoke-WebRequest -Uri "$server/Patients/Register" -Method Post -WebSession $s -SkipHttpErrorCheck -Body @{
            "__RequestVerificationToken" = $token; "Patient.FullName" = $name; "Patient.GuardianRelation" = "SonOf"; "Patient.GuardianName" = "Guardian $pc $_"
            "Patient.Gender" = "Male"; "Patient.Age" = "30"; "Patient.AgeUnit" = "Years"; "Patient.CityOrVillage" = "Daggar"; "ConfirmNotDuplicate" = "true"; "Next" = "profile"
        }
        $landed = $res.BaseResponse.RequestMessage.RequestUri.AbsolutePath
        [pscustomobject]@{ Computer = $pc; Name = $name; Saved = ($landed -match '^/Patients/(Profile/)?\d+$'); Landed = $landed }
    }
}
$failed = $results | Where-Object { -not $_.Saved }
"registrations: $($results.Count) sent, $($results.Count - @($failed).Count) saved (redirect to the patient profile)"
if ($failed) { $failed | Format-Table | Out-String; throw "some registrations failed" }

# 4. One central database: every patient exactly once, unique Patient IDs
$env:PGPASSWORD = $PgPassword
$count = & $Psql -h localhost -U postgres -d burhan_nexus_hms -tAc "select count(*), count(distinct ""PatientNumber"") from ""Patients"" where ""FullName"" like 'LAN %'"
"central database: patients, distinct Patient IDs = $count"
$expected = 2 * $PerComputer
if ($count.Trim() -ne "$expected|$expected") { throw "expected $expected patients with $expected distinct IDs in the central database" }

# 5. Each computer sees the other computer's patients
foreach ($pair in @(@("Reception-PC", "Lab"), @("Lab-PC", "Reception"))) {
    $list = Invoke-WebRequest -Uri "$Server/Patients?q=LAN+$($pair[1])" -WebSession $sessions[$pair[0]]
    $seen = ([regex]::Matches($list.Content, "LAN $($pair[1]) Patient \d+")).Value | Sort-Object -Unique
    "$($pair[0]) sees $($seen.Count) patients registered on the $($pair[1]) computer"
    if ($seen.Count -lt 1) { throw "$($pair[0]) cannot see patients of the other computer" }
}
"MULTI-USER LAN TEST: PASSED"
