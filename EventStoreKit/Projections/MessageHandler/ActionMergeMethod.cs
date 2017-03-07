namespace EventStoreKit.Projections.MessageHandler
{
    public enum ActionMergeMethod
    {
        SingleDontReplace = 0x00,
        SingleReplaceExisting = 0x01,
        MultipleRunAfter = 0x02,
        MultipleRunBefore = 0x03
    }
}