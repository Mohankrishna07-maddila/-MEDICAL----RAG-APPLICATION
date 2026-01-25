# HealthBot API Commands

## 🚀 Run the Application
Open a terminal and run:
```powershell
dotnet run --project HealthBot.Api
```

---

## 🔐 Authentication (Step 1)
**You must get a token before calling other APIs.**

### 1️⃣ Get User Token (For Chat)
Run this to get a token for normal users:
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5030/auth/token" `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"username": "testuser"}'

$userToken = $response.access_token
Write-Host "USER TOKEN: $userToken"
```

### 2️⃣ Get Agent Token (For Tickets)
Run this to get a token for support agents:
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5030/auth/token" `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"username": "agent"}'

$agentToken = $response.access_token
Write-Host "AGENT TOKEN: $agentToken"
```

---

## 💬 Chat API (Requires User Token)

### Send a Message
```powershell
$body = @{
    sessionId = "session-123"
    message = "I need to talk to a human agent"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5030/chat" `
    -Method POST `
    -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $userToken" } `
    -Body $body
```

---

## 🎫 Ticket Management (Requires Agent Token)

### 1️⃣ List Open Tickets
Retrieves all tickets with status `OPEN`.
```powershell
Invoke-RestMethod -Uri "http://localhost:5030/agent/tickets/open" `
    -Headers @{ Authorization = "Bearer $agentToken" }
```

### 2️⃣ Get Specific Ticket
Requires both `TicketId` and `CreatedAt` (timestamp).
*Replace values with your actual data.*
```powershell
$ticketId = "TKT-36e4c453"
$createdAt = 1769330542

Invoke-RestMethod -Uri "http://localhost:5030/agent/tickets/$ticketId/$createdAt" `
    -Headers @{ Authorization = "Bearer $agentToken" }
```

### 3️⃣ Update Ticket Status
Updates the status of a ticket. Note: `createdAt` is required in the body to identify the record.

#### Set to IN_PROGRESS
```powershell
$ticketId = "TKT-36e4c453"
$body = @{
    createdAt = 1769330542
    status = "IN_PROGRESS"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5030/agent/tickets/$ticketId/status" `
    -Method PUT `
    -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $agentToken" } `
    -Body $body
```

#### Set to CLOSED
```powershell
$ticketId = "TKT-36e4c453"
$body = @{
    createdAt = 1769330542
    status = "CLOSED"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5030/agent/tickets/$ticketId/status" `
    -Method PUT `
    -ContentType "application/json" `
    -Headers @{ Authorization = "Bearer $agentToken" } `
    -Body $body
```
