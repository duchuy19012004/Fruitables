namespace Fruitables.Services.Chat;

// Lỗi khi mã cuộc chat không còn tồn tại (cookie cũ / session bị xóa).
// API có thể tạo session mới và thử gửi lại 1 lần.
public class ChatSessionNotFoundException : Exception
{
    public ChatSessionNotFoundException(string message) : base(message)
    {
    }
}
