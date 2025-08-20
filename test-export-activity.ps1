# Test Export-ActivityExplorerData and display results
# Run this after connecting to Exchange Online

# First connect if not already connected
# Connect-IPPSSession -CertificateThumbprint "2F333B7E2923CA32F699E977F2952423990878F7" -AppId "f88ad2f0-dd16-4b4f-ab8b-5061605bd939" -Organization "drozdovo.cz"

# Export activity data
Write-Host "Fetching Activity Explorer data..." -ForegroundColor Yellow
$results = Export-ActivityExplorerData -StartTime (Get-Date).AddDays(-7) -EndTime (Get-Date) -OutputFormat JSON

# Check if we got results
Write-Host "`nResults count: $($results.Count)" -ForegroundColor Cyan

# Display the raw object type
Write-Host "`nObject type: $($results.GetType().FullName)" -ForegroundColor Cyan

# Try to access the actual data in different ways
Write-Host "`n=== Method 1: Direct property access ===" -ForegroundColor Green
$results | Get-Member -MemberType Properties | ForEach-Object {
    Write-Host "$($_.Name): $($results.$($_.Name))"
}

Write-Host "`n=== Method 2: Convert to JSON ===" -ForegroundColor Green
$results | ConvertTo-Json -Depth 10

Write-Host "`n=== Method 3: Select all properties ===" -ForegroundColor Green
$results | Select-Object * | Format-List

Write-Host "`n=== Method 4: If it has Items property ===" -ForegroundColor Green
if ($results.Items) {
    Write-Host "Items count: $($results.Items.Count)"
    $results.Items | Select-Object -First 5 | ConvertTo-Json -Depth 5
}

Write-Host "`n=== Method 5: Iterate if enumerable ===" -ForegroundColor Green
$results | ForEach-Object {
    $_ | Select-Object * | Format-List
}

# If the result has specific known properties from the documentation
Write-Host "`n=== Method 6: Try known Activity Explorer properties ===" -ForegroundColor Green
$properties = @(
    'RecordType',
    'CreationTime', 
    'UserIds',
    'Operations',
    'AuditData',
    'ResultIndex',
    'ResultCount',
    'Identity',
    'IsValid',
    'ObjectState'
)

foreach ($prop in $properties) {
    try {
        $value = $results.$prop
        if ($value) {
            Write-Host "${prop}: $value" -ForegroundColor Yellow
        }
    } catch {
        # Property doesn't exist, skip
    }
}