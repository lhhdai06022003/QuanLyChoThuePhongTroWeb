<#
.SYNOPSIS
Gửi một webhook SePay giả (tiền vào) tới web chạy cục bộ để thử luồng ghi sổ hóa đơn.

.DESCRIPTION
Dựng payload đúng định dạng SePay, ký HMAC-SHA256 trên "{timestamp}.{body}" rồi POST. Dùng khi chưa bật tunnel
và không muốn tốn hạn mức Test mode của SePay. Chạy được trên Windows PowerShell 5.1 và PowerShell 7.

Điều kiện:
- Web đang chạy với SePay:Enabled = true và SePay:WebhookSecret trùng $env:QLCTPT_SEPAY_WEBHOOK_SECRET
  (qua appsettings.json, hoặc thêm đối số khi chạy: -- --SePay:Enabled=true --SePay:WebhookSecret=...).
- VietQRSettings:AccountNumber của web trùng $env:QLCTPT_SEPAY_ACCOUNT_NUMBER, nếu không giao dịch bị bỏ qua
  vì sai tài khoản nhận.
- Chạy web bằng profile http (cổng 5009) để không bị chuyển hướng sang https.
- Hóa đơn phải ở trạng thái Đã gửi thì mới ghi sổ được.

Gửi lại với cùng -ReferenceCode để thử chống trùng: hóa đơn chỉ có một dòng sổ.
Script không in secret.

.PARAMETER HoaDonId
Id hóa đơn, dùng để tạo nội dung chuyển khoản TTPTHD{HoaDonId} khi không truyền -NoiDung.

.PARAMETER SoTien
Số tiền chuyển (VND, số nguyên).

.PARAMETER NoiDung
Nội dung chuyển khoản. Mặc định TTPTHD{HoaDonId}.

.PARAMETER ReferenceCode
Mã tham chiếu ngân hàng, cũng là khóa chống trùng. Mặc định sinh ngẫu nhiên.

.PARAMETER Url
Địa chỉ endpoint webhook. Mặc định http://localhost:5009/api/webhooks/payment/sepay.

.EXAMPLE
$env:QLCTPT_SEPAY_WEBHOOK_SECRET = "<secret>"; $env:QLCTPT_SEPAY_ACCOUNT_NUMBER = "<số tài khoản>"
.\scripts\sepay-test-webhook.ps1 -HoaDonId 105 -SoTien 2000000

.EXAMPLE
.\scripts\sepay-test-webhook.ps1 -HoaDonId 105 -SoTien 2000000 -ReferenceCode FT26ABC123
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$HoaDonId,
    [Parameter(Mandatory)][long]$SoTien,
    [string]$NoiDung,
    [string]$ReferenceCode,
    [string]$Url = "http://localhost:5009/api/webhooks/payment/sepay"
)

$secret = $env:QLCTPT_SEPAY_WEBHOOK_SECRET
$accountNumber = $env:QLCTPT_SEPAY_ACCOUNT_NUMBER
if ([string]::IsNullOrWhiteSpace($secret)) {
    Write-Error "Thiếu biến môi trường QLCTPT_SEPAY_WEBHOOK_SECRET (secret của webhook, trùng SePay:WebhookSecret của web)."
    exit 1
}
if ([string]::IsNullOrWhiteSpace($accountNumber)) {
    Write-Error "Thiếu biến môi trường QLCTPT_SEPAY_ACCOUNT_NUMBER (số tài khoản nhận, trùng VietQRSettings:AccountNumber của web)."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($NoiDung)) {
    $NoiDung = "TTPTHD$HoaDonId"
}
if ([string]::IsNullOrWhiteSpace($ReferenceCode)) {
    $ReferenceCode = "FT" + (-join ((1..10) | ForEach-Object { '{0:X}' -f (Get-Random -Maximum 16) }))
}

# transactionDate là giờ Việt Nam (UTC+7), định dạng yyyy-MM-dd HH:mm:ss.
$transactionDate = [DateTime]::UtcNow.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss", [System.Globalization.CultureInfo]::InvariantCulture)

$payload = [ordered]@{
    id              = Get-Random -Minimum 1 -Maximum 2147483647
    gateway         = "MBBank"
    transactionDate = $transactionDate
    accountNumber   = $accountNumber
    subAccount      = $null
    code            = $null
    content         = $NoiDung
    transferType    = "in"
    transferAmount  = $SoTien
    accumulated     = 0
    referenceCode   = $ReferenceCode
    description     = $NoiDung
}
$json = $payload | ConvertTo-Json -Compress

# Ký đúng các byte sẽ gửi: UTF-8 không BOM.
$bodyBytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($json)
$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$prefixBytes = [System.Text.Encoding]::UTF8.GetBytes("$timestamp.")
$data = New-Object byte[] ($prefixBytes.Length + $bodyBytes.Length)
[Array]::Copy($prefixBytes, 0, $data, 0, $prefixBytes.Length)
[Array]::Copy($bodyBytes, 0, $data, $prefixBytes.Length, $bodyBytes.Length)

$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
$hash = $hmac.ComputeHash($data)
$hmac.Dispose()
$signature = "sha256=" + (($hash | ForEach-Object { $_.ToString("x2") }) -join "")

$headers = @{
    "X-SePay-Timestamp" = "$timestamp"
    "X-SePay-Signature" = $signature
}

Write-Host "Gửi tới $Url"
Write-Host "HoaDonId=$HoaDonId  SoTien=$SoTien  NoiDung='$NoiDung'  ReferenceCode=$ReferenceCode"

$status = 0
$content = $null
try {
    $response = Invoke-WebRequest -Uri $Url -Method Post -UseBasicParsing -ContentType "application/json; charset=utf-8" -Headers $headers -Body $bodyBytes
    $status = [int]$response.StatusCode
    $content = $response.Content
}
catch {
    $errorResponse = $_.Exception.Response
    if ($null -eq $errorResponse) {
        Write-Error "Không kết nối được tới $Url : $($_.Exception.Message)"
        exit 2
    }

    # 401, 400, 404, 500...: in mã trạng thái thay vì ném lỗi.
    $status = [int]$errorResponse.StatusCode
    if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
        $content = $_.ErrorDetails.Message
    }
    elseif ($errorResponse -is [System.Net.HttpWebResponse]) {
        $reader = New-Object System.IO.StreamReader($errorResponse.GetResponseStream())
        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
}

Write-Host "HTTP $status"
if (-not [string]::IsNullOrEmpty($content)) {
    Write-Host $content
}
Write-Host "Gửi lại cùng giao dịch để thử chống trùng: -ReferenceCode $ReferenceCode"

if ($status -ge 200 -and $status -lt 300) { exit 0 } else { exit 1 }
