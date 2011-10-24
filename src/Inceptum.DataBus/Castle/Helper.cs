using System;
using System.Linq;

namespace Inceptum.DataBus.Castle
{
    internal static class Helper
    {
        public static bool IsImplementationOf(this Type t, Type genericType)
        {
            return t != null && t.IsGenericType && t.GetGenericTypeDefinition() == genericType;
        }


        public static TAttr GetAttribute<TAttr>(Type t)
            where TAttr : Attribute
        {
            return (from a in t.GetCustomAttributes(typeof (TAttr), true) select a as TAttr).FirstOrDefault();
        }
    }
}
