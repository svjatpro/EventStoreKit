namespace EventStoreKit.DbProviders
{
    /// <summary>
    /// Supported Data Bases
    /// </summary>
    public enum DbConnectionType
    {
        MsSql = 0x01,
        MySql = 0x02,
        SqlLite = 0x03,
    }
}