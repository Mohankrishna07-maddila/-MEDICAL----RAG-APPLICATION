$response = Invoke-RestMethod -Uri "http://localhost:5030/admin/diagnostic" -Method Get
$response | ConvertTo-Json -Depth 10
