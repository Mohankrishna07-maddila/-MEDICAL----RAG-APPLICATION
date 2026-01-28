$headers = @{ "Content-Type" = "application/json" }

echo "--- TEST 1: Gold Plan (Authorized) ---"
$body1 = @{ SessionId = "test_user_1"; Message = "What is the coverage limit for Gold Plan?" } | ConvertTo-Json
$response1 = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers $headers -Body $body1
echo $response1.Answer
echo "Sources: $($response1.Sources)"

echo "`n--- TEST 2: Internal SOP (Unauthorized) ---"
$body2 = @{ SessionId = "test_user_1"; Message = "What is step 3 of the claim approval guidelines?" } | ConvertTo-Json
$response2 = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers $headers -Body $body2
echo $response2.Answer
echo "Sources: $($response2.Sources)"
