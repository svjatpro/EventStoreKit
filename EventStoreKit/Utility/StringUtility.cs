using System;
using System.Linq;
using Newtonsoft.Json;

namespace EventStoreKit.Utility
{
    public static class StringUtility
    {
        #region Private fields

        private const string Br = "<br />";
        private const string Space = "&nbsp;";
        
        private const string FontBlack = "<font color=black>";
        private const string FontBlue = "<font color=blue>";
        private const string FontGreen = "<font color=green>";
        private const string FontRed = "<font color=red>";
        private const string FontEnd = "</font>";


        private const string FormatAt = "<br /><font color=blue> {0}</font>";
        private const string FormatIn = "<br /><font color=green> {0}</font>";
        private const string FormatTitle = "<br /><font color=red> {0}</font>";
        private const string FormatInner = "<br /><font color=red> {0}</font>";

        #endregion

        public static string Left( this string source, int length )
        {
            var actualLength = source.Length;
            return 
                actualLength <= length ?
                source :
                source.Substring( 0, length );
        }

        public static string FormatHtmlException( this string exception )
        {
            var strings = exception
                .Split( new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries )
                .Select( s => s.Trim() )
                .Select(
                    s =>
                    {
                        if ( s.Substring( 0, 3 ) == "at " )
                        {
                            var innerIndex = s.IndexOf( "--- End of inner ", StringComparison.Ordinal );
                            var inner = string.Empty;
                            if ( innerIndex != -1 )
                            {
                                inner = string.Format( FormatInner, s.Substring( innerIndex ) );
                                s = s.Substring( 0, innerIndex );
                            }

                            var inIndex = s.IndexOf( " in ", StringComparison.Ordinal );
                            var at = string.Format( FormatAt, inIndex != -1 ? s.Substring( 0, inIndex ) : s );
                            var @in = inIndex != -1 ? string.Format( FormatIn, s.Substring( inIndex ) ) : string.Empty;
                            s = at + @in + inner;
                        }
                        else
                        {
                            s = string.Format( FormatTitle, s.Replace( "---> ", Br + "---> " ) );
                        }
                        return s;
                    } )
                .ToList();

            return string.Join( "", strings );
        }

        public static string FormatHtmlEvent( this string @event )
        {
            if( @event == null || @event == "(null)" )
                return string.Empty;
            try
            {
                var strings = JsonConvert.SerializeObject( JsonConvert.DeserializeObject( @event ), Formatting.Indented )
                    .Split( new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries )
                    .Select(
                        ( s, i ) =>
                        {
                            var line = s.TrimEnd().Replace( " ", Space );

                            var prop = line.Split( new[] { ':' } );
                            if ( prop.Length > 1 )
                            {
                                line = string.Format( "{0}{1}{2}{3}:{4}{5}{6}", Br, FontBlue, prop[0], FontEnd, FontGreen, prop[1], FontEnd );
                            }
                            else
                            {
                                line = string.Format( "{0}{1}{2}{3}", ( i == 0 ? "" : Br ), FontBlack, line, FontEnd );
                            }
                            return line;
                        } )
                    .ToList();
                return string.Join( "", strings );
            }
            catch ( JsonException ex )
            {
                return @event;
            }
        }
    
        public static string GetInitialCatalog( this string connectionString )
        {
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder( connectionString );
            return builder.InitialCatalog;
        }
    }
}
