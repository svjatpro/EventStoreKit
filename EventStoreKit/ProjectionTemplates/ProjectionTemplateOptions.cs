using System;

namespace EventStoreKit.ProjectionTemplates
{
    [Flags]
    public enum ProjectionTemplateOptions
    {
        None = 0x0000,

        InsertCaching = 0x0001,

        ReadCachingSingle = 0x0012,
    }
}
