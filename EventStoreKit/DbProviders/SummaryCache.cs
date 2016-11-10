
namespace EventStoreKit.Sql.DbProviders
{
    public class SummaryCache<TModel>
    {
        public bool Ready;
        public string Key;
        public int Total;
        public TModel SummaryModel;
    }
}
