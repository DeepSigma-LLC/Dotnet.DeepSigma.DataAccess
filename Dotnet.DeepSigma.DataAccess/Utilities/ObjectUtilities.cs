using System.Linq.Expressions;
using System.Reflection;

namespace DeepSigma.DataAccess.Utilities;

internal static class ObjectUtilities
{

    internal static V? GetPropertyValue<T, V>(T objectInstance, string propertyName)
    {
        PropertyInfo? property = typeof(T).GetProperty(propertyName) ??
            throw new ArgumentException($"Property '{propertyName}' not found on type '{typeof(T).Name}'.");
        
        return (V?)property.GetValue(objectInstance);
    }

    internal static V? GetPropertyValue<T, V>(T objectInstance, Expression<Func<T, object>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            PropertyInfo? property = typeof(T).GetProperty(memberExpression.Member.Name) ??
                throw new ArgumentException($"Property '{memberExpression.Member.Name}' not found on type '{typeof(T).Name}'.");
            return (V?)property.GetValue(objectInstance);
        }
        else
        {
            throw new ArgumentException("Expression is not a member expression.");
        }
    }
}
