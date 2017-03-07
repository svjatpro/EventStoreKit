using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EventStoreKit.Messages;
using EventStoreKit.Utility;

namespace EventStoreKit.Projections.MessageHandler
{
    internal class DynamicMessageHandler<TMessage> : IMessageHandler
        where TMessage : Message
    {
        #region Private fields

        private readonly List<Func<TMessage, bool>> MandatoryHandlers;
        private readonly List<Func<TMessage, bool>> OptionalHandlers;

        private readonly Stopwatch Timer;
        private readonly int Timeout;
        private readonly bool Sequence;

        #endregion

        #region Private methods

        private void ProcessOrdered( TMessage message )
        {
            var processed = 0;
            if ( MandatoryHandlers.FirstOrDefault().With( match => match( message ) ) )
            {
                MandatoryHandlers.RemoveAt( 0 );
                processed = 1;
            }
            processed += OptionalHandlers.RemoveAll( match => match( message ) );
            if ( processed > 0 )
                ResultMessages.Add( message );
        }

        private void ProcessUnordered( TMessage message )
        {
            var processed = 0;

            var handler = MandatoryHandlers.FirstOrDefault( match => match( message ) );
            handler.Do( h =>
            {
                MandatoryHandlers.Remove( h );
                processed = 1;
            } );

            processed += OptionalHandlers.RemoveAll( match => match( message ) );
            if ( processed > 0 )
                ResultMessages.Add( message );
        }

        #endregion

        public readonly TaskCompletionSource<List<TMessage>> TaskCompletionSource = new TaskCompletionSource<List<TMessage>>();
        public readonly List<TMessage> ResultMessages;

        public DynamicMessageHandler(
            IEnumerable<Func<TMessage, bool>> mandatory, 
            IEnumerable<Func<TMessage, bool>> optional,
            int timeout,
            bool sequence )
        {
            MandatoryHandlers = mandatory.With( m => m.ToList() ) ?? new List<Func<TMessage, bool>>();
            OptionalHandlers = optional.With( m => m.ToList() ) ?? new List<Func<TMessage, bool>>();
            ResultMessages = new List<TMessage>();
            
            Timeout = timeout;
            Sequence = sequence;

            Timer = new Stopwatch();
            Timer.Start();
        }

        public Type Type { get { return typeof( TMessage ); } }
        public bool IsAlive { get { return MandatoryHandlers.Any(); } }

        public virtual void Process( Message message )
        {
            message
                .OfType<TMessage>()
                .Do( msg =>
                {
                    try
                    {
                        if ( Timer.ElapsedMilliseconds > Timeout )
                        {
                            throw new TimeoutException();
                        }

                        if ( Sequence )
                            ProcessOrdered( msg );
                        else
                            ProcessUnordered( msg );

                        if ( !MandatoryHandlers.Any() )
                        {
                            TaskCompletionSource.SetResult( ResultMessages );
                        }
                    }
                    catch ( Exception exc )
                    {
                        MandatoryHandlers.Clear();
                        OptionalHandlers.Clear();
                        TaskCompletionSource.SetException( exc );
                    }
                } );
        }

        public virtual IMessageHandler Combine( Action<Message> process, bool runBefore = false )
        {
            throw new NotImplementedException();
        }
    }
}