"""Application Insights (Log Analytics / KQL) evidence queries."""
import os
from datetime import timedelta
from typing import Any

from azure.identity import DefaultAzureCredential
from azure.monitor.query import LogsQueryClient, LogsQueryStatus

_credential = DefaultAzureCredential()


def query_application_logs(kql_query: str, lookback_minutes: int = 60) -> list[dict[str, Any]]:
    """Run a KQL query against Application Insights and return the top rows.

    `kql_query` should be a scoped, read-only query (e.g. `traces | where ... | take 20`)
    built by the caller — this function does not add its own filtering beyond timespan.
    """
    resource_id = os.environ.get("APPINSIGHTS_RESOURCE_ID")
    if not resource_id:
        raise RuntimeError("APPINSIGHTS_RESOURCE_ID app setting is not configured")

    client = LogsQueryClient(_credential)
    response = client.query_resource(
        resource_id, kql_query, timespan=timedelta(minutes=lookback_minutes)
    )

    if response.status != LogsQueryStatus.SUCCESS:
        return [{"error": str(response.partial_error)}]

    rows: list[dict[str, Any]] = []
    for table in response.tables:
        for row in table.rows:
            rows.append(dict(zip(table.columns, row)))
    return rows
