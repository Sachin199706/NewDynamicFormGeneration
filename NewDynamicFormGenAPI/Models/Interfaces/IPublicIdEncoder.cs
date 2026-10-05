namespace NewDynamicFormGenAPI.Models.Interfaces;

public interface IPublicIdEncoder
{
    string Encode(int aNumFormVersionId);
    bool TryDecode(string? aStrPublicId, out int aNumFormVersionId);
}
