using EventStoreKit.Handler;

namespace Dummy
{
    /// <summary>
    /// Command handler; 
    ///   restore aggregate to actual state, 
    ///   then call appropriate aggregate method to handle new command, 
    ///   then store all new events, which raised by the aggregate
    /// </summary>
    public class SpeakerHandler : ICommandHandler<GreetCommand, Speaker>
    {
        public void Handle( GreetCommand cmd, CommandHandlerContext<Speaker> context )
        {
            context.Entity.Greet( cmd.Object );
        }
    }
}