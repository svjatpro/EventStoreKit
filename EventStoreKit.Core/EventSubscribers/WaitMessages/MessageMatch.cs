using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EventStoreKit.Messages;
using EventStoreKit.Utility;

namespace EventStoreKit.Core.EventSubscribers
{
    public class MessageMatch
    {
        #region Private fields

        private readonly List<Func<Message, bool>> MandatoryHandlers;
        private readonly List<Func<Message, bool>> BreakingHandlers;
        private bool Sequence { get; set; }
        private bool Started;
        private Stopwatch Timer;
        private int Timeout;

        #endregion

        #region Private methods

        private void ValidateState()
        {
            if( Started )
                throw new InvalidOperationException( "Can't set timeout for already started object" );
        }

        private bool ProcessBreaking( Message message )
        {
            if( BreakingHandlers.Any( match => match( message ) ) )
            {
                MandatoryHandlers.Clear();
                BreakingHandlers.Clear();
                ResultMessages.Clear();
                ResultMessages.Add( message );
                return true;
            }
            return false;
        }
        private void ProcessOrdered( Message message )
        {
            if( MandatoryHandlers.Any() && MandatoryHandlers[0].With( match => match( message ) ) )
            {
                MandatoryHandlers.RemoveAt( 0 );
                ResultMessages.Add( message );
            }
        }

        private void ProcessUnordered( Message message )
        {
            var handler = MandatoryHandlers.FirstOrDefault( match => match( message ) );
            handler.Do( h =>
            {
                MandatoryHandlers.Remove( h );
                ResultMessages.Add( message );
            } );
        }

        #endregion

        public readonly TaskCompletionSource<List<Message>> TaskCompletionSource = new TaskCompletionSource<List<Message>>();
        public readonly List<Message> ResultMessages;

        public MessageMatch()
        {
            MandatoryHandlers = new List<Func<Message, bool>>();
            BreakingHandlers = new List<Func<Message, bool>>();
            ResultMessages = new List<Message>();

            Timeout = 20000; // to prevent infinite task
        }

        public void Start()
        {
            Started = true;
            Timer = new Stopwatch();
            Timer.Start();
        }
        public void ProcessMessage( Message message )
        {
            if ( TaskCompletionSource.Task.IsCompleted )
                return;
            if ( !Started )
                Start();

            message
                .Do( msg =>
                {
                    try
                    {
                        if( Timer.ElapsedMilliseconds > Timeout )
                        {
                            throw new TimeoutException();
                        }

                        var breaking = ProcessBreaking( message );

                        if( !breaking )
                        {
                            if ( Sequence )
                                ProcessOrdered( msg );
                            else
                                ProcessUnordered( msg );
                        }

                        if( breaking || !MandatoryHandlers.Any() )
                        {
                            TaskCompletionSource.SetResult( ResultMessages );
                        }
                    }
                    catch( Exception exc )
                    {
                        MandatoryHandlers.Clear();
                        BreakingHandlers.Clear();
                        TaskCompletionSource.SetException( exc );
                    }
                } );
        }

        public MessageMatch And<TMessage>( Func<TMessage, bool> predicat ) where TMessage : Message
        {
            ValidateState();
            MandatoryHandlers.Add( msg => msg.OfType<TMessage>().With( predicat ) );
            return this;
        }

        public MessageMatch Ordered()
        {
            ValidateState();
            Sequence = true;
            return this;
        }

        public MessageMatch WithTimeout( int timeout )
        {
            ValidateState();
            Timeout = timeout;
            return this;
        }

        public MessageMatch BreakBy<TMessage>( Func<TMessage, bool> predicat ) where TMessage : Message
        {
            ValidateState();
            BreakingHandlers.Add( msg => msg.OfType<TMessage>().With( predicat ) );
            return this;
        }
        
        public static MessageMatch Is<TMessage>( Func<TMessage, bool> predicat ) where TMessage : Message
        {
            var match = new MessageMatch().And( predicat );
            return match;
        }
    }
}