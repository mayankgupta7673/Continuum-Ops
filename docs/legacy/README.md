# Legacy / Superseded Approaches

This folder holds documentation for approaches that were **once considered or implemented but are no longer the recommended path**. Nothing here should be used as a starting point for new work — it's kept only for historical context and as a fallback reference if a future requirement forces a return to a lower-level pattern.

| Document | Why it's here |
|---|---|
| [06-AI-Agent-Implementation-AssistantsAPI-Legacy.md](06-AI-Agent-Implementation-AssistantsAPI-Legacy.md) | Original agent implementation guide built on the lower-level Azure AI **Assistants API** (manual thread/run polling, beta SDKs). Superseded by **Microsoft Foundry Agent Service Prompt Agents** — see [../06-AI-Agent-Implementation.md](../06-AI-Agent-Implementation.md) for the current approach. Retained as a reference for the thread/run/tool-call semantics that still apply if a **Hosted Agent** (custom code) path is ever adopted. |

For the reasoning behind these changes, see [../08-AIOps-Solution-Architecture-Review.md](../08-AIOps-Solution-Architecture-Review.md).
