"""Client for the customer's ERP system, fronted by API Management.

Uses an on-behalf-of / managed-identity access token scoped to the ERP API
app registration rather than a static API key.
"""
import os
from typing import Any

import requests
from azure.identity import DefaultAzureCredential

_credential = DefaultAzureCredential()


def query_erp(path: str, params: dict[str, Any] | None = None) -> dict[str, Any]:
    """Call a read-only ERP API endpoint (e.g. `/customers/{id}`, `/orders/{id}/status`)."""
    base_url = os.environ.get("ERP_API_BASE_URL")
    scope = os.environ.get("ERP_API_SCOPE")
    if not base_url or not scope:
        raise RuntimeError("ERP_API_BASE_URL / ERP_API_SCOPE app settings are not configured")

    token = _credential.get_token(scope).token
    response = requests.get(
        f"{base_url.rstrip('/')}/{path.lstrip('/')}",
        headers={"Authorization": f"Bearer {token}"},
        params=params,
        timeout=15,
    )
    response.raise_for_status()
    return response.json()
