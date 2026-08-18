"""Continuum-Ops MCP Tool Server.

Exposes evidence-gathering and repair tools to the Diagnosis and Verify
Foundry Prompt Agents via the Azure Functions MCP tool trigger
(`app.mcp_tool_trigger`), registered as a remote MCP server at
`/runtime/webhooks/mcp` and consumed through Foundry's Toolbox.

See docs/06-AI-Agent-Implementation.md for the full build guide.
"""
import json
import logging

import azure.functions as func

from services.app_insights_logs import query_application_logs as _query_application_logs
from services.erp_client import query_erp as _query_erp
from services.pattern_search import find_similar_patterns, upsert_pattern as _upsert_pattern
from services.service_bus_evidence import (
    check_dlq_depth as _check_dlq_depth,
    peek_dead_letter,
    replay_messages as _replay_messages,
)

app = func.FunctionApp()


def _args(context: str) -> dict:
    return json.loads(context)["arguments"]


# ---------------------------------------------------------------------------
# Read-only evidence tools (safe for the Diagnosis Agent to call freely)
# ---------------------------------------------------------------------------

_PEEK_DLQ_PROPERTIES = json.dumps([
    {"propertyName": "queue", "propertyType": "string", "description": "Queue name", "isRequired": True},
    {"propertyName": "count", "propertyType": "integer", "description": "Number of messages to peek (max 20)", "isRequired": False},
])


@app.mcp_tool_trigger(
    arg_name="context",
    tool_name="peek_dlq_messages",
    description="Peek up to N messages from a Service Bus dead-letter queue without removing them.",
    tool_properties=_PEEK_DLQ_PROPERTIES,
)
def peek_dlq_messages(context: str) -> str:
    args = _args(context)
    count = min(int(args.get("count", 5)), 20)
    logging.info("MCP tool peek_dlq_messages invoked for queue=%s count=%s", args["queue"], count)
    return json.dumps(peek_dead_letter(args["queue"], count))


_QUERY_LOGS_PROPERTIES = json.dumps([
    {"propertyName": "kqlQuery", "propertyType": "string", "description": "Read-only KQL query against Application Insights", "isRequired": True},
    {"propertyName": "lookbackMinutes", "propertyType": "integer", "description": "How far back to search, in minutes", "isRequired": False},
])


@app.mcp_tool_trigger(
    arg_name="context",
    tool_name="query_application_logs",
    description="Run a read-only KQL query against Application Insights to gather diagnostic evidence.",
    tool_properties=_QUERY_LOGS_PROPERTIES,
)
def query_application_logs(context: str) -> str:
    args = _args(context)
    logging.info("MCP tool query_application_logs invoked")
    rows = _query_application_logs(args["kqlQuery"], int(args.get("lookbackMinutes", 60)))
    return json.dumps(rows, default=str)


_SEARCH_PATTERNS_PROPERTIES = json.dumps([
    {"propertyName": "errorSignature", "propertyType": "string", "description": "Normalized error signature or message text", "isRequired": True},
    {"propertyName": "tenantId", "propertyType": "string", "description": "Tenant identifier to scope the search", "isRequired": True},
])


@app.mcp_tool_trigger(
    arg_name="context",
    tool_name="search_similar_patterns",
    description="Vector/text search Azure AI Search for previously learned incident patterns similar to the given error signature.",
    tool_properties=_SEARCH_PATTERNS_PROPERTIES,
)
def search_similar_patterns(context: str) -> str:
    args = _args(context)
    logging.info("MCP tool search_similar_patterns invoked for tenant=%s", args["tenantId"])
    matches = find_similar_patterns(args["errorSignature"], args["tenantId"], top_k=5)
    return json.dumps(matches, default=str)


_CHECK_DLQ_DEPTH_PROPERTIES = json.dumps([
    {"propertyName": "queue", "propertyType": "string", "description": "Queue name", "isRequired": True},
])


@app.mcp_tool_trigger(
    arg_name="context",
    tool_name="check_dlq_depth",
    description="Check the current active and dead-letter message counts for a Service Bus queue (used by the Verify Agent).",
    tool_properties=_CHECK_DLQ_DEPTH_PROPERTIES,
)
def check_dlq_depth(context: str) -> str:
    args = _args(context)
    logging.info("MCP tool check_dlq_depth invoked for queue=%s", args["queue"])
    return json.dumps(_check_dlq_depth(args["queue"]))


_QUERY_ERP_PROPERTIES = json.dumps([
    {"propertyName": "path", "propertyType": "string", "description": "ERP API path, e.g. /orders/{id}/status", "isRequired": True},
])


@app.mcp_tool_trigger(
    arg_name="context",
    tool_name="query_erp",
    description="Query the customer ERP system (read-only) via API Management to confirm business outcomes.",
    tool_properties=_QUERY_ERP_PROPERTIES,
)
def query_erp(context: str) -> str:
    args = _args(context)
    logging.info("MCP tool query_erp invoked for path=%s", args["path"])
    return json.dumps(_query_erp(args["path"]))


# ---------------------------------------------------------------------------
# Mutating tools (only called by Repair/Verify workflow after policy approval)
# ---------------------------------------------------------------------------

_REPLAY_MESSAGES_PROPERTIES = json.dumps([
    {"propertyName": "queue", "propertyType": "string", "description": "Queue name", "isRequired": True},
    {"propertyName": "count", "propertyType": "integer", "description": "Max number of messages to replay", "isRequired": False},
])


@app.mcp_tool_trigger(
    arg_name="context",
    tool_name="replay_messages",
    description="Move up to N messages from a queue's dead-letter sub-queue back onto the main queue. Mutating — only call after repair-plan approval.",
    tool_properties=_REPLAY_MESSAGES_PROPERTIES,
)
def replay_messages(context: str) -> str:
    args = _args(context)
    count = min(int(args.get("count", 10)), 50)
    logging.info("MCP tool replay_messages invoked for queue=%s count=%s", args["queue"], count)
    return json.dumps(_replay_messages(args["queue"], count))


_UPSERT_PATTERN_PROPERTIES = json.dumps([
    {"propertyName": "pattern", "propertyType": "object", "description": "Pattern document: patternId, tenantId, rootCause, repairAction, resolutionCount, lastSeenUtc", "isRequired": True},
])


@app.mcp_tool_trigger(
    arg_name="context",
    tool_name="upsert_pattern",
    description="Persist or update a learned incident pattern after a verified repair. Mutating — only call from the Verify Agent.",
    tool_properties=_UPSERT_PATTERN_PROPERTIES,
)
def upsert_pattern(context: str) -> str:
    args = _args(context)
    logging.info("MCP tool upsert_pattern invoked for patternId=%s", args["pattern"].get("patternId"))
    return json.dumps(_upsert_pattern(args["pattern"]))
