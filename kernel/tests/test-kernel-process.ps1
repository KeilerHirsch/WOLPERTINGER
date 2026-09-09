$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $root 'kernel\bin\wolpertinger_kernel_main.exe'
$fixtures = Join-Path $root 'fixtures\contracts\v1'
if (-not (Test-Path $exe)) { throw "kernel executable not found: $exe" }

function Hex-Bytes([string]$hex) {
    $hex = $hex.Trim()
    $bytes = [byte[]]::new($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16)
    }
    return $bytes
}

function Read-Fixture([string]$name) {
    return Hex-Bytes (Get-Content (Join-Path $fixtures $name) -Raw)
}

function Write-Frame($stream, [byte[]]$payload) {
    $len = $payload.Length
    $header = [byte[]]@([byte](($len -shr 24) -band 255), [byte](($len -shr 16) -band 255),
                       [byte](($len -shr 8) -band 255), [byte]($len -band 255))
    $stream.Write($header, 0, 4)
    $stream.Write($payload, 0, $payload.Length)
    $stream.Flush()
}
function Read-Exact($stream, [int]$count) {
    $buffer = [byte[]]::new($count)
    $offset = 0
    while ($offset -lt $count) {
        $n = $stream.Read($buffer, $offset, $count - $offset)
        if ($n -eq 0) { throw "unexpected EOF after $offset of $count bytes" }
        $offset += $n
    }
    return $buffer
}

function Read-Frame($stream) {
    $h = Read-Exact $stream 4
    $len = (($h[0] -shl 24) -bor ($h[1] -shl 16) -bor ($h[2] -shl 8) -bor $h[3])
    if ($len -lt 1 -or $len -gt 65536) { throw "invalid response length $len" }
    return Read-Exact $stream $len
}

function Assert-Bytes([byte[]]$expected, [byte[]]$actual, [string]$label) {
    if ($expected.Length -ne $actual.Length) {
        throw "$label length expected $($expected.Length), actual $($actual.Length)"
    }
    for ($i = 0; $i -lt $expected.Length; $i++) {
        if ($expected[$i] -ne $actual[$i]) { throw "$label byte $i mismatch" }
    }
}
$psi = [Diagnostics.ProcessStartInfo]::new($exe)
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$p = [Diagnostics.Process]::Start($psi)
try {
    $input = $p.StandardInput.BaseStream
    $output = $p.StandardOutput.BaseStream
    Write-Frame $input (Hex-Bytes 'a3000101010201')
    Assert-Bytes (Read-Fixture 'response-role-accepted.hex') (Read-Frame $output) 'SetRole response'
    Write-Frame $input (Read-Fixture 'session-bound.hex')
    Assert-Bytes (Read-Fixture 'response-session-bound-applied.hex') (Read-Frame $output) 'SessionBound response'
    Write-Frame $input (Read-Fixture 'fsdjump.hex')
    Assert-Bytes (Read-Fixture 'response-fsdjump-applied.hex') (Read-Frame $output) 'FSDJump response'
    $input.Close()
    if (-not $p.WaitForExit(5000)) { throw 'kernel did not exit after stdin EOF' }
    if ($p.ExitCode -ne 0) { throw "kernel exit code $($p.ExitCode): $($p.StandardError.ReadToEnd())" }
    Write-Host 'PASS: framed kernel process SetRole -> SessionBound -> FSDJump'
}
finally {
    if (-not $p.HasExited) { $p.Kill($true) }
    $p.Dispose()
}
