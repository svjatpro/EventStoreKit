﻿using System;
using System.Security.Cryptography;

namespace EventStoreKit.Services.IdGenerators
{
    public class SequentialIdgenerator : IIdGenerator
    {
        public enum SequentialGuidType
        {
            SequentialAsString,
            SequentialAsBinary,
            SequentialAtEnd
        }
        
        private readonly RNGCryptoServiceProvider rng;

        public SequentialIdgenerator( )
        {
            rng = new RNGCryptoServiceProvider();
        }

        #region Implementation of IIdGenerator

        public Guid NewGuid( )
        {
            // http://www.codeproject.com/Articles/388157/GUIDs-as-fast-primary-keys-under-multiple-database
            // "For SQL Server, we would expect the SequentialAtEnd method to work best (since it was added especially for SQL Server)"
            return NewSequentialGuid( SequentialGuidType.SequentialAtEnd );
        }

        #endregion

        private Guid NewSequentialGuid( SequentialGuidType guidType )
        {
            var randomBytes = new byte[10];
            rng.GetBytes( randomBytes );

            var timestamp = DateTime.Now.Ticks / 10000L;
            var timestampBytes = BitConverter.GetBytes( timestamp );

            if ( BitConverter.IsLittleEndian )
                Array.Reverse( timestampBytes );
            var guidBytes = new byte[16];

            switch ( guidType )
            {
                case SequentialGuidType.SequentialAsString:
                case SequentialGuidType.SequentialAsBinary:
                    Buffer.BlockCopy( timestampBytes, 2, guidBytes, 0, 6 );
                    Buffer.BlockCopy( randomBytes, 0, guidBytes, 6, 10 );
                    // If formatting as a string, we have to reverse the order
                    // of the Data1 and Data2 blocks on little-endian systems.
                    if ( guidType == SequentialGuidType.SequentialAsString &&
                         BitConverter.IsLittleEndian )
                    {
                        Array.Reverse( guidBytes, 0, 4 );
                        Array.Reverse( guidBytes, 4, 2 );
                    }
                    break;
                case SequentialGuidType.SequentialAtEnd:
                    Buffer.BlockCopy( randomBytes, 0, guidBytes, 0, 10 );
                    Buffer.BlockCopy( timestampBytes, 2, guidBytes, 10, 6 );
                    break;
            }
            return new Guid( guidBytes );
        }
    }
}
