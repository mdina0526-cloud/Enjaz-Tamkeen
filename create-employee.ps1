param(
    [Parameter(Mandatory = $true)]
    [string]$FullName,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [string]$Email,
    [string]$Phone,
    [string]$JobTitle,
    [decimal]$DeductionPercentage = 0,
    [string]$AdminUsername = 'admin',
    [string]$ApiBaseUrl = 'http://localhost:5224',
    [string]$SiteBaseUrl = 'http://localhost:5173'
)

$ErrorActionPreference = 'Stop'
$ApiBaseUrl = $ApiBaseUrl.TrimEnd('/')
$SiteBaseUrl = $SiteBaseUrl.TrimEnd('/')

function Read-Password([string]$Prompt) {
    $secure = Read-Host -Prompt $Prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Get-ApiError($errorRecord) {
    $body = $errorRecord.ErrorDetails.Message
    if ($body) {
        try {
            $problem = $body | ConvertFrom-Json
            if ($problem.detail) { return $problem.detail }
            if ($problem.title) { return $problem.title }
        }
        catch { }
    }
    return $errorRecord.Exception.Message
}

try {
    $adminPassword = Read-Password 'كلمة مرور المدير'
    $newPassword = Read-Password 'كلمة مرور الحساب الجديد'
    if ($newPassword -cne (Read-Password 'تأكيد كلمة مرور الحساب الجديد')) {
        throw 'كلمتا مرور الحساب الجديد غير متطابقتين.'
    }

    $loginBody = @{ username = $AdminUsername; password = $adminPassword } | ConvertTo-Json
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method Post -ContentType 'application/json' -Body $loginBody
    if ($login.user.role -ne 'SuperAdmin') {
        throw 'الحساب المستخدم لا يملك صلاحية إنشاء الموظفين.'
    }

    $employeeBody = @{
        fullName = $FullName
        username = $Username
        password = $newPassword
        email = $Email
        phone = $Phone
        jobTitle = $JobTitle
        deductionPercentage = $DeductionPercentage
    } | ConvertTo-Json
    $headers = @{ Authorization = "Bearer $($login.token)" }
    $created = Invoke-RestMethod -Uri "$ApiBaseUrl/api/employees" -Method Post -ContentType 'application/json' -Headers $headers -Body $employeeBody

    Write-Host "تم إنشاء الحساب: $($created.username)"
    Write-Host "الرقم الوظيفي: $($created.employeeCode)"
    if ($created.loginSlug) {
        Write-Host "رابط الدخول الخاص: $SiteBaseUrl/e/$($created.loginSlug)"
    }
}
catch {
    Write-Error (Get-ApiError $_)
    exit 1
}
