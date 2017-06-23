using System;
using System.Configuration;

namespace EventStoreKit.Services.Configuration
{
    public abstract class ConfigurationService
    {
        protected string GetAppSetting( string name, string defaultValue = "" )
        {
            return ConfigurationManager.AppSettings["InsertBufferSize"] ?? defaultValue;
        }
        protected T GetAppSetting<T>( string name, Func<string, T> parser, T defaultValue )
        {
            try
            {
                return parser( ConfigurationManager.AppSettings[name] ?? string.Empty );
            }
            catch ( Exception )
            {
                return defaultValue;
            }
        }
    }
}