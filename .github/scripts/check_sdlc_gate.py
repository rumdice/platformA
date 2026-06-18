# ARCHIVED: Sprint #67(2026-06-18)에서 sdlc-gate-check.yml과 함께 비활성화됨.
# gate-check 메커니즘은 인프라 의존성 문제로 제거되었다 — AI/docs/gate-check-future.md 참조.
# 이 파일의 로직은 향후 재도입 시 그대로 재사용 가능하다.
"""
AI_SDLC Gate Check 스크립트.

GitHub Actions 'AI_SDLC Gate Check' 워크플로우에서 호출된다.
PR의 head branch로 AI/tasks/*.json을 탐색하여 test/review/impact 게이트를 검사한다.

환경변수:
  BRANCH         - PR head branch 이름
  CHANGED_FILES  - PR에서 변경된 파일 목록 (줄바꿈 구분)
  GITHUB_STEP_SUMMARY - GitHub Actions Job Summary 파일 경로 (있으면 결과 기록)

종료 코드:
  0 - PASS 또는 WARNING
  1 - FAIL
"""

import json
import os
import sys
from pathlib import Path

BRANCH = os.environ.get("BRANCH", "")
CHANGED_FILES_RAW = os.environ.get("CHANGED_FILES", "")
GITHUB_STEP_SUMMARY = os.environ.get("GITHUB_STEP_SUMMARY", "")

# task JSON(LLM /impact 결과)이 없는 경우에만 사용하는 fallback 판단 기준.
# task JSON이 있으면 impact.risk 필드를 신뢰하고 이 패턴을 사용하지 않는다.
HIGH_RISK_PATTERNS = [
    "PlatformA.Library/",
    "Migrations/",
    "DbContext",
    "Entities/",
    "Auth",
    "Token",
    "Jwt",
    "Redis",
    "Lock",
]

CODE_EXTENSIONS = {".cs", ".proto", ".csproj"}


def find_task_file(branch: str) -> Path | None:
    tasks_dir = Path("AI/tasks")
    if not tasks_dir.exists():
        return None
    for json_file in sorted(tasks_dir.glob("sprint*.json")):
        try:
            data = json.loads(json_file.read_text(encoding="utf-8"))
            if data.get("branch") == branch:
                return json_file
        except Exception:
            continue
    return None


def parse_changed_files() -> list[str]:
    if not CHANGED_FILES_RAW:
        return []
    return [f.strip() for f in CHANGED_FILES_RAW.splitlines() if f.strip()]


def has_code_changes(files: list[str]) -> bool:
    return any(Path(f).suffix in CODE_EXTENSIONS for f in files)


def has_high_risk_changes(files: list[str]) -> bool:
    for f in files:
        for pattern in HIGH_RISK_PATTERNS:
            if pattern.lower() in f.lower():
                return True
    return False


