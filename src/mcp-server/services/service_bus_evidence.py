"""Service Bus evidence and repair helpers.

Uses the fully-qualified namespace + DefaultAzureCredential (managed identity in
Azure, developer credentials locally) instead of a connection string, per the
project's zero-trust / no-secrets-in-config posture.
"""
import os
from typing import Any

from azure.identity import DefaultAzureCredential
from azure.servicebus import ServiceBusClient, ServiceBusMessage
from azure.servicebus.management import ServiceBusAdministrationClient

_credential = DefaultAzureCredential()


def _fqdn() -> str:
    fqdn = os.environ.get("SERVICEBUS_FQDN")
    if not fqdn:
        raise RuntimeError("SERVICEBUS_FQDN app setting is not configured")
    return fqdn


def peek_dead_letter(queue: str, count: int) -> list[dict[str, Any]]:
    """Peek (non-destructive) up to `count` messages from a queue's dead-letter sub-queue."""
    with ServiceBusClient(_fqdn(), _credential) as client:
        with client.get_queue_receiver(
            queue_name=queue, sub_queue="deadletter"
        ) as receiver:
            messages = receiver.peek_messages(max_message_count=count)
            return [
                {
                    "messageId": m.message_id,
                    "body": b"".join(m.body).decode("utf-8", errors="replace"),
                    "deadLetterReason": m.dead_letter_reason,
                    "deadLetterErrorDescription": m.dead_letter_error_description,
                    "enqueuedTimeUtc": m.enqueued_time_utc.isoformat()
                    if m.enqueued_time_utc
                    else None,
                    "deliveryCount": m.delivery_count,
                }
                for m in messages
            ]


def replay_messages(queue: str, count: int) -> dict[str, Any]:
    """Move up to `count` messages from the dead-letter sub-queue back onto the main queue.

    Receives (locks) each dead-lettered message, resends its body to the main
    queue, then completes (removes) the original dead-letter message. This is
    idempotent per-message: if resend succeeds but complete fails, the message
    remains in the DLQ and will simply be retried on the next repair attempt.
    """
    replayed = 0
    errors: list[str] = []

    with ServiceBusClient(_fqdn(), _credential) as client:
        with (
            client.get_queue_receiver(queue_name=queue, sub_queue="deadletter") as receiver,
            client.get_queue_sender(queue_name=queue) as sender,
        ):
            messages = receiver.receive_messages(max_message_count=count, max_wait_time=5)
            for message in messages:
                try:
                    body = b"".join(message.body)
                    sender.send_messages(ServiceBusMessage(body))
                    receiver.complete_message(message)
                    replayed += 1
                except Exception as exc:  # noqa: BLE001 - report back to the agent, don't crash the tool
                    errors.append(f"{message.message_id}: {exc}")
                    receiver.abandon_message(message)

    return {"replayed": replayed, "errors": errors}


def check_dlq_depth(queue: str) -> dict[str, Any]:
    """Return current active and dead-letter message counts for a queue."""
    with ServiceBusAdministrationClient(_fqdn(), _credential) as admin_client:
        props = admin_client.get_queue_runtime_properties(queue)
        return {
            "queue": queue,
            "activeMessageCount": props.active_message_count,
            "deadLetterMessageCount": props.dead_letter_message_count,
            "totalMessageCount": props.total_message_count,
        }
