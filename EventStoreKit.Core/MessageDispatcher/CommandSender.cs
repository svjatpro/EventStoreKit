
using EventStoreKit.Utility;

namespace EventStoreKit.Core
{
    public class CommandSender<TBasic> : ICommandSender<TBasic> where TBasic : class
    {
        #region Private fields

        private readonly IMessageDispatcher<TBasic> Dispatcher;

        #endregion

        public CommandSender( IMessageDispatcher<TBasic> dispatcher )
        {
            Dispatcher = dispatcher.CheckNull( nameof(dispatcher) );
        }

        public void SendCommand<TCommand>( TCommand command ) where TCommand : TBasic
        {
            Dispatcher.Dispatch( command );
        }
    }
}