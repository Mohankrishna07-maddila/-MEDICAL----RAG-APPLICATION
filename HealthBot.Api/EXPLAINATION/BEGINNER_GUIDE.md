# How the HealthBot Works (A Beginner's Guide)

Imagine the HealthBot is a **smart front-desk receptionist** for a hospital. Here is exactly what happens, step-by-step, when you talk to it.

---

### Step 1: The "Instant Reply" (Reflexes)
When you say something, the receptionist (Bot) checks a "Cheat Sheet" first effectively immediately.
*   **Greetings**: If you say "Hi" or "Hello", it immediately says "Hello!" back. It doesn't need to check any big books or think hard for this.
*   **Ticket Check**: If you give it a ticket number (like `TKT-1234`), it instantly checks the computer to see the status of that ticket.
*   **"I want a human"**: If you explicitly say "I want to talk to an agent", it stops everything and opens a support ticket for you right away.

### Step 2: The "Library Search" (RAG - Retrieval Augmented Generation)
If you ask a real question (like *"Does my policy cover eye surgery?"*), the receptionist knows it cannot just guess the answer.

1.  **The Security Check (VIP Access)**: First, it looks at your ID badge.
    *   **Global User**: Can only see "General Info" books (FAQs, Brochures).
    *   **Specific User (Priya)**: Can see "General Info" AND "Priya's Personal File".
    *   *Result*: It ensures Priya never sees Rahul's file, and "Guests" never see any private files.

2.  **The Shelf Search**: It goes to the relevant shelf and looks for documents with words like "eye surgery".

3.  **The Smart Sorting (Ranking)**:
    *   It finds a generic FAQ saying "Some plans cover vision."
    *   It finds Priya's policy saying "Gold Plan covers $500 vision."
    *   **Boost**: It knows Priya's policy is more important, so it puts that page ON TOP.


### Step 3: The "Thinking" (The AI/LLM)
Now the receptionist sits down with the pages it found.
1.  It reads your question: *"Does my policy cover eye surgery?"*
2.  It reads the pages it found: *"Policy Section 4: Vision is covered up to $500..."*
3.  It formulates a polite answer in plain English: *"Yes, your policy covers eye surgery up to $500."*
4.  **Citations**: It adds a small note saying where it found the info (e.g., *"[Source: Gold Plan v2]"*).
    *   *Important: It is strictly told NOT to make things up and to always cite its sources.*

### Step 4: The "Frustration Check" (Safety Net)
Before answering, the receptionist checks how the conversation is going.
*   **Are you angry?** (Did you say "stupid", "broken", or "this is useless"?)
*   **Is it confused?** (Did the library search fail completely?)

If either of these things is true, the receptionist apologizes: *"I'm having trouble helping you. Let me connect you to a human supervisor,"* and gives you a ticket number.

### Step 5: The Reply
Finally, the receptionist speaks the answer to you.

---

### Summary Checklist
1.  **You speak.**
2.  **Bot checks Quick Rules** (Hi? Ticket? Agent?).
3.  **Bot searches the Library** (Finds policy wording).
4.  **Bot thinks** (Reads wording + Your Question).
5.  **Bot replies** (Or calls for help if stuck).
