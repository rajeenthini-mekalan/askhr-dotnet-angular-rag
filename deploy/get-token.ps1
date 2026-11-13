<#
.SYNOPSIS
  DEV ONLY. Fetches an access token for an AskHR demo user from the local Keycloak,
  using the askhr-devtools client (password grant). Passwords are read from deploy/.env.

.EXAMPLE
  $t = ./deploy/get-token.ps1 admin
  curl.exe -H "Authorization: Bearer $t" https://localhost:7124/api/documents -k

.EXAMPLE
  ./deploy/get-token.ps1 employee -Decode   # print the token's claims instead of the token

.EXAMPLE
  ./deploy/get-token.ps1 admin -SaveForHttp
  # Writes ASKHR_TOKEN=... to backend/src/AskHR.Api/.env (git-ignored), which AskHR.Api.http reads with {{$dotenv ASKHR_TOKEN}}.
  # The token never has to be pasted into a tracked file.
#>
param(
    [Parameter(Mandatory)][ValidateSet('admin', 'employee')][string]$User,
    [switch]$Decode,
    [switch]$SaveForHttp,
    [string]$Keycloak = 'http://localhost:8080'
)

$envFile = Join-Path $PSScriptRoot '.env'
if (-not (Test-Path $envFile)) { throw "deploy/.env not found. Copy deploy/.env.example to deploy/.env first." }
$vars = @{}
Get-Content $envFile | Where-Object { $_ -match '^\s*([A-Z0-9_]+)\s*=\s*(.*)$' } | ForEach-Object { $vars[$Matches[1]] = $Matches[2].Trim() }

$username = "$User@askhr.demo"
$password = if ($User -eq 'admin') { $vars['ASKHR_DEMO_ADMIN_PASSWORD'] } else { $vars['ASKHR_DEMO_EMPLOYEE_PASSWORD'] }

$response = Invoke-RestMethod -Method Post -Uri "$Keycloak/realms/askhr/protocol/openid-connect/token" -Body @{
    grant_type = 'password'
    client_id  = 'askhr-devtools'
    scope      = 'openid'
    username   = $username
    password   = $password
}

if ($SaveForHttp) {
    $httpEnv = [IO.Path]::Combine($PSScriptRoot, '..', 'backend', 'src', 'AskHR.Api', '.env')
    # Same folder as AskHR.Api.http. Ignored by the ".env" rule in .gitignore, so it can never be committed.
    Set-Content -Path $httpEnv -Value "ASKHR_TOKEN=$($response.access_token)" -Encoding ascii -NoNewline -ErrorAction Stop
    $expires = (Get-Date).AddSeconds($response.expires_in).ToString('HH:mm:ss')
    Write-Host "Saved the $User token for AskHR.Api.http (valid until $expires). Send the request again."
    return
}

if (-not $Decode) { return $response.access_token }

# A JWT is header.payload.signature, each base64url-encoded. Decode the payload only (no validation).
$payload = $response.access_token.Split('.')[1].Replace('-', '+').Replace('_', '/')
$payload += '=' * ((4 - $payload.Length % 4) % 4)
[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json |
    Select-Object iss, aud, azp, preferred_username, roles, exp | Format-List
