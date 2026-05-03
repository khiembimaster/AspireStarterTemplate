using EventFlow.ValueObjects;
using Newtonsoft.Json;

namespace SharedKernel;

[JsonConverter(typeof(SingleValueObjectConverter))]
public class TenantId : SingleValueObject<string>
{
    public TenantId(string value) : base(value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("TenantId cannot be empty.", nameof(value));
    }
}
