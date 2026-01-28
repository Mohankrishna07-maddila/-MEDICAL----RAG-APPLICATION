Write-Host "Testing Chat Performance Optimizations..." -ForegroundColor Cyan
Write-Host ""

# Test 1: Greeting (should be fast, no history load)
Write-Host "Test 1: Greeting (optimized - no history load)" -ForegroundColor Yellow
$start = Get-Date
$response = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="perf_test_1"; Message="hi"} | ConvertTo-Json)
$elapsed = (Get-Date) - $start
Write-Host "Response: $($response.Answer)" -ForegroundColor Green
Write-Host "Time: $($elapsed.TotalMilliseconds)ms" -ForegroundColor Magenta
Write-Host ""

# Test 2: First real query (cache miss)
Write-Host "Test 2: First query (cache MISS)" -ForegroundColor Yellow
$start = Get-Date
$response = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="perf_test_2"; Message="What is my policy number?"} | ConvertTo-Json)
$elapsed = (Get-Date) - $start
Write-Host "Time: $($elapsed.TotalMilliseconds)ms" -ForegroundColor Magenta
Write-Host ""

# Test 3: Second query same session (cache HIT)
Write-Host "Test 3: Second query (cache HIT - should be faster)" -ForegroundColor Yellow
$start = Get-Date
$response = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers @{"Content-Type"="application/json"} -Body (@{SessionId="perf_test_2"; Message="What is the claim process?"} | ConvertTo-Json)
$elapsed = (Get-Date) - $start
Write-Host "Time: $($elapsed.TotalMilliseconds)ms" -ForegroundColor Magenta
Write-Host ""

Write-Host "Check the API console for [PERF] logs showing:" -ForegroundColor Cyan
Write-Host "  - Cache HIT/MISS messages" -ForegroundColor White
Write-Host "  - Batch write confirmations" -ForegroundColor White
Write-Host "  - Total request times" -ForegroundColor White
