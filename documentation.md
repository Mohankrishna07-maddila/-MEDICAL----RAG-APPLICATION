# HealthBot Project Documentation

## 1. Project Overview & Introduction
**HealthBot** is an advanced, AI-powered customer support assistant designed for the health insurance domain. Unlike standard chatbots, it combines **Local LLMs** (Large Language Models) with **RAG** (Retrieval Augmented Generation) to provide accurate, policy-based answers while maintaining data privacy.

### Key Features
- **Semantic Search**: Understands the *meaning* of user queries, not just keywords.
- **RAG Architecture**: Grounds answers in a verified Knowledge Base (`policy.txt`) to prevent hallucinations.
- **Automated Ticket Management**: Detects user frustration or complex requests and automatically creates support tickets in DynamoDB.
- **Local AI**: Runs entirely on your machine using **Ollama** and **Gemma Models**, ensuring no data leaves your network.

---

## 2. Tech Stack & Dependencies
This project uses a modern, high-performance stack optimized for local development and scalability.

### Core Frameworks
- **Backend**: .NET 8 (C#) ASP.NET Core Web API.
- **Frontend**: Blazor WebAssembly (C#) for a responsive, single-page application experience.

### Data & AI
- **Database**: AWS DynamoDB (running locally or in cloud) for high-speed NoSQL storage of tickets and sessions.
- **AI Engine**: [Ollama](https://ollama.com/) running the `gemma3:4b` model.
- **Vector Search**: Custom in-memory vector store using pre-computed embeddings and Cosine Similarity.

### Tools
- **IDE**: VS Code (recommended).
- **Testing**: Postman / Browser DevTools.
- **CLI Tools**: AWS CLI (for database management).

---

## 3. Getting Started (Beginner Guide)
Follow these steps to get the project running on your local machine.

### Prerequisites
1. **Install .NET 8 SDK**: Download from the [Microsoft website](https://dotnet.microsoft.com/download).
2. **Install Ollama**: Download from [ollama.com](https://ollama.com).
3. **Pull the Model**: Open your terminal and run:
   ```powershell
   ollama pull gemma3:4b
   ```

### Setup Instructions
1. **Clone/Open the Project**: Navigate to the `HealthBot` folder.
2. **Start the Backend**:
   - Open a terminal in `HealthBot.Api`.
   - Run: `dotnet run`
   - The API will start (usually at `http://localhost:5000` or similar).
3. **Start the Frontend**:
   - Open a new terminal in `HealthBot.Ui`.
   - Run: `dotnet run`
   - Open the provided URL (e.g., `http://localhost:5001`) in your browser.
4. **Chat**: You can now say "Hi" or ask "What is my coverage?"

---

## 4. Architecture Deep Dive (Pro Level)
This section explains how the code actually works under the hood.

### High-Level Flow
`User` -> `Blazor UI` -> `ChatController (API)` -> `HybridContextService` -> `LLM/RAG` -> `Response`

### Backend Components (`HealthBot.Api`)

#### 1. The Brain: `ChatController.cs`
This is the entry point for all messages. It follows a strict **Decision Tree**:
1. **Greeting Check**: If the user says "Hi", it short-circuits to avoid wasting AI resources.
2. **Ticket Status**: Checks if the user is asking about an existing ticket (e.g., "Status of TKT-123").
3. **Explicit Agent Request**: If the user asks to "talk to human", it calls `TicketService`.
4. **Hybrid Context Build**: If none of the above, it calls `HybridContextService` to gather RAG data and history.
5. **Frustration Detection**: If the user seems angry or is repeating themselves, it overrides the AI and opens a ticket.
6. **AI Generation**: Finally, it prompts the Local LLM to generate an answer.

#### 2. The Knowledge: `PolicyRagService.cs`
- **Ingestion**: On startup, it reads `policy.txt` and splits it into small "chunks".
- **Embeddings**: Each chunk is converted into a vector (a list of numbers representing meaning) using a local embedding model.
- **Retrieval**: When you ask a question, we compare your question's vector to the chunk vectors using **Cosine Similarity**. If the score is > 0.45, we accept the chunk as relevant context.

#### 3. The Engine: `LocalLlmService.cs`
- Connects to your running Ollama instance via HTTP (`localhost:11434`).
- Sends the `System Prompt` (rules) + `Context` (policy) + `User Question`.
- cleaning: It strips out Markdown (like ```json) to ensure the frontend gets clean text.

#### 4. The Memory: `DynamoConversationMemory.cs`
- Stores every message in DynamoDB.
- Allows the bot to remember what you said 2 minutes ago.

### Frontend Components (`HealthBot.Ui`)

#### `UserChat.razor`
- The main chat interface.
- **State**: Manages the list of messages (`messages` list).
- **Typing Indicator**: Shows a CSS animation while waiting for the API.
- **Auto-Scroll**: Uses JS Interop (`scrollToBottom`) to keep the latest message visible.

---

## 5. Interesting Workflows & Logic

### The "Frustration Handoff"
**File**: `ChatController.cs` (Lines 120-136)
**Logic**: We calculate a "Frustration Score" in `HybridContextService`. If the user has asked the same thing 3 times or used negative sentiment words, the bot *refuses* to answer and instead says:
> "I see we're going in circles... I've created a ticket (ID: TKT-XYZ) and connected you to a human agent."
**Why?**: This prevents the "AI Loop of Death" where bots keep giving the same useless answer.

### Identity Protection
**File**: `PolicyRagService.cs` (System Prompt)
**Logic**: The prompt explicitly forbids the AI from saying "I am Gemma" or "I was made by Google".
**Rule**:
```text
IDENTITY RULES (MANDATORY):
- You are NOT an AI... created by anyone.
- You exist ONLY as a product feature of this insurance app.
```
This ensures the bot stays "in character" as a helpful employee of the insurance company.

---

## 6. Performance of RAG Application
- **Latency**: Since we use a small local model (`gemma3:4b`), responses typically take 1-3 seconds on a standard laptop. This is faster than many cloud APIs.
- **Accuracy**: By setting a strict similarity threshold (0.45), we ensure the bot says "I don't know" rather than making things up.
- **Cost**: **Zero**. Since it runs locally, you pay no API fees to OpenAI or AWS.

---

## 7. Lessons Learned (Mistakes & Fixes)
During development, we encountered several challenges. Here is how we overcame them:

### Mistake 1: The "Static Response" Loop
**Issue**: The bot started answering *every* question with a generic "To file a claim, please use the app".
**Cause**: We had hardcoded a logic trap in `ChatController` that didn't properly hand off to the LLM after checking for tickets.
**Fix**: We rewrote the `ChatController` flow to ensure that if no special condition (Ticket/Greeting) is met, it *always* falls through to the `_ai.GenerateAsync` call.

### Mistake 2: Losing the "Talk to Agent" Feature
**Issue**: During a refactor, we accidentally deleted the `CreateTicketAsync` method in `TicketService`, causing compilation errors.
**Fix**: We restored the method and added a "Wrapper" in `TicketService` to ensure compatibility with the Controller's expected arguments (`sessionId`, `message`).

### Mistake 3: Cloud vs. Local Costs
**Issue**: Initially, we planned to use AWS Bedrock. However, for a personal/portfolio project, the cost and setup complexity were too high.
**Fix**: We pivoted to **Ollama**. We created an interface `IAIService` so we could swap out `AwsBedrockService` for `LocalLlmService` without changing the rest of the app. This is a perfect example of **Dependency Injection**.

---

## Conclusion
This documentation covers the full lifecycle of the HealthBot project. It is designed to be a living document, so as you add new features (like Voice support or Mobile UI), simply add a new section!
