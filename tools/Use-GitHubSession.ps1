param(
    [switch]$SaveCurrentLogin,
    [string[]]$GitArguments = @()
)
$ErrorActionPreference = 'Stop'
# Keep encrypted authentication outside the checkout in a stable user-profile
# location; packaged shells can redirect AppData into a temporary container.
$authFolder = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Documents\Codex\.private\ASUS Utility LITE\GitHubAuth'
$authFile = Join-Path $authFolder 'github.com.dpapi'
$scope = [Security.Cryptography.DataProtectionScope]::CurrentUser

if ($SaveCurrentLogin) {
    $loginToken = ((& gh auth token --hostname github.com) | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $loginToken) { throw 'Sign in with gh auth login before saving this Windows account session.' }
    $clearBytes = [Text.Encoding]::UTF8.GetBytes($loginToken)
    try {
        $encryptedBytes = [Security.Cryptography.ProtectedData]::Protect($clearBytes, $null, $scope)
        [IO.Directory]::CreateDirectory($authFolder) | Out-Null
        $temporaryFile = $authFile + '.tmp'
        [IO.File]::WriteAllBytes($temporaryFile, $encryptedBytes)
        if ([IO.File]::Exists($authFile)) { [IO.File]::Replace($temporaryFile, $authFile, $null) }
        else { [IO.File]::Move($temporaryFile, $authFile) }
    } finally {
        [Array]::Clear($clearBytes, 0, $clearBytes.Length)
        $loginToken = $null
    }
}

if (-not [IO.File]::Exists($authFile)) { throw 'No saved GitHub session. Sign in once, then run this script with -SaveCurrentLogin.' }
$clearBytes = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($authFile), $null, $scope)
try { $env:GH_TOKEN = [Text.Encoding]::UTF8.GetString($clearBytes) }
finally { [Array]::Clear($clearBytes, 0, $clearBytes.Length) }

if ($GitArguments.Count -gt 0) {
    # Pass authentication through an environment variable, never a command-line
    # token or a shell-based Git credential helper.
    $previousHeader = $env:ASUS_UTILITY_GIT_AUTH
    try {
        $env:ASUS_UTILITY_GIT_AUTH = 'AUTHORIZATION: basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('x-access-token:' + $env:GH_TOKEN))
        & git --config-env=http.https://github.com/.extraheader=ASUS_UTILITY_GIT_AUTH -c credential.helper= @GitArguments
        if ($LASTEXITCODE -ne 0) { throw 'GitHub Git operation failed.' }
    } finally { $env:ASUS_UTILITY_GIT_AUTH = $previousHeader }
} else { Write-Output 'GitHub session loaded for this Windows account.' }
