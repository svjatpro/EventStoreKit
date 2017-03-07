using System;

namespace EventStoreKit.Constants
{
    public class EventStoreConstants
    {
        public static Guid RebuildSessionIdentity = new Guid( "77ED6A96-24C9-BF4D-6FEB-39CE00A43093" );

        public static string ProjectionsConfigNameTag = "ProjectionsConfig";

        public static string UndispatchedMessage = "UndispatchedMessage.";
        public static string SagaType = "SagaType";
    }
}
