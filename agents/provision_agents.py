"""
Optional code-first provisioning for the Diagnosis Agent and Verify Agent.

Portal-first creation (see instructions.md in each agent folder) is the
recommended path for the POC. Use this script only if you need agent
creation in a CI/CD pipeline.

Verified against the azure-ai-projects "classic" agent creation quickstart
(create_agent(model=, name=, instructions=, tools=)). NOT verified: the exact
tool object for attaching an existing Toolbox/MCP connection by name to a
persisted agent via this SDK -- attach the ContinuumOpsTools connection
through the Foundry portal after running this script, or confirm the correct
tool class (likely something like an MCP/Toolbox tool under
azure.ai.agents.models) against current SDK docs before automating it here.

Requires: pip install -r requirements.txt, az login (or another
DefaultAzureCredential-compatible identity) with the Foundry User role at
the project scope.

Environment variables:
  PROJECT_ENDPOINT      Foundry project endpoint, e.g.
                        https://<account>.services.ai.azure.com/api/projects/<project>
  MODEL_DEPLOYMENT_NAME e.g. "gpt-4o"
"""

import os
from pathlib import Path

from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

AGENTS_DIR = Path(__file__).parent


def load_instructions(agent_folder: str) -> str:
    text = (AGENTS_DIR / agent_folder / "instructions.md").read_text(encoding="utf-8")
    # Extract the fenced ```text ... ``` instructions block.
    start = text.index("```text") + len("```text")
    end = text.index("```", start)
    return text[start:end].strip()


def main() -> None:
    endpoint = os.environ["PROJECT_ENDPOINT"]
    model = os.environ.get("MODEL_DEPLOYMENT_NAME", "gpt-4o")

    project_client = AIProjectClient(endpoint=endpoint, credential=DefaultAzureCredential())

    with project_client:
        diagnosis_agent = project_client.agents.create_agent(
            model=model,
            name="diagnosis-agent",
            instructions=load_instructions("diagnosis-agent"),
            # TODO: attach the ContinuumOpsTools Toolbox connection here once the
            # correct tool class is confirmed; attach manually in the portal for now.
        )
        print(f"Created diagnosis-agent, ID: {diagnosis_agent.id}")

        verify_agent = project_client.agents.create_agent(
            model=model,
            name="verify-agent",
            instructions=load_instructions("verify-agent"),
            # TODO: same as above.
        )
        print(f"Created verify-agent, ID: {verify_agent.id}")

        print()
        print("Set these as app settings on the .NET Functions app:")
        print(f"  DIAGNOSIS_AGENT_ID={diagnosis_agent.id}")
        print(f"  VERIFY_AGENT_ID={verify_agent.id}")


if __name__ == "__main__":
    main()
