$response = Invoke-RestMethod -Uri "http://localhost:5030/admin/sync-rag" -Method Post
echo $response
