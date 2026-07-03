//using NATS.Client.Core;
//using NATS.Client.JetStream;

//namespace EventStoreKit;

//public class NatsEventStoreKit //: IEventStoreKit
//{
//    public IQueryEventsStore Events { get; }

//    private NatsConnection Nats { get; }
//    private NatsJSContext Js { get; }

//    public NatsEventStoreKit()
//    {
//        //Nats = new NatsConnection(NatsOpts.Default with { Url = "nats://localhost:4223" });
//        Nats = new NatsConnection();
//        Js = new NatsJSContext( Nats );

//        Events = new NatsQueryEventsStore(Js);
//    }
//}
