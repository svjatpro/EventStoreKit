namespace EventStoreKit.Core
{
    public interface IMessageDispatcher<TBasic>
    {
        void Dispatch( object message );
        void Dispatch<TMessage>( TMessage message ) where TMessage : TBasic;
    }
}