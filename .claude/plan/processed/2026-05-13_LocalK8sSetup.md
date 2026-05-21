# Plan: Local K8s Deployment Setup (kind 기반)

## Context

AWS EKS 배포 전 로컬에서 Kubernetes 오케스트레이션을 검증한다.  
현재 모든 서비스 Dockerfile은 완비되어 있고 `docker-compose.full.yml`로 통합 스택 실행이 가능하나, K8s 매니페스트가 전혀 없다.  
kind(Kubernetes in Docker)를 사용해 로컬 클러스터를 구성하고, Ingress TLS 종단 처리 + 내부 HTTP 통신 방식으로 배포한다.  
이 매니페스트가 EKS 배포의 기반이 된다.

---

## 사전 확인 사항 (구현 전 체크)

| 항목 | 상태 | 비고 |
|------|------|------|
| Dockerfiles 완비 | ✅ | 6개 서비스 모두 멀티스테이지 빌드 |
| Health checks `/healthz`, `/readyz` | ⚠️ | Auth/Matching/Ticketing은 있음. **Utils.API, Game.Server는 미구현** |
| 환경변수 중앙화 (Consts.cs) | ✅ | 모든 설정 환경변수로 오버라이드 가능 |
| K8s 매니페스트 | ✅ | 신규 생성 완료 (2026-05-13) |
| Matching.API .NET 버전 | ⚠️ | .NET 9.0 (나머지는 8.0) |

### HTTPS → HTTP 전환 방법

모든 API Dockerfile은 `ASPNETCORE_HTTP_PORTS=""` / `ASPNETCORE_HTTPS_PORTS="{port}"` 로 HTTPS 전용 설정.  
K8s Deployment에서 환경변수 오버라이드로 코드 변경 없이 해결:
```yaml
- name: ASPNETCORE_HTTP_PORTS
  value: "8080"
- name: ASPNETCORE_HTTPS_PORTS
  value: ""
```
`UseHttpsRedirection()`은 HTTPS 포트가 설정되지 않으면 자동으로 no-op 처리됨.

---

## 생성된 디렉토리 구조

```
k8s/                                        ← 프로젝트 루트 아래 신규 생성
├── kind-config.yaml                        # kind 클러스터 설정 (포트 매핑)
├── .gitignore                              # secret.yaml 제외
├── namespace.yaml
├── configmap.yaml                          # 비민감 환경변수
├── secret.yaml.example                     # Secret 예시 (실제 secret.yaml은 .gitignore)
├── infra/
│   ├── redis-cluster.yaml                  # StatefulSet + Headless Service + Init Job
│   └── mariadb.yaml                        # StatefulSet + PVC + Service + Init ConfigMap
├── apps/
│   ├── auth-api.yaml                       # Deployment + ClusterIP Service
│   ├── matching-api.yaml                   # Deployment + ClusterIP Service
│   ├── ticketing-api.yaml                  # Deployment + ClusterIP Service
│   ├── utils-api.yaml                      # Deployment + ClusterIP Service
│   └── game-server.yaml                    # Deployment + NodePort Service (TCP 7777)
├── ingress.yaml                            # NGINX Ingress + TLS
└── scripts/
    └── build-and-load.ps1                  # 이미지 빌드 → kind load 자동화
```

---

## 파일별 구현 명세

### kind-config.yaml

```yaml
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes:
- role: control-plane
  kubeadmConfigPatches:
  - |
    kind: InitConfiguration
    nodeRegistration:
      kubeletExtraArgs:
        node-labels: "ingress-ready=true"
  extraPortMappings:
  - containerPort: 80
    hostPort: 80
    protocol: TCP
  - containerPort: 443
    hostPort: 443
    protocol: TCP
  - containerPort: 30777     # game-server NodePort
    hostPort: 7777
    protocol: TCP
```

### configmap.yaml (platform-a 네임스페이스)

