# The Journey of a Message: "Does my policy cover dental?"

Let's follow a single message as it travels through your code.

**The Message**: *"Does my policy cover dental?"*
**The User**: YOU.

---

### Stop 1: The Gatekeeper (ChatController.cs)
The message arrives at the API. The `ChatController` is the first to see it.
*   **Gatekeeper**: "Is this a greeting (Hi/Hello)?" -> **NO.**
*   **Gatekeeper**: "Is this a Ticket ID (TKT-xxx)?" -> **NO.**
*   **Gatekeeper**: "Is the user asking for a human agent?" -> **NO.**
*   **Gatekeeper**: "Okay, this looks like a real question. I need help. Calling `HybridContextService`!"

---

### Stop 2: The Strategist (HybridContextService.cs)
The Strategist needs to gather information before we can answer.
*   **Strategist**: "Checking History... is the user angry/frustrated?" -> **NO.**
*   **Strategist**: "Is this just a follow-up like 'Tell me more'?" -> **NO.** (It's a specific question about 'Dental').
*   **Strategist**: "Okay, I need Policy facts. Calling `PolicyRagService`!"

---

### Stop 3: The Librarian (PolicyRagService.cs)
The Librarian manages the massive book of insurance rules, now indexed by a specialized catalog (Metadata).
1.  **Translation**: The Librarian calls the Embedding Service to turn *"Does my policy cover dental?"* into numbers (Vector).
2.  **The Index Lookup**: Before scanning books, the Librarian checks the **Metadata Index** (`HealthBot_MetadataIndex`).
    *   *Constraint*: "User is a Customer".
    *   *Result*: "Here is a list of approved Chunk IDs for Customers (ignoring Employee SOPs)."
3.  **The Filtered Search**: It scans ONLY the approved chunks in the **Vector Store** (`VectorStore`).
4.  **The Find & Re-Rank**: It finds matches and re-ranks them based on:
    *   Content Match (Similarity)
    *   Trust Score (Metadata Confidence)
    *   *Match found: "Section 8: Dental Care is NOT covered in the Basic Plan."* (Score: 0.88, Confidence: 1.0 -> Final: 0.91)
5.  **Return**: The Librarian hands this verified paragraph back to the Strategist.

---

### Stop 4: The Construction Site (Back to Controller)
The Controller now has everything it needs:
1.  **User Question**: *"Does my policy cover dental?"*
2.  **Context**: *"Section 8: Dental Care is NOT covered..."*
3.  **System Rules**: *"You are a polite assistant. Do not make things up."*

It packages all of this into a **Prompt**.

---

### Stop 5: The Writer (LocalLlmService / Ollama)
The Controller sends this big package (Prompt) to the AI Brain (Gemma).
*   **The Brain Reads**: "Okay, the user asked about dental. The context says it's NOT covered. I need to be polite."
*   **The Brain Writes**: *"I checked your policy details. Unfortunately, Dental Care is not covered under your current Basic Plan."*

---

### Stop 6: The Archivist (DynamoConversationMemory)
Before sending the answer back:
*   The system saves your question ("Does my policy cover dental?") into the database.
*   The system saves the AI's answer ("Unfortunately...") into the database.
This way, if you ask *"Why not?"* next, it remembers what we just talked about.

### Stop 7: Delivery
The answer appears on your screen.

**End of Journey.**
