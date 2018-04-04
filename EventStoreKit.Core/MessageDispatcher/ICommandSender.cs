
namespace EventStoreKit.Core
{
    public interface ICommandSender<TBasic> where TBasic : class
    {
        void SendCommand<TCommand>( TCommand command ) where TCommand : TBasic;
    }
}