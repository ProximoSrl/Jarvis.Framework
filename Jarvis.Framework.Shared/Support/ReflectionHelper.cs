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
            //if this is an array we simple want to handle base type then append [] at the end.
            var examinedType = type;
            if (type.IsArray)
            {
                examinedType = type.GetElementType();
            }

            if (examinedType.IsNested)
            {
                sb.Append($"{type.Namespace}.{GetNestedTypeList(type.DeclaringType)}+{type.Name}");
            }
            else
            {
                if (examinedType.Namespace == null)
                {
                    sb.Append($"{examinedType.Name}");
                }
                else
                {
                    sb.Append($"{examinedType.Namespace}.{examinedType.Name}");
                }
            }

            if (examinedType.IsGenericType)
            {
                var arguments = examinedType.GetGenericArguments();
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
            if (type.IsArray) 
            {
                sb.Append("[]");
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
