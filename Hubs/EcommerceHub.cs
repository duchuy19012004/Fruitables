using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Fruitables.Services.Interfaces;

namespace Fruitables.Hubs
{
    public class EcommerceHub : Hub
    {
        private readonly IChatService _chatService;

        public EcommerceHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        public override async Task OnConnectedAsync()
        {
            var user = Context.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User:{userId}");
                }

                if (user.IsInRole("Admin") || user.IsInRole("SuperAdmin"))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
                }
            }

            await base.OnConnectedAsync();
        }

        public async Task JoinOrderGroup(int orderId, [Microsoft.AspNetCore.Mvc.FromServices] Fruitables.Data.ApplicationDbContext dbContext)
        {
            if (orderId <= 0) throw new HubException("Invalid orderId.");

            if (Context.User != null && (Context.User.IsInRole("Admin") || Context.User.IsInRole("SuperAdmin")))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Order:{orderId}");
                return;
            }

            var userIdStr = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                var orderExists = await dbContext.Orders.AnyAsync(o => o.Id == orderId && o.UserId == userId);
                if (orderExists)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Order:{orderId}");
                    return;
                }
            }

            throw new HubException("Unauthorized to join this order group.");
        }

        public async Task LeaveOrderGroup(int orderId)
        {
            if (orderId <= 0) return;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Order:{orderId}");
        }

        public async Task JoinProductGroup(int productId)
        {
            if (productId <= 0) throw new HubException("Invalid productId.");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Product:{productId}");
        }

        public async Task LeaveProductGroup(int productId)
        {
            if (productId <= 0) return;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Product:{productId}");
        }

        // Chat: dùng ChatService (đã có intent routing + sensitive guard)
        public async Task SendChat(string message)
        {
            var trimmed = (message ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                await Clients.Caller.SendAsync("ChatError", "Tin nhắn trống.");
                return;
            }

            int? userId = null;
            var userIdStr = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out var parsedUserId))
                userId = parsedUserId;

            try
            {
                // Tạo session nếu chưa có (dùng ChatState để track)
                var state = Services.Chat.ChatState.GetOrAdd(Context);
                if (!state.SessionId.HasValue)
                {
                    state.SessionId = await _chatService.CreateSessionAsync(userId, "signalr");
                }

                // ChatService xử lý: intent routing → handler phù hợp → lưu DB
                var response = await _chatService.SendAsync(
                    state.SessionId.Value, trimmed, userId, null);

                await Clients.Caller.SendAsync("ChatResponse", response.AssistantMessage.Content);
            }
            catch (Exception)
            {
                await Clients.Caller.SendAsync("ChatError", "Đã xảy ra lỗi. Vui lòng thử lại.");
            }
        }
    }
}
