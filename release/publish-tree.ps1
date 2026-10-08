# LWF Tax Ledger — 公開 repo（D:\Wiki\lwf-tax-ledger）に出すものだけを並べる
#
#   powershell -ExecutionPolicy Bypass -File release\publish-tree.ps1 [-Dest D:\Wiki\lwf-tax-ledger]
#
# 出すもの:  ソース 6 本・build.ps1、release\ の README.md / README.en.md / CHANGELOG.md・CHANGELOG.ja.md・img\（en\ 含む）・
#            release.ps1・zip-README.txt・publish-tree.ps1・thunderstore\
# 出さないもの:
#   - README.md（開発用。解析パイプラインの記述と手元のパスを含む）
#   - icon\（作業ファイル）、crop-panel.py（開発用）
#   - bin\ dist\（生成物）
#
# ⚠ このファイル自身は UTF-8 (BOM あり) で保存すること。

param(
    [string]$Dest = "D:\Wiki\lwf-tax-ledger"
)
$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path   # ...\tax_ledger\release
$mod  = Split-Path -Parent $here                          # ...\tax_ledger

if (-not (Test-Path $Dest)) { throw "公開 repo がありません: $Dest（先に git init / clone しておく）" }

function CopyTo($src, $rel) {
    $dst = Join-Path $Dest $rel
    $dir = Split-Path -Parent $dst
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Copy-Item $src -Destination $dst -Force
}

# ソースとビルド
foreach ($f in @('TaxLedgerMod.cs','Ledger.cs','LedgerView.cs','TaxHooks.cs','DevTax.cs','LedgerFile.cs','build.ps1')) {
    CopyTo (Join-Path $mod $f) $f
}

# 文書
CopyTo (Join-Path $here 'README.md') 'README.md'
CopyTo (Join-Path $here 'README.en.md') 'README.en.md'
CopyTo (Join-Path $here 'CHANGELOG.md') 'CHANGELOG.md'
CopyTo (Join-Path $here 'CHANGELOG.ja.md') 'CHANGELOG.ja.md'

# 画像
$img = Join-Path $Dest 'img'
if (-not (Test-Path $img)) { New-Item -ItemType Directory -Path $img -Force | Out-Null }
Copy-Item (Join-Path $here 'img\*') -Destination $img -Force -Recurse

# release
foreach ($f in @('release.ps1','zip-README.txt','publish-tree.ps1')) {
    CopyTo (Join-Path $here $f) (Join-Path 'release' $f)
}
foreach ($f in @('manifest.json','icon.png','README.md','README.ja.md')) {
    CopyTo (Join-Path $here (Join-Path 'thunderstore' $f)) (Join-Path 'release' (Join-Path 'thunderstore' $f))
}

Write-Host "並べました: $Dest"
Get-ChildItem $Dest -Recurse -File | Where-Object { $_.FullName -notmatch '\\\.git\\' } | ForEach-Object { Write-Host "  $($_.FullName.Substring($Dest.Length + 1))" }
