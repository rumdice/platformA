#!/usr/bin/env python3
"""
generate_api_docs.py
컨트롤러 C# 소스 + DTO 클래스를 파싱하여 Docs/api-guide/*.md 를 자동 생성합니다.
docs.yml 의 docfx 빌드 직전 스텝으로 실행됩니다.
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent

# C# 기본 타입 → 마크다운 표기 매핑
CSHARP_TYPES = {
    "string": "string",
    "int": "int",
    "long": "long",
    "bool": "bool",
    "decimal": "decimal",
    "float": "float",
    "double": "double",
    "DateTime": "datetime",
    "DateTimeOffset": "datetime",
    "Guid": "string (UUID)",
    "byte": "byte",
    "short": "short",
    "OrderType": "int (0=Buy, 1=Sell)",
}

# ─────────────────────────────────────────────
# 서비스 정의
# ─────────────────────────────────────────────
SERVICES = [
    {
        "name": "Auth API",
        "output": "Docs/api-guide/auth.md",
        "port": 7001,
        "runtime": ".NET 8.0",
        "auth": "Bearer Token (JWT) — /api/Auth/login·logout·refresh 는 토큰 불필요",
        "description": "인증 및 JWT 토큰 생명주기를 담당하는 서비스입니다.",
        "extra": (
            "\n## 인증 흐름 요약\n\n"
            "```\n"
            "클라이언트                         Auth API                        Redis\n"
            "    │                                 │                              │\n"
            "    │── POST /login ──────────────────►│                              │\n"
            "    │                                 │── SAVE refresh_token ───────►│\n"
            "    │◄─ { token, refreshToken } ──────│                              │\n"
            "    │                                 │                              │\n"
            "    │── POST /refresh ────────────────►│                              │\n"
            "    │                                 │── GET+DEL refresh_token ────►│\n"
            "    │◄─ { token, refreshToken } ──────│── SAVE new_refresh_token ───►│\n"
            "    │                                 │                              │\n"
            "    │── POST /logout ─────────────────►│                              │\n"
            "    │                                 │── GET+DEL refresh_token ────►│\n"
            "    │◄─ { message: \"로그아웃 완료.\" } ─│                              │\n"
            "```\n\n"
            "> Access Token 검증은 각 API 서비스가 JWT 서명을 직접 확인합니다."
            " Auth API에 재요청하지 않습니다.\n"
        ),
        "controllers": [
            "PlatformA/PlatformA.Auth.API/Controllers/AuthController.cs"
        ],
        "dto_dirs": ["PlatformA/PlatformA.Auth.API/"],
    },
    {
        "name": "Ticketing (Queue) API",
        "output": "Docs/api-guide/ticketing.md",
        "port": 7003,
        "runtime": ".NET 8.0",
        "auth": "Bearer Token (JWT) — Authorization 헤더 필요",
        "description": "게임 서버 입장 대기열 진입·상태·이탈을 담당하는 서비스입니다.",
        "extra": "",
        "controllers": [
            "PlatformA/PlatformA.Ticketing.API/Controllers/QueueController.cs"
        ],
        "dto_dirs": ["PlatformA/PlatformA.Ticketing.API/"],
    },
    {
        "name": "Matching API",
        "output": "Docs/api-guide/matching.md",
        "port": 7002,
        "runtime": ".NET 9.0",
        "auth": "Bearer Token (JWT) — Authorization 헤더 필요",
        "description": (
            "1v1 게임 매칭 대기열 및 주식 주문 처리를 담당하는 서비스입니다.\n"
            "SignalR Hub(`/hubs/matching`)를 통해 매칭 결과(MatchFound / MatchTimeout)를 Push합니다."
        ),
        "extra": (
            "\n## SignalR 이벤트\n\n"
            "| 이벤트 | 발생 조건 | 페이로드 |\n"
            "|--------|----------|----------|\n"
            "| `MatchFound` | 매칭 성사 | `{ gameServerIp, gameServerPort, roomId }` |\n"
            "| `MatchTimeout` | 대기 120초 초과 | _(없음)_ |\n"
        ),
        "controllers": [
            "PlatformA/PlatformA.Matching.API/Controllers/GameMatchController.cs",
            "PlatformA/PlatformA.Matching.API/Controllers/OrderController.cs",
        ],
        "dto_dirs": ["PlatformA/PlatformA.Matching.API/"],
    },
    {
        "name": "Utils API",
        "output": "Docs/api-guide/utils.md",
        "port": 7004,
        "runtime": ".NET 8.0",
        "auth": "없음 (공개 API)",
        "description": "IP 조회, URL 단축, 클릭 통계 등 유틸리티 기능을 제공합니다.",
        "extra": "",
        "controllers": [
            "PlatformA/PlatformA.Utils.API/Controllers/UtilController.cs"
        ],
        "dto_dirs": ["PlatformA/PlatformA.Utils.API/"],
    },
]


# ─────────────────────────────────────────────
# 파싱 유틸리티
# ─────────────────────────────────────────────

def extract_xml_summary(text: str) -> str:
    """/// <summary>...</summary> 블록에서 텍스트 추출. 경로 접두사 줄은 제거합니다."""
    lines = []
    in_summary = False
    for line in text.splitlines():
        s = line.strip()
        if re.match(r'///\s*<summary>', s):
            in_summary = True
            inline = re.match(r'///\s*<summary>(.*)</summary>', s)
            if inline:
                return inline.group(1).strip()
            continue
        if re.match(r'///\s*</summary>', s):
            break
        if in_summary and s.startswith("///"):
            content = re.sub(r'^///\s?', '', s)
            content = re.sub(r'<[^>]+>', '', content).strip()
            # HTTP 메서드+경로 줄 제거 (예: "POST /api/auth/login")
            if re.match(r'^(GET|POST|PUT|DELETE|PATCH)\s+/', content, re.IGNORECASE):
                continue
            if content:
                lines.append(content)
    return " ".join(lines).strip()


