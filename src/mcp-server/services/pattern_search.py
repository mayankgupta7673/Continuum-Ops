"""Azure AI Search-backed incident pattern memory.

Patterns are stored in an Azure AI Search index (`incident-patterns`) scoped
per tenant. This scaffold performs a plain text/filter search by default;
swap in a real vector query (`azure.search.documents.models.VectorizedQuery`)
once an embedding model call is wired up for `errorSignature`.
"""
import os
from typing import Any

from azure.identity import DefaultAzureCredential
from azure.search.documents import SearchClient

_credential = DefaultAzureCredential()


def _client() -> SearchClient:
    endpoint = os.environ.get("SEARCH_ENDPOINT")
    index_name = os.environ.get("SEARCH_INDEX_NAME", "incident-patterns")
    if not endpoint:
        raise RuntimeError("SEARCH_ENDPOINT app setting is not configured")
    return SearchClient(endpoint=endpoint, index_name=index_name, credential=_credential)


def find_similar_patterns(error_signature: str, tenant_id: str, top_k: int = 5) -> list[dict[str, Any]]:
    """Find previously-resolved incident patterns similar to `error_signature`.

    NOTE: for production use, replace the `search_text` call below with a
    `VectorizedQuery` against an embedding of `error_signature` (see
    azure-search-documents vector search docs) for true semantic similarity.
    """
    with _client() as client:
        results = client.search(
            search_text=error_signature,
            filter=f"tenantId eq '{tenant_id}'",
            top=top_k,
            select=["patternId", "rootCause", "repairAction", "resolutionCount", "lastSeenUtc"],
        )
        return [dict(r) for r in results]


def upsert_pattern(pattern: dict[str, Any]) -> dict[str, Any]:
    """Create or update a learned pattern document after a verified repair."""
    with _client() as client:
        result = client.merge_or_upload_documents(documents=[pattern])
        return {"succeeded": all(r.succeeded for r in result)}
