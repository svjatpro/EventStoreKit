using CommandLine;

namespace DataImport.CommandLine.Commands;

[Verb( "import", HelpText = "Rebuild all projections in the database" )]
public class HandleCommand : BaseCommand
{
    #region Private members
  
    //private void WriteToJson( IEnumerable<ImportItem> data, string fileName )
    //{
    //    var destName = Path.ChangeExtension( Path.Combine( Destination!, fileName ), ".json" );
    //    var destFile = new FileInfo( destName );
    //    if ( destFile.Exists ) destFile.Delete();

    //    using var destStream = new FileStream( destName, FileMode.Create, FileAccess.Write );
    //    using var writer = new StreamWriter( destStream );

    //    var groups = data
    //        .GroupBy( i => i.Type )
    //        .ToDictionary( g => g.Key, g => g.Select( i => i.Properties ).ToArray() );

    //    writer.Write( JsonConvert.SerializeObject( groups, Formatting.Indented ) );
    //}
    
    #endregion

    [Option( 's', "source", Default = "", HelpText = "source file" )]
    public required string SourcePath { get; set; }

    [Option( 'd', "destination", HelpText = "Destination file" )]
    public string? Destination { get; set; }

    [Option( 'c', "config", HelpText = "import config file" )]
    public required string ConfigPath { get; set; }

    public override void Execute()
    {
        var config = new ImportConfig
        {
            Items = [new ImportItemConfig
            {
                TypeName = "Reserved",
                //Fields = []
                Strategies = new Dictionary<int, FieldStrategy>
                {
                    //{0, FieldStrategy
                    //        .Pairs(
                    //            i => $"{name}OrderDate{i.Index()}",
                    //            i => $"{name}OrderNum{i.Index()}",
                    //            ValueMatcher.ByRegex( orderDatePattern, "OrderDate" ),
                    //            ValueMatcher.ByRegex( orderNumberPatterns, "OrderNumber" ),
                    //            LinesStrategy.Extract.All() )
                    //        .ThenExtract( $"{name}Order",
                    //            ValueMatcher.ByRegex( orderNumberSeparatePattern, "OrderNumber" ),
                    //            LinesStrategy.AllOrFirst.WithExtract().Single() )
                    //        .ThenExtract( $"{name}Date",
                    //            ValueMatcher.ByRegex( datePattern, "OrderDate" ),
                    //            LinesStrategy.AllOrFirst.Single() )
                    //        .Then( FieldStrategy
                    //            .Get( $"Death", ValueMatcher.ByPhrases( deathPatterns ).SingleValue( _ => "200" ) )
                    //            .Or( FieldStrategy
                    //                .Exist( ValueMatcher.ByPhrases( ["розпорядження"] ) )
                    //                .And( FieldStrategy
                    //                    .Extract( $"{name}Reserved",
                    //                        ValueMatcher.Execute( val =>
                    //                            ParseReservedReason( val, out var resReason ) ? [new MatchedValue( resReason )] : [] ),
                    //                        LinesStrategy.Get.Single() )
                    //                    .OrExtract( $"{name}Reserved",
                    //                        ValueMatcher.Const( "Other" ), LinesStrategy.Get.Single() ) ) )
                    //            .Or( FieldStrategy.Options(
                    //                ImportItem.Fields.DischargeReason,
                    //                ImportItem.Fields.DischargeSubReason,
                    //                dischargeReasons ) )
                    //            .Or( FieldStrategy.Get( ImportItem.Fields.DischargeReason, ValueMatcher
                    //                .ByPhrases( ["виключений", "звільнений"] ).SingleValue( _ => "Other" ) ) ) )
                    //        .Then( FieldStrategy
                    //            .Get( $"{name}UnitAlt", ValueMatcher.ByRegex( @"\b[А-ЯЁҐЄІЇ]\s?[0-9]{4}\b" ),
                    //                LinesStrategy.Get.Single() )
                    //            .Or( FieldStrategy.Options( $"{name}UnitAlt", unitOptions,
                    //                LinesStrategy.Get.Single() ) ) )
                    //        .ThenGet( $"{name}Position",
                    //            ValueMatcher.NotEmpty(),
                    //            LinesStrategy.Get.Single() )
                    //}
                }
            }]
        };

        var importer = new DataImporter();
        var result = importer.Import( SourcePath, config );
    }
}