def find_method_body(text: str, start: int) -> str:
    """start 위치 이후 첫 번째 { } 블록을 반환한다."""
    brace_pos = text.find('{', start)
    if brace_pos == -1:
        return ""
    depth = 0
    for i in range(brace_pos, len(text)):
        if text[i] == '{':
            depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0:
                return text[brace_pos : i + 1]
    return ""


def get_base_route(text: str) -> str:
    """클래스 레벨 [Route(...)] 추출. [controller] 치환 포함."""
    m = re.search(r'\[Route\("([^"]+)"\)\]', text)
    if not m:
        return ""
    route = m.group(1)
    if "[controller]" in route:
        cls = re.search(r'public class (\w+)Controller', text)
        if cls:
            route = route.replace("[controller]", cls.group(1))
    return "/" + route.lower().lstrip("/")


def parse_dto_fields(dto_name: str, dto_dirs: list[str]) -> list[dict]:
    """dto_name 클래스의 필드 목록을 파싱한다."""
    # 모든 dto_dirs 하위 .cs 파일 탐색
    cs_files = []
    for d in dto_dirs:
        cs_files.extend((ROOT / d).rglob("*.cs"))

    class_src = None
    for f in cs_files:
        src = f.read_text(encoding="utf-8", errors="ignore")
        if re.search(rf'\bpublic class {re.escape(dto_name)}\b', src):
            class_src = src
            break
    if class_src is None:
        return []

    # 클래스 블록 추출
    m = re.search(rf'public class {re.escape(dto_name)}\b[^\{{]*\{{', class_src)
    if not m:
        return []
    body = find_method_body(class_src, m.start())

    fields = []
    # 속성별로 어트리뷰트 + 선언을 묶어서 파싱
    prop_pattern = re.compile(
        r'((?:\[.*?\]\s*)*)'                        # 어트리뷰트들
        r'public\s+([\w<>\[\]?]+)\s+(\w+)\s*\{[^}]*get[^}]*\}',
        re.DOTALL
    )
    for pm in prop_pattern.finditer(body):
        attrs = pm.group(1)
        ctype = pm.group(2).rstrip("?")
        pname = pm.group(3)

        # 기본값: 표시 타입
        display_type = CSHARP_TYPES.get(ctype, ctype)

        # 필수 여부
        required = "[Required" in attrs

        # 제약 조건 수집
        constraints = []
        min_len = re.search(r'\[MinLength\((\d+)', attrs)
        max_len = re.search(r'\[MaxLength\((\d+)', attrs)
        rng = re.search(r'\[Range\(([^)]+)\)', attrs)
        regex = re.search(r'\[RegularExpression\(@?"([^"]+)"', attrs)

        if min_len and max_len:
            constraints.append(f"{min_len.group(1)}~{max_len.group(1)}자")
        elif max_len:
            constraints.append(f"최대 {max_len.group(1)}자")
        elif min_len:
            constraints.append(f"최소 {min_len.group(1)}자")
        if rng:
            constraints.append(f"범위 {rng.group(1)}")
        if regex:
            constraints.append(f"정규식: `{regex.group(1)}`")

        fields.append({
            "name": pname[0].lower() + pname[1:],  # camelCase
            "type": display_type,
            "required": required,
            "constraint": ", ".join(constraints) if constraints else "—",
        })
    return fields


