//using NATS.Client.JetStream;
//using NATS.Client.JetStream.Models;
//using NEventStore;
//using Newtonsoft.Json;

//namespace EventStoreKit;

//public class NatsQueryEventsStore : IQueryEventsStore
//{
//    #region Private fields
        
//    private const string GlobalStream = "global";
//    private const string GlobalEsPrefix = "es";

//    #endregion

//    private NatsJSContext Js { get; }

//    public NatsQueryEventsStore(NatsJSContext js)
//    {
//        Js = js;

//        Js.CreateStreamAsync( new StreamConfig( name: GlobalStream, subjects: [$"{GlobalEsPrefix}.*"] ) )
//           .GetAwaiter().GetResult();
//    }

//    public void Append<T>(Guid streamId, T @event)
//    {
//        var data = JsonConvert.SerializeObject(@event);
//        var ack = Js
//           .PublishAsync(subject:$"{GlobalEsPrefix}.{streamId.ToString()}", data: data)
//           .GetAwaiter().GetResult();
//        ack.EnsureSuccess();
//    }

//    public void Append(Guid streamId, IEnumerable<object> events)
//    {
//        foreach (var @event in events)
//        {
//            Append(streamId, @event);
//        }        
//    }

//    //public async Task<List<string>> GetAllInternal()
//    //{
//    //    var streams = Js.ListStreamsAsync();
//    //    var result = new List<string>();
//    //    await foreach ( var stream in streams )
//    //    {
//    //        var streamInfo = stream.Info;
//    //        var subjects = streamInfo.Config.Subjects;

//    //        foreach ( var subject in subjects )
//    //        {
//    //            await FetchMessagesFromStream( Js, subject );
//    //        }
//    //    }

//    //    async Task FetchMessagesFromStream( NatsJSContext js, string subject )
//    //    {
//    //        var options = new NatsJSFetchOpts();
//    //        var subscription = await js.PullSubscribeAsync( subject, options );

//    //        //bool hasMoreMessages = true;

//    //        //while ( hasMoreMessages )
//    //        //{
//    //        //    var messages = await subscription.FetchAsync( 100, timeout: 1000 );

//    //        //    if ( messages.Count == 0 )
//    //        //    {
//    //        //        hasMoreMessages = false;
//    //        //    }

//    //        //    foreach ( var msg in messages )
//    //        //    {
//    //        //        Console.WriteLine(
//    //        //            $"Stream: {streamName}, Subject: {subject}, Message: {System.Text.Encoding.UTF8.GetString( msg.Data )}" );
//    //        //        msg.Ack();
//    //        //    }
//    //        //}
//    //    }

//    //    return result;
//    //}

//    private async Task<List<EventMessage>> GetAllInternal()
//    {
//        var streams = Js.ListStreamsAsync();
//        var result = new List<EventMessage>();
//        await foreach ( var stream in streams )
//        {
//            var streamInfo = stream.Info;
//            var subjects = streamInfo.Config.Subjects;

//            foreach ( var subject in subjects )
//            {
//                await FetchMessagesFromStream( Js, subject );
//            }
//        }

//        async Task FetchMessagesFromStream( NatsJSContext js, string subject )
//        {
//        }

//        return result;
//    }

//    public IEnumerable<EventMessage> GetAllEvents()
//    {
//        return GetAllInternal().GetAwaiter().GetResult();
        

//        //var streams = Js.ListStreamsAsync();
//        //var result = new List<string>();
//        //foreach ( var stream in streams )
//        //{
//        //    var streamInfo = stream.Info;
//        //    var subjects = streamInfo.Config.Subjects;

//        //    foreach ( var subject in subjects )
//        //    {
//        //        await FetchMessagesFromStream( Js, subject );
//        //    }
//        //}

//        //async Task FetchMessagesFromStream( NatsJSContext js, string subject )
//        //{
//        //}

//        //var consumer = Js
//        //    .CreateOrUpdateConsumerAsync($"{GlobalEsPrefix}", new ConsumerConfig("global"))
//        //    .GetAwaiter().GetResult();

//        //await foreach (var jsMsg in consumer.ConsumeAsync<string>())
//        //{
//        //    Console.WriteLine($"Processed: {jsMsg.Data}");
//        //    await jsMsg.AckAsync();
//        //}

//        //var consumer = Js
//        //    .CreateOrUpdateConsumerAsync(stream: $"{GlobalEsPrefix}", new ConsumerConfig("fetcher"))
//        //    .GetAwaiter().GetResult();

//        //foreach (var msg in consumer...ConsumeAsync().WithCancellation(cancellationToken))
//        //{
//        //    // Process message
//        //    await msg.AckAsync();

//        //    // loop never ends unless there is a terminal error, cancellation or a break
//        //}

//        //var streams = Js.ListStreamsAsync();
//        //var result = new List<string>();



//        //var consumer = Js.PullSubscribeAsync(subject: $"{GlobalEsPrefix}.*", durable: "all-reader")
//        //   .GetAwaiter().GetResult();

//        //var messages = consumer.Fetch(1000, timeout: Duration.OfSeconds(5));

//        //foreach (var msg in messages)
//        //{
//        //    var data = JsonSerializer.Deserialize<object>(msg.Data); // Adjust deserialization
//        //    yield return new EventMessage
//        //    {
//        //        Body = data!,
//        //        Headers = new Dictionary<string, object>
//        //        {
//        //            ["Subject"] = msg.Subject,
//        //            ["StreamId"] = msg.Subject.Substring(GlobalEsPrefix.Length + 1) // es.{streamId}
//        //        },
//        //    };
//        //    msg.Ack();
//        //}   

//        return [];
//    }
//}
