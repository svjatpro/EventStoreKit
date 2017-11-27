using System;
using LinqToDB.Mapping;

namespace OSMD.Common.ReadModels
{
    [Table( "Customers", IsColumnAttributeRequired = false )]
    public class CustomerModel
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        [Column( Length = 50 ), NotNull]
        public string FirstName { get; set; }
        [Column( Length = 50 ), NotNull]
        public string LastName { get; set; }
        [Column( Length = 50 ), NotNull]
        public string MiddleName { get; set; }
        [Column( Length = 150 ), NotNull]
        public string FormattedName { get; set; }
        [Column( Length = 150 ), NotNull]
        public string AltFormattedName { get; set; }

        [Column( Length = 50 ), Nullable]
        public string Citizenship { get; set; }
        [Nullable]
        public DateTime? BornDate { get; set; }
        [Column( Length = 200 ), Nullable]
        public string BornAddress { get; set; }
        [Column( Length = 20 ), Nullable]
        public string ContactPhone { get; set; }

        [Column( Length = 200 ), Nullable]
        public string Passport { get; set; }
        [Column( Length = 20 ), Nullable]
        public string PassportSerie { get; set; }
        [Column( Length = 20 ), Nullable]
        public string PassportNumber { get; set; }
        [Column( Length = 200 ), Nullable]
        public string PassportBy { get; set; }
        [Nullable]
        public DateTime? PassportDate { get; set; }
        [Column( Length = 20 ), Nullable]
        public string DocumentType { get; set; }
    }
}