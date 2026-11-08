# Runs all tests with code coverage and writes the results to TestReport.pdf (next to this script).
# Usage, from the solution folder:
#   powershell -ExecutionPolicy Bypass -File TaskManager.Tests\run-tests.ps1 -Postgres   (PostgreSQL, needs Docker running)
#   powershell -ExecutionPolicy Bypass -File TaskManager.Tests\run-tests.ps1             (in-memory SQLite, no setup)
#
# -Postgres starts its own temporary container on port 5434 and removes it afterwards.
# It never touches the taskmanager-db container.

param([switch]$Postgres)

$project = $PSScriptRoot
$results = Join-Path $project 'TestResults'
$pdf = Join-Path $project 'TestReport.pdf'
$culture = [Globalization.CultureInfo]::InvariantCulture
$container = 'taskmanager-test-db'
$image = 'postgres:18'

function Percent([double]$part, [double]$whole) {
    if ($whole -eq 0) { return 'n/a' }
    return [string]::Format($culture, '{0:0.0}%', (100 * $part / $whole))
}

function Bar([double]$part, [double]$whole) {
    if ($whole -eq 0) { return '' }
    $width = [string]::Format($culture, '{0:0.0}', (100 * $part / $whole))
    return "<div class='bar'><div style='width:$width%'></div></div>"
}

function Html([string]$text) { return [System.Net.WebUtility]::HtmlEncode($text) }

if (Test-Path $results) { Remove-Item $results -Recurse -Force -Confirm:$false }

$database = 'SQLite (in-memory, tables built from the models)'
try {
    if ($Postgres) {
        docker rm -f $container 2>&1 | Out-Null
        docker run -d --rm --name $container -e POSTGRES_PASSWORD=test_only -p 127.0.0.1:5434:5432 $image 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not start the PostgreSQL test container. Is Docker running?' }

        $ready = $false
        for ($i = 0; $i -lt 60 -and -not $ready; $i++) {
            docker exec $container pg_isready -U postgres -h 127.0.0.1 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) { $ready = $true } else { Start-Sleep -Seconds 1 }
        }
        if (-not $ready) { throw 'The PostgreSQL test container did not become ready.' }

        $env:TASKMANAGER_TEST_POSTGRES = 'Host=127.0.0.1;Port=5434;Username=postgres;Password=test_only'
        $database = "PostgreSQL ($image), tables built by the project's migrations"
    }

    $output = & dotnet test $project '--collect:XPlat Code Coverage' --results-directory $results --logger 'trx;LogFileName=results.trx' 2>&1
}
finally {
    Remove-Item Env:\TASKMANAGER_TEST_POSTGRES -ErrorAction SilentlyContinue
    if ($Postgres) { docker stop $container 2>&1 | Out-Null }
}

$trxFile = Join-Path $results 'results.trx'
if (-not (Test-Path $trxFile)) {
    $output | ForEach-Object { "$_" }
    throw 'The tests did not produce a results file. See the output above.'
}

# Test results
[xml]$trx = Get-Content $trxFile -Raw -Encoding UTF8
$tests = @($trx.TestRun.Results.UnitTestResult | ForEach-Object {
    $parts = $_.testName.Split('.')
    $message = ''
    if ($_.outcome -ne 'Passed' -and $_.Output.ErrorInfo) { $message = $_.Output.ErrorInfo.Message }
    [pscustomobject]@{
        Group   = $parts[-2] -replace 'Tests$', ''
        Name    = $parts[-1] -replace '_', ' '
        Outcome = $_.outcome
        Message = $message
    }
})
$passed = @($tests | Where-Object { $_.Outcome -eq 'Passed' })
$failed = @($tests | Where-Object { $_.Outcome -ne 'Passed' })

# Coverage: merge every class of a file by line, because async methods compile into several classes
$coverageFile = Get-ChildItem $results -Recurse -Filter 'coverage.cobertura.xml' | Select-Object -First 1
if (-not $coverageFile) { throw 'The tests did not produce a coverage file.' }
[xml]$coverage = Get-Content $coverageFile.FullName -Raw -Encoding UTF8

$files = @{}
foreach ($class in $coverage.SelectNodes("//package[@name='TaskManager']/classes/class")) {
    $file = $class.GetAttribute('filename')
    if ($file -match '(^|[\\/])(Migrations|obj)[\\/]') { continue }
    if (-not $files.ContainsKey($file)) { $files[$file] = @{ Lines = @{}; Branches = @{} } }
    foreach ($line in $class.SelectNodes('lines/line')) {
        $number = [int]$line.GetAttribute('number')
        $hits = [int]$line.GetAttribute('hits')
        if (-not $files[$file].Lines.ContainsKey($number) -or $files[$file].Lines[$number] -lt $hits) {
            $files[$file].Lines[$number] = $hits
        }
        if ($line.GetAttribute('branch') -eq 'True' -and $line.GetAttribute('condition-coverage') -match '\((\d+)/(\d+)\)') {
            $covered = [int]$Matches[1]
            $total = [int]$Matches[2]
            if (-not $files[$file].Branches.ContainsKey($number) -or $files[$file].Branches[$number][0] -lt $covered) {
                $files[$file].Branches[$number] = @($covered, $total)
            }
        }
    }
}

