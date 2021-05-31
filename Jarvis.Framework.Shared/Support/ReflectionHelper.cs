using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Jarvis.Framework.Shared.Support
{
    public static class ReflectionHelper
    {
        public static string GetFullNameWithTypeForwarded(this Type type)
        {
            StringBuilder sb = new StringBuilder();

            if (!type.IsGenericType)
            {
                sb.Append(type.FullName);
            }
            else
            {
                ManageGenericArgument(type, sb);
            }

            return sb.ToString();
        }

        private static void ManageGenericArgument(Type type, StringBuilder sb)
        {
            var arguments = type.GetGenericArguments();
            if (type.IsNested)
            {
                sb.Append($"{type.Namespace}.{GetNestedTypeList(type.DeclaringType)}+{type.Name}");
            }
            else
            {
                sb.Append($"{type.Namespace}.{type.Name}");
            }

            if (type.IsGenericType)
            {
                sb.Append("[");
                foreach (var typeArgument in arguments)
                {
                    string assemblyName = GetForwardedAssemblyName(typeArgument);
                    sb.Append("[");
                    ManageGenericArgument(typeArgument, sb);
                    sb.Append($", {assemblyName}],");
                }
                sb.Length -= 1;
                sb.Append("]");
            }
        }

        private static String GetNestedTypeList(Type firstContainingType)
        {
            Stack<Type> stack = new Stack<Type>();
            while (firstContainingType != null) 
            {
                stack.Push(firstContainingType);
                firstContainingType = firstContainingType.DeclaringType;
            }

            StringBuilder sb = new StringBuilder();

            while (stack.Count > 0) 
            {
                sb.Append(stack.Pop().Name);
                sb.Append("+");
            }

            sb.Length -= 1;
            return sb.ToString();
        }

        private static string GetForwardedAssemblyName(Type type)
        {
            var testedType = type;
            if (type.IsArray)
            {
                testedType = type.GetElementType();
            }

            if (Attribute.GetCustomAttribute(testedType, typeof(TypeForwardedFromAttribute), false) is TypeForwardedFromAttribute attr)
            {
                return attr.AssemblyFullName;
            }

            return testedType.Assembly.FullName;
        }
    }
}
