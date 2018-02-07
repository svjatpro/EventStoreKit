using System;

namespace EventStoreKit.Services
{
    public class ServiceProperty<TPropertyValue> : IServiceProperty
        where TPropertyValue : class
    {
        private readonly Func<TPropertyValue> DefaultValueInitializer;
        private TPropertyValue InitializedValue;
        private TPropertyValue DefaultValue;
        private bool Initialized;

        public TPropertyValue Value
        {
            get => InitializedValue;
            set
            {
                if ( !Initialized )
                    InitializedValue = value;
            }
        }

        public TPropertyValue Default
        {
            get
            {
                if ( DefaultValue == null )
                    DefaultValue = DefaultValueInitializer();
                return DefaultValue;
            }
        }

        public ServiceProperty( Func<TPropertyValue> defaultValueInitializer )
        {
            DefaultValueInitializer = defaultValueInitializer;
        }

        public void Initialize()
        {
            if ( Initialized )
                return;

            if ( InitializedValue == null )
                InitializedValue = Default;

            Initialized = true;
        }

        public TPropertyValue GetValueOrDefault()
        {
            return InitializedValue ?? Default;
        }
    }
}