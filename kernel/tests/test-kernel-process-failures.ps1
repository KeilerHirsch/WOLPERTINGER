$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $root 'kernel\bin\wolpertinger_kernel_main.exe'
$fixtures = Join-Path $root 'fixtures\contracts\v1'
if (-not (Test-Path $exe)) { throw "kernel executable not found: $exe" }

function New-Kernel {
    $psi = [Diagnostics.ProcessStartInfo]::new($exe)
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    return [Diagnostics.Process]::Start($psi)
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
    $header = Read-Exact $stream 4
    $length = (($header[0] -shl 24) -bor ($header[1] -shl 16) -bor
               ($header[2] -shl 8) -bor $header[3])
    if ($length -lt 1 -or $length -gt 65536) { throw "invalid frame length $length" }
    return Read-Exact $stream $length
}

function Assert-Bytes([byte[]]$expected, [byte[]]$actual, [string]$label) {
    if ($expected.Length -ne $actual.Length) {
        throw "$label length expected $($expected.Length), actual $($actual.Length)"
    }
    for ($i = 0; $i -lt $expected.Length; $i++) {
        if ($expected[$i] -ne $actual[$i]) { throw "$label byte $i mismatch" }
    }
}

function Hex-Bytes([string]$hex) {
    $hex = $hex.Trim()
    $bytes = [byte[]]::new($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16)
    }
    return $bytes
}
function Assert-FramingFailure([byte[]]$inputBytes, [string]$label) {
    $p = New-Kernel
    try {
        $input = $p.StandardInput.BaseStream
        if ($inputBytes.Length -gt 0) {
            $input.Write($inputBytes, 0, $inputBytes.Length)
            $input.Flush()
        }
        $input.Close()
        if (-not $p.WaitForExit(5000)) { throw "${label}: kernel did not exit" }
        if ($p.ExitCode -eq 0) { throw "${label}: expected non-zero exit" }
        $out = [IO.MemoryStream]::new()
        $p.StandardOutput.BaseStream.CopyTo($out)
        if ($out.Length -ne 0) { throw "${label}: protocol stdout must stay empty" }
    }
    finally {
        if (-not $p.HasExited) { $p.Kill($true) }
        $p.Dispose()
    }
}

function Assert-CleanEof {
    $p = New-Kernel
    try {
        $p.StandardInput.Close()
        if (-not $p.WaitForExit(5000)) { throw 'clean EOF: kernel did not exit' }
        if ($p.ExitCode -ne 0) { throw "clean EOF: exit code $($p.ExitCode)" }
        if (-not $p.StandardOutput.EndOfStream) { throw 'clean EOF: unexpected stdout' }
    }
    finally {
        if (-not $p.HasExited) { $p.Kill($true) }
        $p.Dispose()
    }
}
function Assert-InvalidMessageResponse {
    $expectedHex = Get-Content (Join-Path $fixtures 'response-invalid-message-shadow.hex') -Raw
    $expected = Hex-Bytes $expectedHex
    $p = New-Kernel
    try {
        $input = $p.StandardInput.BaseStream
        [byte[]]$frame = @(0, 0, 0, 1, 255)
        $input.Write($frame, 0, $frame.Length)
        $input.Flush()
        Assert-Bytes $expected (Read-Frame $p.StandardOutput.BaseStream) 'invalid-message response'
        $input.Close()
        if (-not $p.WaitForExit(5000)) { throw 'invalid message: kernel did not exit after EOF' }
        if ($p.ExitCode -ne 0) { throw "invalid message: exit code $($p.ExitCode)" }
    }
    finally {
        if (-not $p.HasExited) { $p.Kill($true) }
        $p.Dispose()
    }
}

Assert-CleanEof
Assert-FramingFailure ([byte[]]@(0, 0, 1)) 'truncated header'
Assert-FramingFailure ([byte[]]@(0, 0, 0, 0)) 'zero length'
Assert-FramingFailure ([byte[]]@(0, 1, 0, 1)) 'oversize length'
Assert-FramingFailure ([byte[]]@(0, 0, 0, 4, 161)) 'truncated payload'
Assert-InvalidMessageResponse
Write-Host 'PASS: kernel process failure-mode contract'
