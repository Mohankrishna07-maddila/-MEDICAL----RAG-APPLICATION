$headers = @{ "Content-Type" = "application/json" }
$body = @{ SessionId = "test_user_new"; Message = "What is the policy number for Ramesh Kumar?" } | ConvertTo-Json
$response = Invoke-RestMethod -Uri "http://localhost:5030/chat" -Method Post -Headers $headers -Body $body
echo $response.Answer
echo "Sources: $($response.Sources)"