| Key | Value |
|-----|-------|
| REDIS_CONNECTION_STRING | `redis-0.redis-headless.platform-a.svc.cluster.local:6379,...` (3 master 노드) |
| GAME_SERVER_IP | `game-server.platform-a.svc.cluster.local` |
| GAME_SERVER_PORT | `7777` |
| AUTH_API_URL | `http://auth-api.platform-a.svc.cluster.local:8080/api/Auth/login` |
| AUTH_API_REFRESH_URL | `http://auth-api.platform-a.svc.cluster.local:8080/api/Auth/refresh` |
| TICKET_API_URL | `http://ticketing-api.platform-a.svc.cluster.local:8080` |
| MATCH_API_URL | `http://matching-api.platform-a.svc.cluster.local:8080/api/GameMatch/RequestMatch` |
| MATCH_HUB_URL | `http://matching-api.platform-a.svc.cluster.local:8080/hubs/matching` |
| QUEUE_BASE_RATE | `50` |
| QUEUE_MAX_RATE | `500` |
| SNOWFLAKE_WORKER_ID | `1` |
| SNOWFLAKE_DATACENTER_ID | `1` |

### secret.yaml.example (실제 값은 .gitignore)

```yaml
stringData:
  JWT_SECRET: "change-me-in-production-must-be-32-chars-or-more"
  MYSQL_WEBAPP_CONNECTION_STRING: "Server=mariadb.platform-a.svc.cluster.local;Port=3306;Database=db_WebApp;User=root;Password=pass1234"
  MYSQL_LOGAPP_CONNECTION_STRING: "Server=mariadb.platform-a.svc.cluster.local;Port=3306;Database=db_LogApp;User=root;Password=pass1234"
```

---

### infra/redis-cluster.yaml

**구성 요소:**
1. **ConfigMap** `redis-config` — `redis.conf` (cluster-enabled yes, appendonly yes)
2. **StatefulSet** `redis` — 6 replicas, headless service 연동, PVC 256Mi per pod
3. **Headless Service** `redis-headless` — DNS: `redis-{0..5}.redis-headless.platform-a.svc.cluster.local:6379`
4. **Job** `redis-cluster-init` — 6개 노드 ready 대기 후 `redis-cli --cluster create ... --cluster-replicas 1 --cluster-yes`

**REDIS_CONNECTION_STRING** (ConfigMap에 설정):
```
redis-0.redis-headless.platform-a.svc.cluster.local:6379,redis-1.redis-headless.platform-a.svc.cluster.local:6379,redis-2.redis-headless.platform-a.svc.cluster.local:6379
```

---

### infra/mariadb.yaml

**구성 요소:**
1. **ConfigMap** `mariadb-init` — 초기화 SQL (db_WebApp, db_LogApp 데이터베이스 생성)
2. **StatefulSet** `mariadb` — 1 replica, volumeClaimTemplates(1Gi), init ConfigMap 마운트
3. **Service** `mariadb` — ClusterIP, port 3306

---

### apps/\*.yaml 공통 패턴

```yaml
# Deployment
spec:
  template:
    spec:
      containers:
      - image: platforma-{service}:local
        imagePullPolicy: Never          # kind에서 로컬 이미지 사용
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_HTTP_PORTS
          value: "8080"
        - name: ASPNETCORE_HTTPS_PORTS
          value: ""
        envFrom:
        - configMapRef: { name: platforma-config }
        - secretRef:    { name: platforma-secret }
        livenessProbe:
          httpGet: { path: /healthz, port: 8080 }   # Utils/Game은 tcpSocket
        readinessProbe:
          httpGet: { path: /readyz,  port: 8080 }
        resources:
          requests: { memory: 128Mi, cpu: 100m }
          limits:   { memory: 512Mi, cpu: 500m }
```

**서비스별 이미지 태그:**
| 서비스 | 이미지 태그 | probe 방식 |
|-------|-----------|-----------|
| auth-api | `platforma-auth:local` | HTTP /healthz /readyz |
| matching-api | `platforma-matching:local` | HTTP /healthz /readyz |
| ticketing-api | `platforma-ticketing:local` | HTTP /healthz /readyz |
| utils-api | `platforma-utils:local` | TCP (미구현) |
| game-server | `platforma-game:local` | TCP 7777 |

