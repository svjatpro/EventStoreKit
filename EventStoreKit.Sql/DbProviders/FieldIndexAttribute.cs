
namespace EventStoreKit.Sql.DbProviders
{
    public class FieldIndexAttribute : System.Attribute
    {
        public FieldIndexAttribute() { }

        public FieldIndexAttribute(string indexName)
        {
            IndexName = indexName;
        }

        public string IndexName { get; set; }
        public bool Unique { get; set; }
        public bool Clustered { get; set; }
    }
}