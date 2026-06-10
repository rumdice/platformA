# AI_SDLC Cost Log (DB 기반)

> 생성 시각: 2026-06-10 00:46 UTC
> 소스: PostgreSQL `sdlc.ai_model_runs` JOIN `sdlc.ai_jobs`
> 이 파일은 스크립트로 생성됩니다. 직접 편집하지 마세요.

## Summary

| Total Runs | Total Duration (sec) | Total Consume Tokens | Total Cache Tokens |
|---:|---:|---:|---:|
| 15 | 21001 | 284,964,596 | 4,306,294 |

## By Sprint

| Sprint | Runs | Duration (sec) | Consume Tokens | Cache Tokens |
|---:|---:|---:|---:|---:|
| #28 | 1 | 2208 | 16,096,374 | 654,245 |
| #30 | 1 | 3192 | 98,248,083 | 1,470,418 |
| #34 | 1 | 1556 | 7,027,484 | 83,800 |
| #35 | 1 | 475 | 11,804,327 | 65,899 |
| #36 | 1 | 830 | 10,483,470 | 32,436 |
| #37 | 1 | 1586 | 27,779,817 | 173,949 |
| #38 | 1 | 178 | 5,558,096 | 9,268 |
| #45 | 1 | 1166 | 13,077,872 | 259,111 |
| #46 | 1 | 1168 | 15,469,790 | 272,980 |
| #47 | 1 | 1859 | 23,429,700 | 347,374 |
| #48 | 1 | 3027 | 33,948,347 | 490,813 |
| #49 | 1 | 0 | 19,208,710 | 313,568 |
| #52 | 1 | 3600 | 45,000 | 120,000 |
| #53 | 1 | 89 | 1,612,519 | 8,850 |
| #54 | 1 | 67 | 1,175,007 | 3,583 |

## Details

| Sprint | Task | Model | Duration (sec) | Consume Tokens | Cache Tokens | Date |
|---:|---|---|---:|---:|---:|---|
| #28 | AddCostLogMetrics | claude-sonnet-4-6 | 2208 | 16,096,374 | 654,245 | 2026-05-26 |
| #30 | ModernizeNet10Stack | claude-sonnet-4-6 | 3192 | 98,248,083 | 1,470,418 | 2026-05-29 |
| #34 | FixCostLogCalcWindows | claude-sonnet-4-6 | 1556 | 7,027,484 | 83,800 | 2026-06-02 |
| #35 | SetupDockerOneClick | claude-sonnet-4-6 | 475 | 11,804,327 | 65,899 | 2026-06-03 |
| #36 | AddSdlcDockerInfra | claude-sonnet-4-6 | 830 | 10,483,470 | 32,436 | 2026-06-03 |
| #37 | FixRequirementEnforcement | claude-sonnet-4-6 | 1586 | 27,779,817 | 173,949 | 2026-06-03 |
| #38 | FixPrArchiveDateDep | claude-sonnet-4-6 | 178 | 5,558,096 | 9,268 | 2026-06-03 |
| #45 | CompletePhase3Automation | claude-sonnet-4-6 | 1166 | 13,077,872 | 259,111 | 2026-06-05 |
| #46 | AddModelRunsIntegration | claude-sonnet-4-6 | 1168 | 15,469,790 | 272,980 | 2026-06-05 |
| #47 | AddLlmRouter | claude-sonnet-4-6 | 1859 | 23,429,700 | 347,374 | 2026-06-05 |
| #48 | PostgresPrimaryMigration | claude-sonnet-4-6 | 3027 | 33,948,347 | 490,813 | 2026-06-05 |
| #49 | StabilizeSdlcPhase3Ops | claude-sonnet-4-6 | null | 19,208,710 | 313,568 | 2026-06-07 |
| #52 | AdoptPhaseCDbOnly | claude-sonnet-4-6 | 3600 | 45,000 | 120,000 | 2026-06-09 |
| #53 | FixDoneSkillPhaseC | claude-sonnet-4-6 | 89 | 1,612,519 | 8,850 | 2026-06-10 |
| #54 | FixGatesStepBased | claude-sonnet-4-6 | 67 | 1,175,007 | 3,583 | 2026-06-10 |
