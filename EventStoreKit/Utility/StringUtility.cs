using System;
using System.Linq;
using Newtonsoft.Json;

namespace EventStoreKit.Utility
{
    public static class StringUtility
    {
        #region Private fields

        private const string Br = "<br />";
        private const string FormatAt = "<br /><font color=blue> {0}</font>";
        private const string FormatIn = "<br /><font color=green> {0}</font>";
        private const string FormatTitle = "<br /><font color=red> {0}</font>";
        private const string FormatInner = "<br /><font color=red> {0}</font>";

        private const string FormatJsonBrace = "<font color=black>&nbsp;{0}</font>";
        private const string FormatJsonName = "<br /><font color=blue>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;{0}</font>";
        private const string FormatJsonValue = "<font color=green>&nbsp;{0}</font>";

        #endregion

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
                        s =>
                        {
                            if ( new[] {"{", "}"}.Contains( s.Trim().Substring( 0, 1 ) ) )
                            {
                                s = string.Format( FormatJsonBrace, s );
                            }
                            else
                            {
                                var prop = s.Split( new[] {':'} );
                                if ( prop.Length > 1 )
                                    s = string.Format( FormatJsonName, prop[0] ) + " : " + string.Format( FormatJsonValue, prop[1] );
                            }
                            return s;
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