def infer_error_codes(body: str, has_rate_limit: bool) -> list[tuple[int, str]]:
    """메서드 바디에서 반환되는 HTTP 오류 코드와 설명을 추출한다."""
    codes = {}
    if "return Unauthorized" in body:
        codes[401] = "유효하지 않거나 만료된 토큰"
    if "return BadRequest" in body:
        codes[400] = "입력 검증 실패 또는 잘못된 요청"
    if "return NotFound" in body:
        codes[404] = "리소스를 찾을 수 없음"
    if "return Conflict" in body:
        codes[409] = "리소스 충돌"
    if has_rate_limit:
        codes[429] = "Rate Limit 초과"
    return sorted(codes.items())


def infer_success_code(body: str) -> int:
    if "return Accepted" in body:
        return 202
    if "return Created" in body:
        return 201
    return 200


def make_json_example(fields: list[dict]) -> str:
    """DTO 필드 목록에서 JSON 예시 생성."""
    if not fields:
        return ""
    lines = ["{"]
    for i, f in enumerate(fields):
        comma = "," if i < len(fields) - 1 else ""
        t = f["type"]
        if "string" in t:
            val = f'"<{f["name"]}>"'
        elif t in ("int", "long", "short", "byte"):
            val = "0"
        elif t == "bool":
            val = "false"
        elif t == "decimal" or t == "float" or t == "double":
            val = "0.0"
        elif "datetime" in t:
            val = '"2026-01-01T00:00:00Z"'
        elif "0=Buy" in t:
            val = "0"
        else:
            val = f'"<{f["name"]}>"'
        lines.append(f'  "{f["name"]}": {val}{comma}')
    lines.append("}")
    return "\n".join(lines)


# ─────────────────────────────────────────────
# 마크다운 생성
# ─────────────────────────────────────────────

def render_endpoint(ep: dict, dto_dirs: list[str]) -> str:
    lines = []
    method = ep["method"]
    route = ep["route"]
    summary = ep.get("summary", "")
    has_rl = ep.get("rate_limit") is not None

    lines.append(f"### {method} {route}")
    lines.append("")
    if summary:
        lines.append(summary)
        lines.append("")

    if has_rl:
        lines.append(f"**Rate Limit**: 적용 (`{ep['rate_limit']}` 정책)")
        lines.append("")

    # Request body
    dto_name = ep.get("dto_type")
    if dto_name:
        fields = parse_dto_fields(dto_name, dto_dirs)
        if fields:
            lines.append("**요청 Body**")
            lines.append("")
            lines.append("| 필드 | 타입 | 필수 | 제약 조건 |")
            lines.append("|------|------|------|-----------|")
            for f in fields:
                req = "✓" if f["required"] else ""
                lines.append(f'| {f["name"]} | {f["type"]} | {req} | {f["constraint"]} |')
            lines.append("")
            example = make_json_example(fields)
            if example:
                lines.append("```json")
                lines.append(example)
                lines.append("```")
                lines.append("")

    # Success response
    success = ep.get("success_code", 200)
    lines.append(f"**응답 {success}**")
    lines.append("")
    lines.append("> 응답 JSON 형식은 구현 코드 또는 Swagger(`/swagger`)를 참조하세요.")
    lines.append("")

    # Error codes
    body = ep.get("_body", "")
    errs = infer_error_codes(body, has_rl)
    if errs:
        lines.append("**오류 코드**")
        lines.append("")
        lines.append("| 코드 | 상황 |")
        lines.append("|------|------|")
        for code, desc in errs:
            lines.append(f"| {code} | {desc} |")
        lines.append("")

    lines.append("---")
    lines.append("")
    return "\n".join(lines)


