#!/usr/bin/env bash
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y --no-install-recommends python3.12 python3.12-venv ca-certificates >/tmp/openhands-apt.log
python3.12 -m venv /tmp/openhands-test
/tmp/openhands-test/bin/python -m pip install --disable-pip-version-check --require-hashes --only-binary=:all: -r /pilot/src/CSweet.Agent.OpenHands/worker/build-requirements.lock >/tmp/openhands-build-pip.log
/tmp/openhands-test/bin/python -m pip install --disable-pip-version-check --require-hashes --no-build-isolation -r /pilot/src/CSweet.Agent.OpenHands/worker/requirements-linux.lock >/tmp/openhands-pip.log
/tmp/openhands-test/bin/python -c 'import importlib.metadata as m; from openhands.sdk import Agent, Conversation; from openhands.tools.file_editor import FileEditorTool; from openhands.tools.task_tracker import TaskTrackerTool; assert m.version("openhands-sdk")=="1.56.0"; assert m.version("openhands-tools")=="1.56.0"; print("Pinned Linux Python 3.12 runtime imports passed")'
