using Microsoft.AspNetCore.SignalR;
using PlatformA.Library.Common;

namespace PlatformA.Ticketing.API.Hubs
{
    /// <summary>
    /// 대기열 SignalR 허브.
    /// 클라이언트가 대기열 진입 후 연결하면, QueueWorkerService가 Active 전환 시
    /// "QueueActivated" 이벤트를 push하여 폴링 없이 즉시 알림.
    /// </summary>
    public class QueueHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            string? jwtToken = null;

            if (httpContext != null)
            {
                string authHeader = httpContext.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                    jwtToken = authHeader.Substring(7);

                if (string.IsNullOrEmpty(jwtToken))
                    jwtToken = httpContext.Request.Query["access_token"].ToString();
            }

            if (!string.IsNullOrEmpty(jwtToken))
            {
                int userId = TokenManager.ValidateTokenAndGetUserId(jwtToken);
                if (userId > 0)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
                    Context.Items["UserId"] = userId;
                }
                else
                {
                    Context.Abort();
                }
            }
            else
            {
                Context.Abort();
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