def write_summary(lines: list[str]) -> None:
    if not GITHUB_STEP_SUMMARY:
        return
    try:
        with open(GITHUB_STEP_SUMMARY, "a", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
    except Exception as e:
        print(f"[warn] summary 기록 실패: {e}")


def main() -> None:
    if not BRANCH:
        print("[error] BRANCH environment variable not set")
        sys.exit(1)

    print(f"AI_SDLC Gate Check — branch: {BRANCH}")

    changed_files = parse_changed_files()
    code_changed = has_code_changes(changed_files)
    high_risk = has_high_risk_changes(changed_files)

    warnings: list[str] = []
    failures: list[str] = []

    # task JSON 탐색
    task_file = find_task_file(BRANCH)
    if task_file is None:
        # task JSON 없음: 코드 변경 유무에 따라 FAIL / WARNING 분기
        if code_changed and high_risk:
            failures.append(
                f"task JSON not found for branch '{BRANCH}': "
                "고위험 코드 변경 PR은 /plan 스킬로 task JSON을 먼저 생성해야 합니다."
            )
        elif code_changed:
            warnings.append(
                f"task JSON not found for branch '{BRANCH}' "
                "(코드 변경 있음 — /plan 실행 권장)"
            )
        # else: 코드 변경 없음 → 핫픽스/문서 PR, 경고 없이 통과
        task_data = {}
    else:
        task_data = json.loads(task_file.read_text(encoding="utf-8"))

    test_generated = task_data.get("test_generated", None)
    review_completed = task_data.get("review_completed", None)
    adr_required = task_data.get("adr_required", False)
    impact = task_data.get("impact", None)
    impact_risk = impact.get("risk", "UNKNOWN") if isinstance(impact, dict) else None
    steps = task_data.get("steps", [])
    requirement_done = any(
        s.get("name") == "requirement" and s.get("status") == "done"
        for s in steps
    )

    # 게이트 검사 (코드 변경 없으면 전체 pass)
    if code_changed and task_file is not None:
        # 검사 0: adr_required — /requirement DESIGN_REVIEW에서 신규 ADR 필요 판정
        if adr_required is True:
            failures.append(
                "adr_required == true: DESIGN_REVIEW에서 신규 ADR이 필요하다고 판정되었습니다. "
                "'/adr {주제}' 실행 후 task JSON의 adr_required를 false로 변경하세요."
            )

        # 검사 0.5: /requirement 미실행 경고 (코드 변경 시)
        if not requirement_done:
            warnings.append(
                "/requirement 미실행: DESIGN_REVIEW(ADR 검토)가 수행되지 않았습니다. "
                "아키텍처 결정이 누락되었을 수 있습니다."
            )

        # 검사 1: test_generated
        if test_generated is False:
            failures.append("test_generated == false: 코드 변경이 있지만 /test-gen이 실행되지 않았습니다.")

        # 검사 2: impact (고위험 경로 → FAIL, 일반 → WARNING)
        if impact is None:
            if high_risk:
                failures.append("impact == null: 고위험 경로 변경이 있지만 /impact가 실행되지 않았습니다.")
            else:
                warnings.append("impact == null: 코드 변경이 있지만 /impact가 실행되지 않았습니다.")

        # 검사 3: review_completed (고위험 조건 시 FAIL)
        # task JSON이 있으므로 LLM의 /impact 판단(impact_risk)을 신뢰한다.
        # HIGH_RISK_PATTERNS는 사용하지 않는다.
        needs_review = (
            (isinstance(impact_risk, str) and impact_risk.upper() in ("HIGH", "MEDIUM"))
            or len(changed_files) > 10
        )
        if needs_review and review_completed is False:
            failures.append(
                "review_completed == false: 고위험 변경이 있지만 /review가 실행되지 않았습니다."
            )

    # 결과 결정
    if failures:
        result = "FAIL"
        exit_code = 1
    elif warnings:
        result = "WARNING"
        exit_code = 0
    else:
        result = "PASS"
        exit_code = 0

    # 콘솔 출력
    print(f"\n{'='*50}")
    print(f"Branch:           {BRANCH}")
    print(f"Task file:        {task_file or '(없음)'}")
    print(f"Code changed:     {'yes' if code_changed else 'no'}")
    print(f"High-risk paths:  {'yes' if high_risk else 'no'}")
    print(f"requirement_done: {'yes' if requirement_done else 'no'}")
    print(f"adr_required:     {adr_required}")
    print(f"test_generated:   {test_generated}")
    print(f"impact risk:      {impact_risk or '(null)'}")
    print(f"review_completed: {review_completed}")
    print(f"\nResult: {result}")
    if warnings:
        for w in warnings:
            print(f"  [warn] {w}")
    if failures:
        for f in failures:
            print(f"  [fail] {f}")
    print(f"{'='*50}\n")

    # GitHub Actions Job Summary
    summary_lines = [
        "## AI_SDLC Gate Check",
        "",
        f"| 항목 | 값 |",
        f"|------|-----|",
        f"| Branch | `{BRANCH}` |",
        f"| Task file | `{task_file or '없음'}` |",
        f"| Code changed | {'✅ yes' if code_changed else '➖ no'} |",
        f"| High-risk paths | {'⚠️ yes' if high_risk else '➖ no'} |",
        f"| test_generated | {'✅' if test_generated else ('❌' if test_generated is False else '➖ null')} |",
        f"| impact risk | {impact_risk or '➖ null'} |",
        f"| review_completed | {'✅' if review_completed else ('❌' if review_completed is False else '➖ null')} |",
        "",
        f"### Result: {'✅ PASS' if result == 'PASS' else ('⚠️ WARNING' if result == 'WARNING' else '❌ FAIL')}",
    ]
    if warnings:
        summary_lines.append("\n**Warnings:**")
        for w in warnings:
            summary_lines.append(f"- {w}")
    if failures:
        summary_lines.append("\n**Failures:**")
        for f in failures:
            summary_lines.append(f"- {f}")

    write_summary(summary_lines)

    sys.exit(exit_code)


if __name__ == "__main__":
    main()
