Write-Host "Testing Smart Incremental RAG Sync..." -ForegroundColor Cyan
Write-Host ""

# Step 1: Full sync to initialize
Write-Host "Step 1: Running FULL sync to initialize system..." -ForegroundColor Yellow
$fullSync = Invoke-RestMethod -Uri "http://localhost:5030/admin/sync-rag/full" -Method Post
Write-Host "Result: $($fullSync.Message)" -ForegroundColor Green
Write-Host ""

Start-Sleep -Seconds 3

# Step 2: Check sync status
Write-Host "Step 2: Checking sync status..." -ForegroundColor Yellow
$status = Invoke-RestMethod -Uri "http://localhost:5030/admin/sync-rag/status" -Method Get
Write-Host "Last Sync: $($status.LastSync)" -ForegroundColor Magenta
Write-Host "Files Processed: $($status.FilesProcessed)" -ForegroundColor Magenta
Write-Host "Duration: $($status.LastSyncDuration)" -ForegroundColor Magenta
Write-Host ""

# Step 3: Test incremental sync (should find no new files)
Write-Host "Step 3: Testing incremental sync (should find 0 new files)..." -ForegroundColor Yellow
$incremental1 = Invoke-RestMethod -Uri "http://localhost:5030/admin/sync-rag/incremental" -Method Post
Write-Host "Result: $($incremental1.Message)" -ForegroundColor Green
Write-Host "Files Processed: $($incremental1.FilesProcessed)" -ForegroundColor Magenta
Write-Host ""

# Step 4: Test user-specific filtering
Write-Host "Step 4: Testing user-specific RAG filtering..." -ForegroundColor Yellow
$response1 = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="U101"; Message="What is my policy number?"} | ConvertTo-Json)
Write-Host "U101 Answer: $($response1.Answer)" -ForegroundColor Green
Write-Host ""

$response2 = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="U102"; Message="What is my sum insured?"} | ConvertTo-Json)
Write-Host "U102 Answer: $($response2.Answer)" -ForegroundColor Green
Write-Host ""

Write-Host "✅ Testing Complete!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Background Service Info:" -ForegroundColor Yellow
Write-Host "  - Auto-sync runs every 5 minutes" -ForegroundColor White
Write-Host "  - Add a new file to S3 and wait 5 minutes to test auto-sync" -ForegroundColor White
Write-Host "  - Check API console for [AUTO-SYNC] logs" -ForegroundColor White
