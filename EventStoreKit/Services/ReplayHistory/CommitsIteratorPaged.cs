using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using EventStoreKit.DbProviders;
using Newtonsoft.Json;
using NEventStore;
using NEventStore.Persistence;

namespace EventStoreKit.Services.ReplayHistory
{
    public class CommitsIteratorPaged : ICommitsIterator
    {
        #region Private fields

        private readonly IDbProviderFactory DbProviderFactory;
        private const int PageSize = 7000;
        private Commits LastCommit = null;

        #endregion

        #region Private methods

        private Dictionary<string, object> ParseHeaders( byte[] data )
        {
            using ( var streamReader = new StreamReader( new MemoryStream( data ), Encoding.UTF8 ) )
            {
                var headers = JsonConvert.DeserializeObject<Dictionary<string, object>>( streamReader.ReadToEnd() );
                return headers;
            }
        }
        private IEnumerable<EventMessage> ParsePayload( byte[] data )
        {
            using ( var streamReader = new StreamReader( new MemoryStream( data ), Encoding.UTF8 ) )
            {
                var value = streamReader.ReadToEnd();
                var payload = JsonConvert.DeserializeObject<List<EventMessage>>(
                    value, 
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        DefaultValueHandling = DefaultValueHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore
                    } );
                return payload;
            }
        }

        #endregion

        public CommitsIteratorPaged( IDbProviderFactory dbProviderFactory )
        {
            DbProviderFactory = dbProviderFactory;
        }

        public void Reset()
        {
            LastCommit = null;
        }

        public List<ICommit> LoadNext()
        {
            var db = DbProviderFactory.Create<Commits>();
            var query = db.Query<Commits>()
                .OrderBy( c => c.CheckpointNumber )
                .AsQueryable();
            if ( LastCommit != null )
                query = query.Where( c => c.CheckpointNumber > LastCommit.CheckpointNumber );

            var result = query.Take( PageSize ).ToList();
            if ( result.Any() )
                LastCommit = result.Last();
            else
                LastCommit = null;

            var commits = result
                .Select( c => (ICommit)new Commit(
                        Bucket.Default,
                        c.StreamId,
                        c.StreamRevision,
                        c.CommitId,
                        c.CommitSequence,
                        c.CommitStamp,
                        c.CheckpointNumber.ToString( CultureInfo.InvariantCulture ),
                        ParseHeaders( c.Headers ),
                        ParsePayload( c.Payload ) ) )
                .ToList();

            return commits;
        }
    }
}
