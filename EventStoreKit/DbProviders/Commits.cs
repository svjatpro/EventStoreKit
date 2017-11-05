using System;
//using LinqToDB.Mapping;

namespace EventStoreKit.linq2db
{
    //[Table( "Commits", IsColumnAttributeRequired = false )]
    public class Commits
    {
        //[Column( Length = 40 ), NotNull]
        public string BucketId { get; set; }
        //[Column( Length = 40 ), NotNull]
        public string StreamId { get; set; }
        //[Column( Length = 1000 ), NotNull]
        public string StreamIdOriginal { get; set; }
        
        public int StreamRevision { get; set; }
        public short Items { get; set; }
        public Guid CommitId { get; set; }
        public int CommitSequence { get; set; }
        public long CheckpointNumber { get; set; }
        
        public bool Dispatched { get; set; }
        public byte[] Headers { get; set; }
        public byte[] Payload { get; set; }

        public DateTime CommitStamp { get; set; }
    }
}