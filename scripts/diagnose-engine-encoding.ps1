param(
    [string]$EnginePath = ".\UMMInstallerEngine.exe",
    [string]$PayloadPath = ""
)

$ErrorActionPreference = "Stop"
function Select-DiagnosticFile([string]$title, [string]$filter, [string]$directory) {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    try {
        $dialog.Title = $title
        $dialog.Filter = $filter
        if ($directory) { $dialog.InitialDirectory = $directory }
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
            throw "File selection cancelled."
        }
        return $dialog.FileName
    }
    finally { $dialog.Dispose() }
}
if (-not (Test-Path -LiteralPath $EnginePath -PathType Leaf)) {
    $EnginePath = Select-DiagnosticFile "Select UMMInstallerEngine.exe in the installed app folder" "Installation engine (UMMInstallerEngine.exe)|UMMInstallerEngine.exe" ""
}
$engine = (Resolve-Path -LiteralPath $EnginePath).Path
$expectedPayload = Join-Path (Split-Path -Parent $engine) "UMMPayload"
$payload = $expectedPayload
if ($PayloadPath) { $payload = (Resolve-Path -LiteralPath $PayloadPath).Path }
if (-not (Test-Path -LiteralPath (Join-Path $payload "UnityModManager.dll"))) {
    Write-Output "No DLL at expected location: $payload"
    $dll = Select-DiagnosticFile "Select UnityModManager.dll in the installed app's UMMPayload folder" "Unity Mod Manager (UnityModManager.dll)|UnityModManager.dll" (Split-Path -Parent $engine)
    $payload = Split-Path -Parent $dll
}
$payload = (Resolve-Path -LiteralPath $payload).Path
Write-Output "Engine path: $engine"
Write-Output "Expected payload path: $expectedPayload"
Write-Output "Selected payload path: $payload"
if ($payload -ne $expectedPayload) {
    Write-Output "LAYOUT DIFFERENCE: DLLs are not at the expected path next to this engine. This test uses the selected folder directly."
}
if ($payload -notmatch '[^\x00-\x7F]') {
    Write-Output "ASCII PATH: this payload path contains no non-ASCII characters, so a Korean username alone does not explain corruption of this path."
}

# The requests have identical JSON values. Only the wire representation differs:
# literal UTF-8, as sent by Rust, versus ASCII-only JSON Unicode escapes.
$literal = @{ action = "status"; protocolVersion = 1; payloadDir = $payload } | ConvertTo-Json -Compress
$escaped = [regex]::Replace($literal, '[^\x00-\x7F]', {
    param($match)
    '\u{0:x4}' -f [int][char]$match.Value
})

function Invoke-Status([string]$json) {
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $engine
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        $process.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
        $process.StandardInput.Close()
        if (-not $process.WaitForExit(20000)) {
            $process.Kill()
            throw "Engine status request timed out."
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errors = $stderr.GetAwaiter().GetResult()
        if (-not $output) { throw "No engine response. $errors" }
        return ($output | ConvertFrom-Json)
    }
    finally {
        $process.Dispose()
    }
}

$utf8 = Invoke-Status $literal
$ascii = Invoke-Status $escaped
Write-Output "Payload path: $payload"
Write-Output ("Literal UTF-8: detected={0}, bundledVersion={1}, error={2}" -f $utf8.detected, $utf8.bundledVersion, $utf8.error)
Write-Output ("ASCII escapes: detected={0}, bundledVersion={1}, error={2}" -f $ascii.detected, $ascii.bundledVersion, $ascii.error)
if (-not $utf8.bundledVersion -and $ascii.bundledVersion) {
    Write-Output "CONFIRMED: equivalent ASCII-escaped JSON finds the bundled DLL, but literal UTF-8 does not. The engine input decoding corrupts the path."
}
elseif ($utf8.bundledVersion -and $utf8.bundledVersion -eq $ascii.bundledVersion) {
    Write-Output "NOT REPRODUCED: both requests find the same bundled DLL. Input encoding does not explain this run."
}
else {
    Write-Output "INCONCLUSIVE: compare the responses above. The engine must detect the game and report bundledVersion for this test."
}
