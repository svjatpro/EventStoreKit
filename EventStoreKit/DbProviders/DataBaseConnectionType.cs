namespace EventStoreKit.DbProviders
{
    /// <summary>
    /// Supported Data Bases
    /// </summary>
    public enum DataBaseConnectionType
    {
        None = 0x00,

        MsSql2000 = 0x01,
        MsSql2005 = 0x02,
        MsSql2008 = 0x03,
        MsSql2012 = 0x04,

        MySql = 0x05,
        SqlLite = 0x06,
    }
}