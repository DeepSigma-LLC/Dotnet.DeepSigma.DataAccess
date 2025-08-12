using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Dotnet.DeepSigma.DataAccess.Utilities
{
    internal static class ObjectUtilities
    {

        internal static V GetPropertyValue<T, V>(T objectInstance, string propertyName)
        {
            var property = typeof(T).GetProperty(propertyName);
            if (property == null)
            {
                throw new ArgumentException($"Property '{propertyName}' not found on type '{typeof(T).Name}'.");
            }
            return (V)property.GetValue(objectInstance);
        }

        internal static V GetPropertyValue<T, V>(T objectInstance, Expression<Func<T, object>> expression)
        {
            if (expression.Body is MemberExpression memberExpression)
            {
                var property = typeof(T).GetProperty(memberExpression.Member.Name);
                if (property == null)
                {
                    throw new ArgumentException($"Property '{memberExpression.Member.Name}' not found on type '{typeof(T).Name}'.");
                }
                return (V)property.GetValue(objectInstance);
            }
            else
            {
                throw new ArgumentException("Expression is not a member expression.");
            }
        }
    }
}
