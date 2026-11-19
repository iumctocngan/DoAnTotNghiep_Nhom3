$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/CmsEdu.Api/CmsEdu.Api.csproj'

$secureInput = Read-Host 'Paste your Gemini key here, or copy it and press Enter to use the clipboard' -AsSecureString
$keyInput = [System.Net.NetworkCredential]::new('', $secureInput).Password
if ([string]::IsNullOrWhiteSpace($keyInput)) {
    $keyInput = [string](Get-Clipboard -Raw)
}

$keys = @($keyInput -split '[,\r\n]' |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ })

if ($keys.Count -eq 0 -or ($keys | Where-Object { $_ -cnotmatch '^[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)*$' -or $_.Length -lt 20 })) {
    throw 'The input does not contain a complete Gemini API key. No settings were written.'
}

@{ 'AssessmentAI:ApiKey' = $keys -join ',' } |
    ConvertTo-Json -Compress |
    dotnet user-secrets set --project $project
if ($LASTEXITCODE -ne 0) {
    throw 'Could not save the Gemini API key.'
}
