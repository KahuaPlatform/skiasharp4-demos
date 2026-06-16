[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputDir
)

# Publishes the launcher and every game into a single static site layout:
#
#   publish/site/                  ← launcher (the catalog landing page)
#   publish/site/games/pohaku/     ← Pohaku  wasm app
#   publish/site/games/hokulele/   ← HokuLele wasm app
#   publish/site/games/lua/        ← Lua     wasm app
#   ... etc
#
# Drop publish/site/ onto any static host (Azure Static Web Apps, GitHub Pages,
# Netlify, S3+CloudFront, or plain `dotnet serve`/`python -m http.server`) and
# the launcher's tile clicks navigate to /games/<slug>/.
#
# Uno's wasm bootstrap bakes site-root-absolute paths ("/package_<hash>/...",
# "/_framework/...", "/manifest.webmanifest", etc.) into index.html, uno-config.js,
# service-worker.js, and manifest.webmanifest. Those 404 when the game is served
# from /games/<slug>/, so after each game is published we walk its tree and
# rewrite any "/<path>" → "./<path>" inside string literals. The launcher itself
# sits at the site root and needs no rewriting.

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot 'publish\site' }

$games = @(
    @{ Name = 'Pohaku';   Slug = 'pohaku'   }
    @{ Name = 'HokuLele'; Slug = 'hokulele' }
    @{ Name = 'Lua';      Slug = 'lua'      }
    @{ Name = 'Mahina';   Slug = 'mahina'   }
    @{ Name = 'Heiau';    Slug = 'heiau'    }
    @{ Name = 'Kanapi';   Slug = 'kanapi'   }
    @{ Name = 'Alaloa';   Slug = 'alaloa'   }
    @{ Name = 'Hahai';    Slug = 'hahai'    }
    @{ Name = 'Paku';     Slug = 'paku'     }
    @{ Name = 'Kiai';     Slug = 'kiai'     }
    @{ Name = 'Koa';      Slug = 'koa'      }
)

if (Test-Path $OutputDir) {
    Write-Host "Cleaning $OutputDir" -ForegroundColor DarkYellow
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

function Publish-WasmApp {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Dest
    )
    $projDir = Split-Path -Parent $Project
    # Wipe the prior publish output first. Re-publishing into a dirty folder can
    # leave stale, differently-hashed assets behind and desync the boot manifest:
    # uno-config.js ends up pointing at an old dotnet.<hash>.js, which boots the
    # PREVIOUS app assembly (e.g. an old theme default) even though index.html and
    # the new wasm are present. A clean publish folder guarantees one coherent set.
    $pubDir = Join-Path $projDir "bin\$Configuration\net10.0-browserwasm\publish"
    if (Test-Path $pubDir) { Remove-Item $pubDir -Recurse -Force }

    dotnet publish $Project -c $Configuration -f net10.0-browserwasm --nologo
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $Project" }

    $wwwroot = Join-Path $projDir "bin\$Configuration\net10.0-browserwasm\publish\wwwroot"
    if (-not (Test-Path $wwwroot)) {
        # Some Uno SDK versions emit the static assets at the build-output root
        # rather than under a publish/ folder.
        $wwwroot = Join-Path $projDir "bin\$Configuration\net10.0-browserwasm\wwwroot"
    }
    if (-not (Test-Path $wwwroot)) { throw "wwwroot not found under $projDir (looked in publish/wwwroot and wwwroot)" }

    New-Item -ItemType Directory -Force -Path $Dest | Out-Null
    Copy-Item -Path (Join-Path $wwwroot '*') -Destination $Dest -Recurse -Force
}

function Convert-RootAbsolutePathsInFile {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path $Path)) { return }
    # Read raw bytes so we can preserve a UTF-8 BOM exactly (Uno's service-worker.js
    # ships with one). Skip the BOM bytes when decoding so they don't end up inside
    # the string and double-encode on write.
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = if ($hasBom) {
        [System.Text.Encoding]::UTF8.GetString($bytes, 3, $bytes.Length - 3)
    } else {
        [System.Text.Encoding]::UTF8.GetString($bytes)
    }
    # Three transforms, all targeting quoted string literals to stay safe:
    #   "/foo..."  → "./foo..."   (most app-local paths — _framework, package_, Assets, ...)
    #   "/"        → "./"         (root entry in offline_files / scope in manifest)
    #   "/."       → "./."        (current-dir sentinel sometimes added by Uno)
    $text = $text -replace '(["''])/([\w])', '$1./$2'
    $text = $text -replace '(["''])/(["''])', '$1./$2'
    $text = $text -replace '(["''])/(\.)', '$1./$2'
    $utf8 = New-Object System.Text.UTF8Encoding($hasBom)
    [System.IO.File]::WriteAllBytes($Path, $utf8.GetPreamble() + $utf8.GetBytes($text))
}

