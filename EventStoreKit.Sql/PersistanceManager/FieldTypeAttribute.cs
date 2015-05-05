using System.Globalization;

namespace EventStoreKit.Sql.PersistanceManager
{
    public class FieldTypeAttribute : System.Attribute
    {
        public FieldTypeAttribute(string type, string length)
        {
            TypeName = type;
            Length = length;
        }

        public FieldTypeAttribute(string type, int length) : this(length)
        {
            TypeName = type;
        }

        public FieldTypeAttribute(int length)
        {
            Length = length.ToString(CultureInfo.InvariantCulture);
        }

        public FieldTypeAttribute() { }

        public string TypeName { get; protected set; }
        public string Length { get; set; }
    }
}