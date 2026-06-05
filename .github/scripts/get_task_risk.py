#!/usr/bin/env python3
"""
task JSON의 impact.risk에 따라 사용할 Claude 모델을 결정한다.

사용법:
  python get_task_risk.py --branch <브랜치명>

출력:
  MODEL=claude-sonnet-4-6   (stdout, 환경변수 형식)

매핑:
  LOW  → claude-haiku-4-5-20251001
  MEDIUM (기본값) → claude-sonnet-4-6
  HIGH → claude-opus-4-8

task JSON이 없거나 impact가 null이면 기본값(sonnet) 사용.
"""

import argparse
import json
import sys
from pathlib import Path
from typing import Optional

REPO_ROOT = Path(__file__).resolve().parent.parent.parent

MODEL_MAP = {
    "LOW": "claude-haiku-4-5-20251001",
    "MEDIUM": "claude-sonnet-4-6",
    "HIGH": "claude-opus-4-8",
}
DEFAULT_MODEL = "claude-sonnet-4-6"


def find_task_json(branch: str) -> Optional[Path]:
    tasks_dir = REPO_ROOT / "AI" / "tasks"
    if not tasks_dir.exists():
        return None
    for f in tasks_dir.glob("*.json"):
        try:
            data = json.loads(f.read_text(encoding="utf-8"))
            if data.get("branch") == branch:
                return f
        except Exception:
            continue
    return None


def get_model_for_branch(branch: str) -> str:
    task_file = find_task_json(branch)
    if task_file is None:
        return DEFAULT_MODEL

    try:
        data = json.loads(task_file.read_text(encoding="utf-8"))
        impact = data.get("impact")
        if not impact:
            return DEFAULT_MODEL
        risk = str(impact.get("risk", "MEDIUM")).upper()
        return MODEL_MAP.get(risk, DEFAULT_MODEL)
    except Exception:
        return DEFAULT_MODEL


def main() -> None:
    parser = argparse.ArgumentParser(description="LLM 라우터 — impact.risk 기반 모델 선택")
    parser.add_argument("--branch", required=True, help="현재 브랜치명")
    args = parser.parse_args()

    model = get_model_for_branch(args.branch)
    print(f"MODEL={model}")


if __name__ == "__main__":
    main()
