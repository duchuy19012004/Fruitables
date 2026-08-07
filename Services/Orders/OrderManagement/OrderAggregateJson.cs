using Fruitables.Models;
using Fruitables.Models.Json;
using Fruitables.Services.Infrastructure.Json;

namespace Fruitables.Services.Orders.OrderManagement;

internal static class OrderAggregateJson
{
    public static OrderStatusHistoryDocument ReadHistory(string json, IJsonDocumentSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            return new OrderStatusHistoryDocument();
        return serializer.Deserialize<OrderStatusHistoryDocument>(json);
    }

    public static OrderNotesDocument ReadNotes(string json, IJsonDocumentSerializer serializer)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            return new OrderNotesDocument();
        return serializer.Deserialize<OrderNotesDocument>(json);
    }

    public static string SerializeHistory(OrderStatusHistoryDocument document, IJsonDocumentSerializer serializer) =>
        serializer.Serialize(document);

    public static string SerializeNotes(OrderNotesDocument document, IJsonDocumentSerializer serializer) =>
        serializer.Serialize(document);

    public static List<OrderStatusHistory> ToHistoryEntities(int orderId, OrderStatusHistoryDocument document, IReadOnlyDictionary<int, User>? admins = null)
    {
        var index = 1;
        return document.Entries.Select(entry => new OrderStatusHistory
        {
            Id = index++,
            OrderId = orderId,
            OldStatus = entry.OldStatus,
            NewStatus = entry.NewStatus,
            AdminId = entry.AdminId,
            Notes = entry.Notes,
            CreatedAt = entry.CreatedAt,
            Admin = admins != null && admins.TryGetValue(entry.AdminId, out var admin)
                ? admin
                : new User { Id = entry.AdminId, Name = "System" }
        }).ToList();
    }

    public static List<OrderNote> ToNoteEntities(int orderId, OrderNotesDocument document)
    {
        return document.Notes.Select((note, index) => new OrderNote
        {
            Id = note.Id > 0 ? note.Id : index + 1,
            OrderId = orderId,
            AdminId = note.AdminId,
            AdminName = note.AdminName,
            Content = note.Content,
            CreatedAt = note.CreatedAt
        }).ToList();
    }

    public static OrderStatusHistoryDocument AppendHistory(
        OrderStatusHistoryDocument document,
        OrderStatus oldStatus,
        OrderStatus newStatus,
        int adminId,
        string? notes,
        DateTime createdAt) =>
        new()
        {
            Entries =
            [
                ..document.Entries,
                new OrderStatusHistoryEntry
                {
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    AdminId = adminId,
                    Notes = notes,
                    CreatedAt = createdAt
                }
            ]
        };

    public static (OrderNotesDocument Document, OrderNoteDocument Added) AppendNote(
        OrderNotesDocument document,
        int adminId,
        string adminName,
        string content,
        DateTime createdAt)
    {
        var nextId = Math.Max(1, document.Notes.Select(note => note.Id).DefaultIfEmpty(0).Max() + 1);
        var added = new OrderNoteDocument
        {
            Id = nextId,
            AdminId = adminId,
            AdminName = adminName,
            Content = content,
            CreatedAt = createdAt
        };
        return (new OrderNotesDocument { Notes = [..document.Notes, added] }, added);
    }
}