$rows = @($files.Keys | ForEach-Object {
    $entry = $files[$_]
    $parent = Split-Path -Parent $_
    $folder = if ($parent) { Split-Path -Leaf $parent } else { '' }
    if (-not $folder -or $folder -eq 'TaskManager') { $folder = '(root)' }
    [pscustomobject]@{
        Folder          = $folder
        File            = Split-Path -Leaf $_
        Lines           = $entry.Lines.Count
        LinesCovered    = @($entry.Lines.Values | Where-Object { $_ -gt 0 }).Count
        Branches        = [int](($entry.Branches.Values | ForEach-Object { $_[1] } | Measure-Object -Sum).Sum)
        BranchesCovered = [int](($entry.Branches.Values | ForEach-Object { $_[0] } | Measure-Object -Sum).Sum)
    }
})

$totalLines = ($rows | Measure-Object Lines -Sum).Sum
$totalLinesCovered = ($rows | Measure-Object LinesCovered -Sum).Sum
$totalBranches = ($rows | Measure-Object Branches -Sum).Sum
$totalBranchesCovered = ($rows | Measure-Object BranchesCovered -Sum).Sum

# Report
$css = @'
@page { size: A4; margin: 16mm 14mm; }
* { box-sizing: border-box; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
body { font-family: "Segoe UI", Arial, sans-serif; color: #1f2933; font-size: 10.5pt; line-height: 1.4; margin: 0; }
h1 { font-size: 22pt; margin: 0 0 2px; }
h2 { font-size: 13.5pt; margin: 22px 0 8px; padding-bottom: 4px; border-bottom: 2px solid #d9e2ec; }
h3 { font-size: 11pt; margin: 16px 0 6px; }
.meta { color: #52606d; font-size: 9.5pt; }
.cards { display: flex; gap: 10px; margin-top: 16px; }
.card { flex: 1; border: 1px solid #d9e2ec; border-radius: 6px; padding: 12px 14px; }
.card .label { color: #52606d; font-size: 9pt; }
.card .value { font-size: 23pt; font-weight: 600; line-height: 1.25; }
.card .detail { color: #52606d; font-size: 9pt; }
.good { color: #1a7f4b; }
.bad { color: #c0392b; }
.note { color: #52606d; font-size: 9pt; margin-top: 10px; }
table { width: 100%; border-collapse: collapse; }
th { text-align: left; font-size: 9pt; color: #52606d; font-weight: 600; border-bottom: 1px solid #bcccdc; padding: 4px 8px 4px 0; }
td { padding: 3.5px 8px 3.5px 0; border-bottom: 1px solid #edf1f5; vertical-align: middle; }
tr { break-inside: avoid; }
h2, h3 { break-after: avoid; }
.num { text-align: right; white-space: nowrap; font-variant-numeric: tabular-nums; }
th.num { text-align: right; }
.status { width: 56px; font-weight: 600; font-size: 9pt; }
.bar { width: 100px; height: 7px; background: #e4e9ef; border-radius: 4px; overflow: hidden; }
.bar div { height: 100%; background: #2f7ed8; }
.fail { margin: 0 0 8px; padding: 8px 10px; border-left: 3px solid #c0392b; background: #fdf3f2; break-inside: avoid; }
.fail small { display: block; color: #52606d; word-break: break-word; }
'@

$passClass = if ($failed.Count -eq 0) { 'good' } else { 'bad' }
$html = New-Object System.Text.StringBuilder
function Add([string]$text) { [void]$html.AppendLine($text) }

Add "<!doctype html><html lang='en'><head><meta charset='utf-8'><title>TaskManager Test Report</title><style>$css</style></head><body>"
Add "<h1>TaskManager Test Report</h1>"
Add "<div class='meta'>$((Get-Date).ToString('dd/MM/yyyy HH:mm', $culture)) &middot; Database: $(Html $database)</div>"
Add "<div class='cards'>"
Add "<div class='card'><div class='label'>Tests passing</div><div class='value $passClass'>$(Percent $passed.Count $tests.Count)</div><div class='detail'>$($passed.Count) of $($tests.Count) tests</div></div>"
Add "<div class='card'><div class='label'>Code tested (lines)</div><div class='value'>$(Percent $totalLinesCovered $totalLines)</div><div class='detail'>$totalLinesCovered of $totalLines lines</div></div>"
Add "<div class='card'><div class='label'>Code tested (branches)</div><div class='value'>$(Percent $totalBranchesCovered $totalBranches)</div><div class='detail'>$totalBranchesCovered of $totalBranches branches</div></div>"
Add "</div>"
Add "<p class='note'>Code tested counts the application code the tests actually ran. Branches are the separate paths through conditions such as if and ??. EF Core migrations are generated code and are left out. The tests call the real API inside the test process.</p>"

if ($failed.Count -gt 0) {
    Add "<h2>Failing tests ($($failed.Count))</h2>"
    foreach ($test in $failed) {
        $message = ($test.Message -replace '\s+', ' ').Trim()
        if ($message.Length -gt 400) { $message = $message.Substring(0, 400) + '...' }
        Add "<div class='fail'><strong>$(Html $test.Group): $(Html $test.Name)</strong><small>$(Html $message)</small></div>"
    }
}

Add "<h2>Code tested by folder</h2>"
Add "<table><tr><th>Folder</th><th class='num'>Lines</th><th class='num'>%</th><th></th><th class='num'>Branches</th><th class='num'>%</th></tr>"
foreach ($group in ($rows | Group-Object Folder | Sort-Object Name)) {
    $l = ($group.Group | Measure-Object Lines -Sum).Sum
    $lc = ($group.Group | Measure-Object LinesCovered -Sum).Sum
    $b = ($group.Group | Measure-Object Branches -Sum).Sum
    $bc = ($group.Group | Measure-Object BranchesCovered -Sum).Sum
    Add "<tr><td>$(Html $group.Name)</td><td class='num'>$lc / $l</td><td class='num'>$(Percent $lc $l)</td><td>$(Bar $lc $l)</td><td class='num'>$bc / $b</td><td class='num'>$(Percent $bc $b)</td></tr>"
}
Add "</table>"

$partial = @($rows | Where-Object { $_.LinesCovered -lt $_.Lines } | Sort-Object { $_.LinesCovered / $_.Lines }, File)
if ($partial.Count -gt 0) {
    Add "<h2>Files not fully tested</h2>"
    Add "<table><tr><th>File</th><th class='num'>Lines</th><th class='num'>%</th><th></th></tr>"
    foreach ($row in $partial) {
        Add "<tr><td>$(Html "$($row.Folder)/$($row.File)")</td><td class='num'>$($row.LinesCovered) / $($row.Lines)</td><td class='num'>$(Percent $row.LinesCovered $row.Lines)</td><td>$(Bar $row.LinesCovered $row.Lines)</td></tr>"
    }
    Add "</table>"
}

Add "<h2>All tests</h2>"
foreach ($group in ($tests | Group-Object Group | Sort-Object Name)) {
    $groupPassed = @($group.Group | Where-Object { $_.Outcome -eq 'Passed' }).Count
    Add "<h3>$(Html $group.Name) &middot; $groupPassed of $($group.Count) passing</h3>"
    Add "<table>"
    foreach ($test in ($group.Group | Sort-Object Name)) {
        $status = if ($test.Outcome -eq 'Passed') { "<span class='good'>PASS</span>" } else { "<span class='bad'>FAIL</span>" }
        Add "<tr><td>$(Html $test.Name)</td><td class='status num'>$status</td></tr>"
    }
    Add "</table>"
}
Add "</body></html>"

$htmlFile = Join-Path $results 'TestReport.html'
[IO.File]::WriteAllText($htmlFile, $html.ToString(), (New-Object Text.UTF8Encoding($false)))

# Print the page to PDF with Microsoft Edge, which ships with Windows
$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "Microsoft Edge was not found. The report is at $htmlFile" }

if (Test-Path $pdf) { Remove-Item $pdf -Force -Confirm:$false }
$profileDir = Join-Path $results 'edge-profile'
$url = ([Uri]$htmlFile).AbsoluteUri
$arguments = @('--headless=new', '--disable-gpu', '--no-pdf-header-footer', "--user-data-dir=`"$profileDir`"", "--print-to-pdf=`"$pdf`"", $url)
Start-Process -FilePath $edge -ArgumentList $arguments -Wait -WindowStyle Hidden
for ($i = 0; $i -lt 30 -and -not (Test-Path $pdf); $i++) { Start-Sleep -Seconds 1 }
if (-not (Test-Path $pdf)) { throw "Edge did not create the PDF. The report is at $htmlFile" }

"Database: $database"
"Tests passing: $($passed.Count) of $($tests.Count) ($(Percent $passed.Count $tests.Count))"
"Code tested (lines): $(Percent $totalLinesCovered $totalLines)"
"Code tested (branches): $(Percent $totalBranchesCovered $totalBranches)"
"Report: $pdf"
