using System.Diagnostics;

Console.WriteLine("=== 티켓팅 전쟁 시뮬레이터 (공격 개시) ===");

using var client = new HttpClient();
string baseUrl = "http://localhost:5282"; // 포트번호 확인 필수! (Ticketing 서버)

// 1. 티켓 100장 발행 (초기화)
await client.PostAsync($"{baseUrl}/api/tickets/reset?count=100", null);
Console.WriteLine("🎫 티켓 100장 세팅 완료.");

// 2. 300명이 동시에 '구매' 버튼 클릭!
int attackerCount = 300;
var tasks = new List<Task<HttpResponseMessage>>();

Console.WriteLine($"🔥 {attackerCount}명의 사용자가 동시에 접속합니다...");
var sw = Stopwatch.StartNew();

for (int i = 0; i < attackerCount; i++)
{
    // 비동기로 요청을 동시에 쏩니다 (await 안 함)
    //tasks.Add(client.PostAsync($"{baseUrl}/api/tickets/buy-bad", null)); // 취약한 버전
    tasks.Add(client.PostAsync($"{baseUrl}/api/tickets/buy-good", null)); // 분산락 처리 적용 수정
}

// 모든 요청이 끝날 때까지 대기
var results = await Task.WhenAll(tasks);
sw.Stop();

// 3. 결과 분석
int successCount = results.Count(r => r.IsSuccessStatusCode);
int failCount = results.Count(r => !r.IsSuccessStatusCode);

Console.WriteLine($"\n--- 결과 리포트 ({sw.ElapsedMilliseconds}ms) ---");
Console.WriteLine($"✅ 예매 성공 응답: {successCount}명");
Console.WriteLine($"❌ 예매 실패 응답: {failCount}명");

// 실제 Redis에 남은 수량 확인 (서버 로그나 Redis-cli로 확인해야 정확함)
Console.WriteLine("⚠️ 주의: 성공 응답이 100개를 넘었다면 '중복 예매' 사고 발생!");