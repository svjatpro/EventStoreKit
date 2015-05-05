using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;

namespace EventStoreKit.Utility
{
    public static class EnumUtility
    {
        public static string GetEnumDescription(this Enum value)
        {
            if (value == null)
                return String.Empty;

            if (!value.GetType().IsEnum)
            {
                throw new InvalidOperationException("Value is not an enum");
            }
            var attributes = value.GetType().GetMember(value.ToString())[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
            return !attributes.Any() ? value.ToString() : ((DescriptionAttribute)attributes[0]).Description;
        }

        public static T GetValueFromDescription<T>(this Enum e, string description)
        {
            var type = typeof(T);
            if (!type.IsEnum) throw new InvalidOperationException();
            foreach (var field in type.GetFields())
            {
                var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
                if (attribute != null)
                {
                    if (attribute.Description == description)
                        return (T)field.GetValue(null);
                }
                else
                {
                    if (field.Name == description)
                        return (T)field.GetValue(null);
                }
            }
            throw new ArgumentException(string.Format("Coudn't extract value from {0} for {1} type", description, type.Name), "description");
        }

        public static T GetEnumFromName<T>(this Enum e, string name)
        {
            string[] names = Enum.GetNames(typeof(T));
            if (((IList)names).Contains(name))
            {
                return (T)Enum.Parse(typeof(T), name);
            }
            return default(T);
        }       
    }
}
