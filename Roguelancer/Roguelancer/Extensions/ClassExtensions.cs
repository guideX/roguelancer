using System;
using System.Linq;
using System.Linq.Expressions;
public static class ClassExtensions {
    /// <summary>
    /// Get Property Name
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyLambda"></param>
    /// <returns></returns>
    public static string GetPropertyName<T>(this Expression<Func<T>> ft) {
        var me = ft.Body as MemberExpression;
        if (me == null || me.Member == null) return null;
        return me.Member.Name;
    }
    /// <summary>
    /// Get Property Value
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public static object GetPropertyValue(this object obj, string propertyName) {
        if (propertyName != null) {
            return obj.GetType().GetProperties()
               .Single(pi => pi.Name == propertyName)
               .GetValue(obj, null);
        } else {
            return null;
        }
    }
}