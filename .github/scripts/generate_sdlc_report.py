"""
AI_SDLC 주간 리포트 생성 스크립트.

AI/tasks/*.json과 AI/cost-log.md를 읽어 AI/reports/weekly_YYYY-WNN.md를 생성한다.

사용법:
  python3 .github/scripts/generate_sdlc_report.py
"""

import json
import re
import sys
from datetime import date, datetime, timezone
from pathlib import Path


def load_tasks() -> list[dict]:
    tasks_dir = Path("AI/tasks")
    if not tasks_dir.exists():
        return []
    tasks = []
    for f in sorted(tasks_dir.glob("sprint*.json")):
        try:
            data = json.loads(f.read_text(encoding="utf-8"))
            data["_file"] = str(f)
            tasks.append(data)
        except Exception as e:
            print(f"[warn] {f} 읽기 실패: {e}", file=sys.stderr)
    return tasks


def load_cost_log() -> list[str]:
    cost_log = Path("AI/cost-log.md")
    if not cost_log.exists():
        return []
    lines = cost_log.read_text(encoding="utf-8").splitlines()
    return [l for l in lines if l.startswith("|") and "날짜" not in l and "---" not in l]


def week_label() -> str:
    today = date.today()
    year, week, _ = today.isocalendar()
    return f"{year}-W{week:02d}"


def size_distribution(cost_rows: list[str]) -> dict[str, int]:
    dist: dict[str, int] = {"S": 0, "M": 0, "L": 0, "XL": 0}
    for row in cost_rows:
        cols = [c.strip() for c in row.split("|") if c.strip()]
        if len(cols) >= 5:
            size = cols[4].strip()
            if size in dist:
                dist[size] += 1
    return dist


def generate_report(tasks: list[dict], cost_rows: list[str]) -> str:
    today = date.today().isoformat()
    label = week_label()

    total = len(tasks)
    done = [t for t in tasks if t.get("status") == "done"]
    failed = [t for t in tasks if t.get("status") == "failed"]
    in_progress = [t for t in tasks if t.get("status") not in ("done", "failed", "pending")]

    test_not_gen = [
        t for t in done
        if t.get("test_generated") is False
    ]
    review_missing = [
        t for t in done
        if t.get("review_completed") is False
        and isinstance(t.get("impact"), dict)
        and t["impact"].get("risk", "").upper() == "HIGH"
    ]
    impact_missing = []
    for t in tasks:
        impact = t.get("impact")
        status = t.get("status", "")
        if impact is None and status not in ("pending", "analyzing"):
            impact_missing.append(t)

    dist = size_distribution(cost_rows)

    lines = [
        f"# AI_SDLC 주간 리포트 — {label}",
        f"",
        f"**생성일**: {today}  ",
        f"**기간**: {label}",
        f"",
        f"---",
        f"",
        f"## 작업 현황",
        f"",
        f"| 상태 | 건수 |",
        f"|------|------|",
        f"| 전체 | {total} |",
        f"| 완료 (done) | {len(done)} |",
        f"| 실패 (failed) | {len(failed)} |",
        f"| 진행 중 | {len(in_progress)} |",
        f"",
        f"---",
        f"",
        f"## 작업 규모 분포 (cost-log 기준)",
        f"",
        f"| 규모 | 건수 |",
        f"|------|------|",
        f"| S (1-2 파일) | {dist['S']} |",
        f"| M (3-10 파일) | {dist['M']} |",
        f"| L (11-30 파일) | {dist['L']} |",
        f"| XL (31+ 파일) | {dist['XL']} |",
        f"",
        f"---",
        f"",
        f"## 공정 누락 현황",
        f"",
        f"### test_generated == false인 완료 task",
        f"",
    ]

    if test_not_gen:
        lines += [f"| task | sprint | branch |", f"|------|--------|--------|"]
        for t in test_not_gen:
            lines.append(f"| {t.get('task', 'N/A')} | #{t.get('sprint', '?')} | {t.get('branch', '')} |")
    else:
        lines.append("없음")

    lines += [
        f"",
        f"### review_completed == false인 HIGH risk 완료 task",
        f"",
    ]

    if review_missing:
        lines += [f"| task | sprint | impact risk |", f"|------|--------|-------------|"]
        for t in review_missing:
            risk = t.get("impact", {}).get("risk", "?") if isinstance(t.get("impact"), dict) else "?"
            lines.append(f"| {t.get('task', 'N/A')} | #{t.get('sprint', '?')} | {risk} |")
    else:
        lines.append("없음")

    lines += [
        f"",
        f"### impact == null인 코드 변경 task",
        f"",
    ]

    if impact_missing:
        lines += [f"| task | sprint | status |", f"|------|--------|--------|"]
        for t in impact_missing:
            lines.append(f"| {t.get('task', 'N/A')} | #{t.get('sprint', '?')} | {t.get('status', '?')} |")
    else:
        lines.append("없음")

    lines += [
        f"",
        f"---",
        f"",
        f"## 완료 task 목록",
        f"",
        f"| sprint | task | pr |",
        f"|--------|------|----|",
    ]
    for t in done:
        pr = t.get("pr_url") or "N/A"
        lines.append(f"| #{t.get('sprint', '?')} | {t.get('task', 'N/A')} | {pr} |")

    lines += ["", "---", "", f"*이 리포트는 `generate_sdlc_report.py`에 의해 자동 생성됩니다.*"]

    return "\n".join(lines) + "\n"


def main() -> None:
    print("AI_SDLC 주간 리포트 생성 중...")

    tasks = load_tasks()
    cost_rows = load_cost_log()

    if not tasks:
        print("[warn] AI/tasks/ 디렉토리가 없거나 task JSON이 없습니다.")

    report_content = generate_report(tasks, cost_rows)

    reports_dir = Path("AI/reports")
    reports_dir.mkdir(parents=True, exist_ok=True)

    label = week_label()
    output_path = reports_dir / f"weekly_{label}.md"
    output_path.write_text(report_content, encoding="utf-8")

    print(f"[ok] 리포트 생성 완료: {output_path}")
    print(f"     task 수: {len(tasks)}, cost-log 항목 수: {len(cost_rows)}")


if __name__ == "__main__":
    main()
