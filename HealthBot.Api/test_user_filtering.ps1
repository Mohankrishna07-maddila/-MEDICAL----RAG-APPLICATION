Write-Host "Re-syncing RAG with user-specific metadata..." -ForegroundColor Cyan
Write-Host ""

# Step 1: Clear and re-sync
Write-Host "Step 1: Triggering RAG sync..." -ForegroundColor Yellow
$syncResponse = Invoke-RestMethod -Uri "http://localhost:5030/admin/sync-rag" -Method Post
Write-Host "Sync Response: $($syncResponse.message)" -ForegroundColor Green
Write-Host ""

# Step 2: Check diagnostic
Write-Host "Step 2: Checking diagnostic info..." -ForegroundColor Yellow
$diagnostic = Invoke-RestMethod -Uri "http://localhost:5030/admin/diagnostic" -Method Get
Write-Host "Total Chunks: $($diagnostic.totalChunks)" -ForegroundColor Magenta
Write-Host "Index Terms: $($diagnostic.indexTermCount)" -ForegroundColor Magenta
Write-Host ""

# Step 3: Test user-1 filtering
Write-Host "Step 3: Testing user-1 (should only see user1 + global docs)..." -ForegroundColor Yellow
$response1 = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="user-1"; Message="What is my policy number?"} | ConvertTo-Json)
Write-Host "Answer: $($response1.Answer)" -ForegroundColor Green
Write-Host ""

# Step 4: Test user-2 filtering
Write-Host "Step 4: Testing user-2 (should only see user2 + global docs)..." -ForegroundColor Yellow
$response2 = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="user-2"; Message="What is my policy number?"} | ConvertTo-Json)
Write-Host "Answer: $($response2.Answer)" -ForegroundColor Green
Write-Host ""

Write-Host "Check API console for [RAG] logs showing user_id filtering!" -ForegroundColor Cyan
