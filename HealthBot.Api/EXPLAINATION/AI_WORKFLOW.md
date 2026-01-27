# HealthBot AI Workflow

This document outlines the architecture and data flow of the HealthBot AI application.

## 1. High-Level Architecture

The HealthBot is a .NET Web API that interfaces with a Frontend (Blazor/React) and a Local LLM (Ollama).

**Core Components:**
- **HealthBot.Api**: The backend handling logic, context management, and API endpoints.
- **Ollama (Local LLM)**: Hosts the `gemma3:4b` model for generating responses.
- **DynamoDB**: Stores conversation history (`DynamoConversationMemory`) and vectors (`DynamoVectorRepository`).
- **AWS S3**: Stores policy documents which are indexed into vectors.

---

## 2. Request Lifecycle (The "Chat" Flow)

When a user sends a message, it follows this pipeline in `ChatController.cs`:

### Step 1: Immediate Checks (Short-Circuits)
Before involving the AI, the system deals with specific cases instantly:
1.  **Greeting Check**: If the user says "Hi", "Hello", etc., the bot responds with a standard greeting immediately.
2.  **Ticket Status**: If the message contains `TKT-XXXX`, the system looks up the ticket in DynamoDB and returns its status.
3.  **Explicit Handoff**: If the user asks to "talk to agent" or "support", a ticket is created instantly, and the bot responds with the ticket ID.

### Step 2: Context Building (`HybridContextService`)
If no short-curcuit triggers, the system builds the context for the AI:
1.  **Conversation History**: Fetches the last 6 messages from DynamoDB.
2.  **Frustration Detection**: Checks for keywords like "stupid", "broken" or repeated confusion. If detected, `IsFrustrated` flag is set.
3.  **Vector RAG (Policy Search)**:
    *   Embeds the user's question using `EmbeddingService`.
    *   Searches `DynamoVectorRepository` for matching policy chunks.
    *   If matches are found (Cosine Similarity > 0.45), they are added to the context.
    *   If no matches are found for a relevant query, `IsLowConfidence` flag is set.

### Step 3: Handoff Logic
Before generating an answer, the system checks the flags from Step 2:
*   **Frustrated User**: Auto-creates a ticket and replies: *"I see we're going in circles... I've created a ticket."*
*   **Low Confidence (Unknown Policy)**: If RAG failed but the user asked about insurance, it auto-creates a ticket and replies: *"I couldn't find details... I've connected you to an agent."*

### Step 4: AI Generation (`LocalLlmService`)
If no handoff is needed, the system generates a response:
1.  **Prompt Construction**: A system prompt is built containing:
    *   Identity Rules ("You are an AI for hospital insurance...").
    *   Context (Conversation History + Policy Chunks).
    *   The User's Question.
2.  **Ollama Inference**: The prompt is sent to `http://localhost:11434/api/generate` (Model: `gemma3:4b`).
3.  **Response**: The generated text is returned.

### Step 5: Persistence
1.  The User's question is saved to `DynamoConversationMemory`.
2.  The AI's Answer (or the Handoff message) is saved to Memory.

---

## 3. Key Services

### `LocalLlmService`
- **Role**: Interface to the Ollama server.
- **Model**: `gemma3:4b`.
- **Endpoint**: `POST /api/generate`.
- **Function**: Handles both chat generation and intent classification (if enabled).

### `PolicyRagService`
- **Role**: Manages the Knowledge Base.
- **Indexing**:
    1.  Scans AWS S3 for documents.
    2.  Chunks text into 1000-character segments.
    3.  Generates embeddings via `EmbeddingService`.
    4.  Stores Vectors + Text in DynamoDB.
- **Retrieval**: vector similarity search to find relevant policy info.

### `HybridContextService`
- **Role**: The "Brain" that decides *what* the AI should know.
- **Logic**: Combines History + RAG + Heuristics (Frustration/Confusion analysis).

### `TicketService`
- **Role**: Manages Support Tickets.
- **Storage**: DynamoDB.
- **Function**: Creates tickets (`TKT-XXXX`) when the AI cannot handle a request or when requested by the user.

---

## 4. Data Flow Diagram

```mermaid
sequenceDiagram
    participant User
    participant API as ChatController
    participant Hybrid as HybridContextService
    participant RAG as PolicyRagService
    participant DB as DynamoDB
    participant LLM as Ollama (Gemma)

    User->>API: Send Message
    
    API->>API: Check Greeting / TicketID / Explicit Handoff
    alt Quick Reply
        API->>User: Return Greeting or Ticket Status
    end

    API->>Hybrid: BuildContext(Message)
    Hybrid->>DB: Fetch History
    Hybrid->>RAG: Search Policy (Vector Search)
    RAG-->>Hybrid: Return Relevant Chunks
    Hybrid-->>API: Context + Flags (Frustrated/LowConf)

    alt Frustrated OR Low Confidence
        API->>DB: Create Ticket
        API->>User: "Connecting you to agent..."
    else Standard Flow
        API->>LLM: Generate(Prompt + Context)
        LLM-->>API: Generated Answer
        API->>DB: Save Conversation
        API->>User: Return Answer
    end
```
