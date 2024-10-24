using System.Linq.Expressions;
using System.Reflection;

namespace EventStoreKit.Core.Extensions;

public static class ExpressionsUtility
{
    public static TDestination CopyTo<TSource, TDestination>(
        this TSource source,
        Expression<Func<TSource, TDestination>> customAssigns)
    {
        var sourceType = typeof(TSource);
        var destType = typeof(TDestination);
        var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
        var destProperties = destType.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
        var customBindings = (customAssigns.Body is MemberInitExpression binding) ? binding.Bindings.ToList() : [];
        var parameter = customAssigns.Parameters[0];

        var bindings = destProperties
            .Select(destProp =>
            {
                var customBinding = customBindings.SingleOrDefault(b => b.Member.Name == destProp.Name);
                if (customBinding != null)
                {
                    return customBinding;
                }
                var prop = sourceProperties.SingleOrDefault(p => p.Name == destProp.Name);
                return prop != null ? Expression.Bind(destProp, Expression.MakeMemberAccess(parameter, prop)) : null;
            })
            .Where(bind => bind != null)
            .ToList();
        var ctor = Expression.New(destType);
        var memberInit = Expression.MemberInit(ctor, bindings!);
        var lambda = Expression.Lambda<Func<TSource, TDestination>>(memberInit, parameter);

        return lambda.Compile()(source);
    }
}
