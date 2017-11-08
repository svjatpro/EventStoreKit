using System;

namespace EventStoreKit.DbProviders
{
    public class Commits
    {
        public string BucketId { get; set; }
        public string StreamId { get; set; }
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