def parse_controller(filepath: str) -> list[dict]:
    """단일 컨트롤러 파일을 파싱하여 엔드포인트 목록 반환."""
    path = ROOT / filepath
    if not path.exists():
        print(f"[WARN] 파일 없음: {filepath}")
        return []

    text = path.read_text(encoding="utf-8", errors="ignore")
    base_route = get_base_route(text)

    endpoints = []

    # 메서드 단위 파싱: XML 주석 + 어트리뷰트 + 메서드 선언 묶음
    method_re = re.compile(
        r'((?:\s*///[^\n]*\n)*)'                    # XML 주석 블록
        r'((?:\s*\[[^\]]+\]\s*\n)*)'               # 어트리뷰트 줄들
        r'\s*public\s+(?:async\s+)?Task<IActionResult>\s+(\w+)\s*\(([^)]*)\)',
        re.MULTILINE,
    )

    for m in method_re.finditer(text):
        xml_block = m.group(1)
        attr_block = m.group(2)
        method_name = m.group(3)
        params_str = m.group(4)

        # HTTP 어트리뷰트 탐색
        http_m = re.search(
            r'\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?\]',
            attr_block, re.IGNORECASE
        )
        if not http_m:
            continue

        http_method = http_m.group(1).upper()
        sub_route = http_m.group(2) or ""

        # 경로 조합
        if sub_route.startswith("/"):
            # 절대 경로 (예: /go/{code})
            full_route = sub_route
        else:
            full_route = (base_route.rstrip("/") + "/" + sub_route).rstrip("/")
        full_route = re.sub(r'/+', '/', full_route)
        if not full_route.startswith("/"):
            full_route = "/" + full_route

        # Rate Limit
        rl_m = re.search(r'\[RedisRateLimit\("([^"]+)"\)\]', attr_block)
        rate_limit = rl_m.group(1) if rl_m else None

        # 요약
        summary = extract_xml_summary(xml_block)
        if not summary:
            # 인라인 주석 시도
            inline_m = re.search(r'//\s+(\d+\.\s+)?([^\n]+)\n\s*\[(?:RedisRateLimit|Http)', attr_block)
            if inline_m:
                summary = inline_m.group(2).strip()

        # [FromBody] DTO
        fb_m = re.search(r'\[FromBody\]\s+(\w+)', params_str)
        dto_type = fb_m.group(1) if fb_m else None

        # 메서드 바디
        body = find_method_body(text, m.end())

        endpoints.append({
            "method": http_method,
            "route": full_route,
            "method_name": method_name,
            "summary": summary,
            "rate_limit": rate_limit,
            "dto_type": dto_type,
            "success_code": infer_success_code(body),
            "_body": body,
        })

    return endpoints


def generate_service_doc(svc: dict) -> str:
    """서비스 정의에서 마크다운 전체 문자열 생성."""
    lines = []

    # 헤더
    lines.append(f"# {svc['name']}")
    lines.append("")
    lines.append(svc["description"])
    lines.append("")

    # 서비스 정보 표
    lines.append("| 항목 | 값 |")
    lines.append("|------|-----|")
    lines.append(f"| 개발 환경 기본 URL | `https://localhost:{svc['port']}` |")
    lines.append(f"| 런타임 | {svc['runtime']} |")
    lines.append(f"| 인증 방식 | {svc['auth']} |")
    lines.append("")
    lines.append("---")
    lines.append("")

    # 공통 오류 응답
    lines.append("## 공통 오류 응답 형식")
    lines.append("")
    lines.append("```json")
    lines.append('{ "message": "설명 메시지" }')
    lines.append("```")
    lines.append("")
    lines.append("---")
    lines.append("")

    # 엔드포인트
    lines.append("## 엔드포인트")
    lines.append("")

    all_endpoints = []
    for ctrl_path in svc["controllers"]:
        all_endpoints.extend(parse_controller(ctrl_path))

    if not all_endpoints:
        lines.append("_엔드포인트가 없거나 파싱에 실패했습니다._")
    else:
        for ep in all_endpoints:
            lines.append(render_endpoint(ep, svc["dto_dirs"]))

    # 서비스별 추가 섹션
    if svc.get("extra"):
        lines.append(svc["extra"])

    # 자동 생성 고지
    lines.append("")
    lines.append(
        "> **이 파일은 `.github/scripts/generate_api_docs.py`로 자동 생성됩니다.**"
        " 수동 편집 내용은 다음 실행 시 덮어씌워집니다."
        " 내용 변경이 필요하면 컨트롤러 XML 주석 또는 스크립트를 수정하세요."
    )

    return "\n".join(lines) + "\n"


# ─────────────────────────────────────────────
# 메인
# ─────────────────────────────────────────────

def main():
    errors = 0
    for svc in SERVICES:
        out_path = ROOT / svc["output"]
        out_path.parent.mkdir(parents=True, exist_ok=True)
        try:
            content = generate_service_doc(svc)
            out_path.write_text(content, encoding="utf-8")
            print(f"[OK] {svc['output']}")
        except Exception as e:
            print(f"[ERROR] {svc['output']}: {e}", file=sys.stderr)
            errors += 1
    return errors


if __name__ == "__main__":
    sys.exit(main())
