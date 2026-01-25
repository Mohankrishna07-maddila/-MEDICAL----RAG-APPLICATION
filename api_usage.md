# HealthBot API Commands

## 🚀 Run the Application
```powershell
dotnet run --project HealthBot.Api
```

## 🎫 Ticket Management

### 1️⃣ List Open Tickets
Retrieves all tickets with status `OPEN`.
```powershell
Invoke-RestMethod http://localhost:5030/agent/tickets/open
```

### 2️⃣ Get Specific Ticket
Requires both `TicketId` and `CreatedAt` (timestamp).
*Replace values with your actual data.*
```powershell
$ticketId = "TKT-36e4c453"
$createdAt = 1769330542

Invoke-RestMethod "http://localhost:5030/agent/tickets/$ticketId/$createdAt"
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
    -Body $body
```
