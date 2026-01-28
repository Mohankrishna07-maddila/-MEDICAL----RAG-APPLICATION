Write-Host "Re-syncing with smart confidence scoring..." -ForegroundColor Cyan
$response = Invoke-RestMethod -Uri "http://localhost:5030/admin/sync-rag" -Method Post
Write-Host $response.message -ForegroundColor Green

Write-Host "`nRunning diagnostic..." -ForegroundColor Cyan
$diag = Invoke-RestMethod -Uri "http://localhost:5030/admin/diagnostic" -Method Get
Write-Host "Total Vectors: $($diag.TotalVectors)" -ForegroundColor Yellow
Write-Host "Metadata Index (role:customer): $($diag.MetadataIndex_RoleCustomer_Count)" -ForegroundColor Yellow
