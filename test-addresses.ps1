# Test script to verify addresses are returned correctly

$baseUrl = "http://localhost:5671"

# Create workspace
Write-Host "Creating workspace..." -ForegroundColor Cyan
$workspace = Invoke-RestMethod -Uri "$baseUrl/admin/workspaces" -Method Post -ContentType "application/json" -Body '{"name":"Test Workspace"}'
$workspaceId = $workspace.data.id
$apiKey = $workspace.data.apiKeys[0].key
Write-Host "Created workspace: $workspaceId" -ForegroundColor Green

# Create vault
Write-Host "`nCreating vault..." -ForegroundColor Cyan
$vault = Invoke-RestMethod -Uri "$baseUrl/admin/vaults" -Method Post -ContentType "application/json" -Body '{"name":"Test Vault"}' -Headers @{"X-Workspace-Id"=$workspaceId}
$vaultId = $vault.data.id
Write-Host "Created vault: $vaultId" -ForegroundColor Green

# Create BTC wallet
Write-Host "`nCreating BTC wallet..." -ForegroundColor Cyan
$wallet = Invoke-RestMethod -Uri "$baseUrl/admin/vaults/$vaultId/wallets" -Method Post -ContentType "application/json" -Body '{"assetId":"BTC"}' -Headers @{"X-Workspace-Id"=$workspaceId}
Write-Host "Created wallet with addresses:" -ForegroundColor Green
$wallet.data.addresses | ForEach-Object {
    Write-Host "  - Type: $($_.type), Address: $($_.addressValue)" -ForegroundColor Yellow
}

# Get vault detail to verify all addresses show up
Write-Host "`nGetting vault details..." -ForegroundColor Cyan
$vaultDetail = Invoke-RestMethod -Uri "$baseUrl/admin/vaults/$vaultId" -Method Get -Headers @{"X-Workspace-Id"=$workspaceId}
Write-Host "Vault has $($vaultDetail.data.wallets.Count) wallet(s)" -ForegroundColor Green

foreach ($w in $vaultDetail.data.wallets) {
    Write-Host "`nWallet: $($w.assetId)" -ForegroundColor Cyan
    Write-Host "  Address Count: $($w.addressCount)" -ForegroundColor White
    Write-Host "  Addresses:" -ForegroundColor White
    if ($w.addresses) {
        foreach ($addr in $w.addresses) {
            Write-Host "    - Type: $($addr.type), Value: $($addr.addressValue), Description: $($addr.description)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "    WARNING: No addresses array in response!" -ForegroundColor Red
    }
}
