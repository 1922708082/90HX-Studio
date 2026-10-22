# ============================================================
#  给 exe 打代码签名（Authenticode）
#
#  用法：
#    powershell -ExecutionPolicy Bypass -File sign-exe.ps1
#      → 用本机已有的代码签名证书签 D:\app 下的两个 exe
#    powershell ... -File sign-exe.ps1 -CreateCert
#      → 没有证书时，新建一张自签名证书（10 年有效）再签
#    powershell ... -File sign-exe.ps1 -TrustCert
#      → 把证书装进「受信任的根证书颁发机构」（只影响本机）
#    -File sign-exe.ps1 -WhatIf  → 只列会签哪些文件
#
#  重要事实（别被"签了名就没人警告"误导）：
#   · 自签名证书只能证明「这个 exe 没被改过」，不能证明「你是谁」
#   · 别人电脑第一次运行仍会弹 SmartScreen「未知发布者」，除非他先装了你的证书
#   · 想彻底消除警告，只能买 CA 签发的 OV / EV 证书，或使用微软 Trusted Signing
# ============================================================

param(
    [switch]$CreateCert,
    [switch]$TrustCert,
    [switch]$WhatIf,
    [string]$Subject = "CN=CMP90HX Unlock Signing",
    [string[]]$Files
)

$ErrorActionPreference = 'Stop'

function Write-Step($text, $color = 'Gray') { Write-Host $text -ForegroundColor $color }

if (-not $Files -or $Files.Count -eq 0) {
    $Files = @(
        'D:\app\90HX Studio.exe',
        'D:\app\HX家族多功能工具箱.exe',
        'C:\Users\19227\Desktop\90HX Studio.exe'
    )
}
$Files = $Files | Where-Object { Test-Path $_ }

if ($Files.Count -eq 0) {
    Write-Step "[X] 没找到要签名的 exe（先跑一次 build.ps1）" 'Red'
    exit 1
}

# ---------- 找证书 ----------
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.Subject -eq $Subject } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

if (-not $cert -and $CreateCert) {
    Write-Step "没有找到证书，正在创建自签名代码签名证书..." 'Yellow'
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(10) `
        -KeyUsage DigitalSignature `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')
    Write-Step ("已创建证书: {0}" -f $cert.Thumbprint) 'Green'
}

if (-not $cert) {
    Write-Step "[X] 当前用户证书库里没有代码签名证书" 'Red'
    Write-Step "    加 -CreateCert 参数可以创建一张自签名证书，例如：" 'Gray'
    Write-Step "    powershell -ExecutionPolicy Bypass -File sign-exe.ps1 -CreateCert" 'DarkGray'
    exit 1
}

Write-Step ("使用证书: {0}" -f $cert.Subject) 'Cyan'
Write-Step ("  指纹   : {0}" -f $cert.Thumbprint) 'Gray'
Write-Step ("  有效期 : {0:yyyy-MM-dd}" -f $cert.NotAfter) 'Gray'
if ($cert.Subject -eq $cert.Issuer) { Write-Step "  类型   : 自签名（只有装了你证书的机器才信任）" 'Yellow' }

if ($TrustCert) {
    Write-Step "正在把证书安装到「受信任的根证书颁发机构」..." 'Yellow'
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'CurrentUser')
    $store.Open('ReadWrite')
    $store.Add($cert)
    $store.Close()
    Write-Step "[OK] 已加入当前用户的受信任根（别的用户/别的机器仍然不信任）" 'Green'
}

# ---------- 签名 ----------
$ts = 'http://timestamp.digicert.com'
$ok = 0; $fail = 0
foreach ($f in $Files) {
    if ($WhatIf) { Write-Step ("将签名: {0}" -f $f) 'DarkGray'; continue }
    try {
        Write-Step ("正在签名: {0}" -f $f) 'Yellow'
        $sig = Set-AuthenticodeSignature -FilePath $f -Certificate $cert -TimestampServer $ts -HashAlgorithm SHA256
        if ($sig.Status -eq 'Valid') {
            Write-Step ("  [OK] 签名有效  签名时间: {0}" -f $sig.SignerCertificate.Subject) 'Green'
            $ok++
        } else {
            Write-Step ("  [!] 状态: {0}  {1}" -f $sig.Status, $sig.StatusMessage) 'Yellow'
            $fail++
        }
    } catch {
        Write-Step ("  [X] 签名失败: {0}" -f $_.Exception.Message) 'Red'
        $fail++
    }
}

if (-not $WhatIf) {
    Write-Step "--------------------------------------------------" 'Cyan'
    Write-Step ("完成：成功 {0} 个，失败 {1} 个" -f $ok, $fail) $(if ($fail -eq 0) { 'Green' } else { 'Yellow' })
    Write-Step "注意：重新 build 会覆盖 exe，签名也随之丢失，每次发版前都要重新签。" 'Gray'
}
