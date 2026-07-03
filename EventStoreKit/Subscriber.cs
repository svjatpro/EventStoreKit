using LinqToDB.Mapping;

namespace EventStoreKit;

[Table("subscriber", IsColumnAttributeRequired = false)]
public class Subscriber
{
    public long LastToken { get; set; } = 0;
}