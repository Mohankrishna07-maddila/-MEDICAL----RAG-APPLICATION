# Changes Report: Single LLM Call Refactor

This document summarizes the changes made to your codebase to merge Intent Detection and Answer Generation into a single LLM call.

## 1. Core Logic Changes

### `HealthBot.Api/Controllers/ChatController.cs`
- **Before**: Called `_intentDetector` (Step 1) then `_rag` or string literals (Step 2).
- **After**: Calls `_ai.AskWithIntentAsync` once. Returns `{ intent, answer }` directly from the LLM result.

### `HealthBot.Api/LocalLlmService.cs`
- **Modified**: Replaced `AskAsync(string)` with `AskWithIntentAsync(string, history)`.
- **New Feature**: Added a system prompt that enforces strict JSON output (`{ "intent": "...", "answer": "..." }`).
- **Fix**: Added logic to strip Markdown code blocks (```json) from the response before parsing to prevent `JsonException`.

### `HealthBot.Api/IAIService.cs`
- **Modified**: Updated interface signature to match the new `AskWithIntentAsync` method.

## 2. Deleted Files (Cleanup)

The following files were removed as they are no longer needed in the single-call architecture:

- `IntentDetectionService.cs` (Logic merged into LLM prompt)
- `RagService.cs` (Logic merged into LLM prompt)
- `GeminiService.cs` (Incompatible with new interface)
- `MockService.cs` (Incompatible with new interface)

## 3. Configuration Changes

### `HealthBot.Api/Program.cs`
- **Removed**: Registrations for `IntentDetectionService` and `RagService`.
- **Fixed**: Removed duplicate registration of `DynamoConversationMemory`.
