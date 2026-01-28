Write-Host "Testing New S3 Structure with User-Specific Filtering..." -ForegroundColor Cyan
Write-Host ""

# Step 1: Re-sync RAG data
Write-Host "Step 1: Re-syncing RAG data from new S3 structure..." -ForegroundColor Yellow
$syncResponse = Invoke-RestMethod -Uri "http://localhost:5030/admin/sync-rag" -Method Post
Write-Host "Sync Response: $($syncResponse.message)" -ForegroundColor Green
Write-Host ""

Start-Sleep -Seconds 2

# Step 2: Check diagnostic
Write-Host "Step 2: Checking diagnostic info..." -ForegroundColor Yellow
$diagnostic = Invoke-RestMethod -Uri "http://localhost:5030/admin/diagnostic" -Method Get
Write-Host "Total Chunks: $($diagnostic.totalChunks)" -ForegroundColor Magenta
Write-Host "Index Terms: $($diagnostic.indexTermCount)" -ForegroundColor Magenta
Write-Host ""

# Step 3: Test U101 (should map to user1)
Write-Host "Step 3: Testing session 'U101' (should see user1 docs + global)..." -ForegroundColor Yellow
$response1 = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="U101"; Message="What is my policy number?"} | ConvertTo-Json)
Write-Host "Answer: $($response1.Answer)" -ForegroundColor Green
Write-Host ""

# Step 4: Test user1 (alternative format)
Write-Host "Step 4: Testing session 'user1' (should see user1 docs + global)..." -ForegroundColor Yellow
$response2 = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="user1"; Message="What claims have I made?"} | ConvertTo-Json)
Write-Host "Answer: $($response2.Answer)" -ForegroundColor Green
Write-Host ""

# Step 5: Test U102 (should map to user2)
Write-Host "Step 5: Testing session 'U102' (should see user2 docs + global)..." -ForegroundColor Yellow
$response3 = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="U102"; Message="What is my sum insured?"} | ConvertTo-Json)
Write-Host "Answer: $($response3.Answer)" -ForegroundColor Green
Write-Host ""

Write-Host "Check API console for:" -ForegroundColor Cyan
Write-Host "  [RAG-SYNC] File: users/U101/policy.txt → user_id: user1" -ForegroundColor White
Write-Host "  [RAG] Session 'U101' mapped to user_id: 'user1'" -ForegroundColor White
Write-Host "  [RAG] User-specific filter: X user docs + Y global docs = Z total" -ForegroundColor White
