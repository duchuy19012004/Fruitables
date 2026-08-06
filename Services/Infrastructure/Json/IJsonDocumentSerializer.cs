namespace Fruitables.Services.Infrastructure.Json;

public interface IJsonDocumentSerializer
{
    string Serialize<T>(T document);
    T Deserialize<T>(string json);
    bool TryDeserialize<T>(string json, out T? document, out string? error);
}