**game-server Service:**
```yaml
type: NodePort
ports:
- port: 7777 / nodePort: 30777  # kind-config.yaml extraPortMappings와 일치
```

---

### ingress.yaml

- `ingressClassName: nginx`
- TLS: `platforma-tls` Secret (build-and-load.ps1이 생성)
- host: `platforma.local`
- WebSocket 어노테이션: proxy-read-timeout/send-timeout 3600s, Upgrade/Connection 헤더 전달
- 경로: `/api/Auth`, `/api/GameMatch`, `/hubs/matching`, `/api/Queue`, `/hubs/queue`, `/api/Util`

---

### scripts/build-and-load.ps1

자동화 스크립트 (Windows PowerShell 7+):
1. `cd PlatformA && docker build -f {Dockerfile} -t {tag} .` — 5개 서비스 이미지 빌드
2. `kind load docker-image {tag} --name platforma` — 각 이미지를 kind 클러스터에 로드
3. `openssl req -x509 ...` — 자체서명 인증서 생성 (`k8s/scripts/certs/`)
4. `kubectl create secret tls platforma-tls --dry-run=client -o yaml | kubectl apply` — TLS Secret 적용

---

## 배포 순서 (Verification)

```powershell
# 사전 조건: kind, kubectl, docker, openssl 설치
# winget install Kubernetes.kind

# 1. kind 클러스터 생성
kind create cluster --name platforma --config k8s/kind-config.yaml

# 2. NGINX Ingress Controller 설치 (kind 전용)
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/kind/deploy.yaml
kubectl wait --namespace ingress-nginx --for=condition=ready pod --selector=app.kubernetes.io/component=controller --timeout=90s

# 3. 이미지 빌드 + kind 로드 + TLS Secret 생성
cd PlatformA && pwsh ../k8s/scripts/build-and-load.ps1

# 4. 매니페스트 적용 (순서 중요)
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
cp k8s/secret.yaml.example k8s/secret.yaml  # 값 수정 후
kubectl apply -f k8s/secret.yaml
kubectl apply -f k8s/infra/
kubectl wait -n platform-a --for=condition=complete job/redis-cluster-init --timeout=120s
kubectl apply -f k8s/apps/
kubectl apply -f k8s/ingress.yaml

# 5. 상태 확인
kubectl get pods -n platform-a
kubectl logs -n platform-a job/redis-cluster-init

# 6. EF Core Migration 실행 (최초 1회)
kubectl run -it --rm --image=platforma-auth:local --restart=Never -n platform-a migrate \
  -- dotnet ef database update --context DbWebAppContext

# 7. hosts 파일 추가 (C:\Windows\System32\drivers\etc\hosts)
# 127.0.0.1  platforma.local

# 8. 접속 테스트
curl https://platforma.local/api/Auth/health -k

# 9. 클러스터 삭제
kind delete cluster --name platforma
```

---

## 주요 설계 결정

- HTTPS 비활성화: `ASPNETCORE_HTTP_PORTS=8080` + `ASPNETCORE_HTTPS_PORTS=` 오버라이드 (코드 변경 불필요)
- Redis 클러스터 init: docker-compose 방식과 동일 로직을 K8s Job으로 이식
- 인증서: K8s TLS Secret으로 devcert.pfx 대체 (Ingress만 사용)
- `imagePullPolicy: Never`: kind는 로컬 이미지를 `kind load`로 주입하므로 레지스트리 불필요

## 미해결 항목 (backlog)

- Utils.API, Game.Server Health check 미구현 → TCP probe 임시 사용
- Migration 자동화: 현재 수동 kubectl run 방식
- EKS 이전 시: NodePort → LoadBalancer, ACM 인증서 교체 필요
