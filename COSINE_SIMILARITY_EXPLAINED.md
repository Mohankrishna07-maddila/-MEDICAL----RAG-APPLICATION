# Understanding Cosine Similarity & Embeddings

You asked: *"What is the use of cosine similarity and how does it check similarity?"*

Here is the simple explanation using the **Compass Analogy**.

---

## 1. The "Magic Numbers" (Embeddings)

Computers cannot understand words like "Apple" or "Doctor". They only understand numbers.
So, we use an AI (Embedding Model) to turn every sentence into a list of numbers, called a **Vector**.

Imagine a **Compass**.
*   If I say "North", that is a direction.
*   If I say "North-East", that is a different direction.

In our AI world:
*   **"King"** might point North. ☁️
*   **"Queen"** might point North-North-East (Very close direction). 👸
*   **"Apple"** might point South (Completely different direction). 🍎

The "List of Numbers" (Vector) just describes these coordinates.

---

## 2. Cosine Similarity (The Angle)

**Cosine Similarity** is just a fancy way of measuring the **Angle** between two arrows.

*   **Score 1.0 (Perfect Match)**: The arrows point in the **exact same direction**.
    *   Example: "Hello" vs "Hello"
*   **Score ~0.8 (Very Similar)**: The arrows are very close.
    *   Example: "The dog barked" vs "The puppy made noise"
*   **Score 0.0 (Unrelated)**: The arrows point in completely different directions (90 degrees).
    *   Example: "I like Pizza" vs "The sky is blue"

### Why "Cosine"?
In math, the "Cosine" of an angle of 0 degrees is 1. The Cosine of 90 degrees is 0. That's why we use it! It gives us a nice score from 0 to 1.

---

## 3. How It Works in Your Project

In your `PolicyRagService.cs`, this is exactly what happens:

### Step A: Turn User Question into an Arrow
User asks: *"Does this cover eye surgery?"*
The Embedding Service turns this into a vector (an arrow pointing in a specific direction in distinct "meaning space").

### Step B: Compare with Repository Arrows
Your database (`DynamoVectorRepository`) is full of arrows representing every paragraph of your insurance policy.
*   **Paragraph A** ("Dental Care") points West.
*   **Paragraph B** ("Vision and Eye Procedures") points North-East.
*   **Paragraph C** ("Maternity Leave") points South.

### Step C: The Calculation
The code runs a loop (conceptually) comparing your Question Arrow to every Paragraph Arrow.

1.  **Question vs Dental**: Score 0.1 (Too wide)
2.  **Question vs Vision**: Score **0.85** (Very close angle!) ✅
3.  **Question vs Maternity**: Score 0.05 (Unrelated)

### Step D: The Threshold (0.45)
Your code has a specific line:
```csharp
.Where(x => x.Score > 0.45f)
```
This means: "If the arrows aren't pointing at least somewhat in the same direction (Score > 0.45), ignore them."

We only keep the matches that are "Close enough".

## Summary
*   **Embeddings**: Converting text into a directional arrow (Vector).
*   **Cosine Similarity**: Measuring how close two arrows are to each other.
*   **The Check**: If the score is high (close to 1), the AI knows the text contains the answer.