function Convert-GameToSubfolderRelative {
    param([Parameter(Mandatory)][string]$GameDir)
    # Files Uno emits with site-root-absolute paths. Each is rewritten in place.
    $targets = @(
        (Join-Path $GameDir 'index.html'),
        (Join-Path $GameDir 'service-worker.js'),
        (Join-Path $GameDir 'manifest.webmanifest')
    )
    foreach ($pkgConfig in Get-ChildItem -Path $GameDir -Filter 'uno-config.js' -Recurse -File -ErrorAction SilentlyContinue) {
        $targets += $pkgConfig.FullName
    }
    foreach ($t in $targets) { Convert-RootAbsolutePathsInFile -Path $t }
}

function Repair-RootWebConfig {
    # The Uno wasm publish emits a web.config that breaks on a stock IIS host:
    #   1. It adds a ".json" mimeMap without a preceding <remove>, but IIS already
    #      defines .json (and .wasm) at the server level -> "duplicate collection
    #      entry" -> HTTP 500.19 on *every* route. We insert the missing <remove>.
    #   2. ".webmanifest" has no server-level MIME mapping, so the PWA manifest 404s.
    #      We add it.
    # Only the site-root (launcher) web.config is kept; per-game ones are deleted
    # in the loop below (they'd nest under this one and collide).
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path $Path)) { return }
    $bytes  = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text   = if ($hasBom) { [System.Text.Encoding]::UTF8.GetString($bytes,3,$bytes.Length-3) } else { [System.Text.Encoding]::UTF8.GetString($bytes) }
    $jsonMap = '<mimeMap fileExtension=".json" mimeType="application/octet-stream" />'
    if ($text -notmatch '<remove fileExtension="\.json"') {
        $text = $text.Replace($jsonMap, "<remove fileExtension=`".json`" />`r`n      $jsonMap")
    }
    if ($text -notmatch 'fileExtension="\.webmanifest"') {
        $text = $text.Replace($jsonMap, "$jsonMap`r`n      <mimeMap fileExtension=`".webmanifest`" mimeType=`"application/manifest+json`" />")
    }
    $enc = New-Object System.Text.UTF8Encoding($hasBom)
    [System.IO.File]::WriteAllBytes($Path, $enc.GetPreamble() + $enc.GetBytes($text))
}

function Add-SwSkipWaiting {
    # The generated service worker calls clients.claim() on activate but never
    # skipWaiting(), so a freshly deployed SW sits in "waiting" and returning
    # browsers keep being served the previous build until every tab is closed.
    # Injecting skipWaiting() lets the new SW activate on the next reload, so
    # deploys propagate without users having to manually clear site data.
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path $Path)) { return }
    $marker = "console.debug('[ServiceWorker] Installing offline worker');"
    $bytes  = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text   = if ($hasBom) { [System.Text.Encoding]::UTF8.GetString($bytes,3,$bytes.Length-3) } else { [System.Text.Encoding]::UTF8.GetString($bytes) }
    if ($text -match 'skipWaiting' -or -not $text.Contains($marker)) { return }
    $text = $text.Replace($marker, "$marker`r`n        self.skipWaiting();")
    $enc = New-Object System.Text.UTF8Encoding($hasBom)
    [System.IO.File]::WriteAllBytes($Path, $enc.GetPreamble() + $enc.GetBytes($text))
}

$launcher = Join-Path $repoRoot 'Source\Launcher\Launcher\Launcher.csproj'
Write-Host ""
Write-Host "=== Launcher -> $OutputDir ===" -ForegroundColor Yellow
Publish-WasmApp -Project $launcher -Dest $OutputDir
Repair-RootWebConfig -Path (Join-Path $OutputDir 'web.config')
Add-SwSkipWaiting    -Path (Join-Path $OutputDir 'service-worker.js')

foreach ($g in $games) {
    $proj = Join-Path $repoRoot ("Source\{0}\{0}\{0}.csproj" -f $g.Name)
    $dest = Join-Path $OutputDir ("games\{0}" -f $g.Slug)
    Write-Host ""
    Write-Host ("=== {0} -> games/{1}/ ===" -f $g.Name, $g.Slug) -ForegroundColor Yellow
    Publish-WasmApp -Project $proj -Dest $dest
    Convert-GameToSubfolderRelative -GameDir $dest
    # The per-game web.config is byte-identical to the launcher's; nested under it
    # on IIS its repeated mimeMaps + rewrite-rule names become duplicate collection
    # entries -> HTTP 500.19 on /games/<slug>/. Drop it and inherit the root config.
    Remove-Item (Join-Path $dest 'web.config') -Force -ErrorAction SilentlyContinue
    Add-SwSkipWaiting -Path (Join-Path $dest 'service-worker.js')
}

Write-Host ""
Write-Host "Site published to $OutputDir" -ForegroundColor Green
Write-Host "Serve locally with one of:" -ForegroundColor DarkGray
Write-Host "  dotnet serve -d `"$OutputDir`" -p 8080" -ForegroundColor DarkGray
Write-Host "  python -m http.server 8080 --directory `"$OutputDir`"" -ForegroundColor DarkGray
exit 0
