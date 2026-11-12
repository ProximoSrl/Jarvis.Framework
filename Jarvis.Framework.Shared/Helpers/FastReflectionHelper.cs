using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Jarvis.Framework.Shared.Helpers
{
    public static class FastReflectionHelper
    {
        /// <summary>
        /// http://rogeralsing.com/2008/02/28/linq-expressions-creating-objects/
        /// </summary>
        /// <param name="ctor"></param>
        /// <returns></returns>
        public static ObjectActivator GetActivator(ConstructorInfo ctor)
        {
            ParameterInfo[] paramsInfo = ctor.GetParameters();

            //create a single param of type object[]
            ParameterExpression param =
                Expression.Parameter(typeof(object[]), "args");

            Expression[] argsExp =
                new Expression[paramsInfo.Length];

            //pick each arg from the params array 
            //and create a typed expression of them
            for (int i = 0; i < paramsInfo.Length; i++)
            {
                Expression index = Expression.Constant(i);
                Type paramType = paramsInfo[i].ParameterType;

                Expression paramAccessorExp =
                    Expression.ArrayIndex(param, index);

                Expression paramCastExp =
                    Expression.Convert(paramAccessorExp, paramType);

                argsExp[i] = paramCastExp;
            }

            //make a NewExpression that calls the
            //ctor with the args we just created
            NewExpression newExp = Expression.New(ctor, argsExp);

            //create a lambda with the New
            //Expression as body and our param object[] as arg
            LambdaExpression lambda =
                Expression.Lambda(typeof(ObjectActivator), newExp, param);

            //compile it
            ObjectActivator compiled = (ObjectActivator)lambda.Compile();
            return compiled;
        }

        public delegate object ObjectActivator(params object[] args);

        /// <summary>
        /// Typed version of <see cref="GetActivator(ConstructorInfo)"/> for a constructor with a
        /// single argument: compiles <c>arg => new T(arg)</c>. The argument is passed with its
        /// own type, so calling the activator does not allocate an object[] nor box a value type
        /// argument. Prefer this one when the type of the argument is known at compile time.
        /// </summary>
        /// <typeparam name="TArg">Type of the single argument of the constructor.</typeparam>
        /// <typeparam name="TResult">Type returned by the activator, the class that declares the
        /// constructor must be assignable to it.</typeparam>
        public static Func<TArg, TResult> GetActivator<TArg, TResult>(ConstructorInfo ctor)
        {
            ParameterInfo[] paramsInfo = ctor.GetParameters();
            if (paramsInfo.Length != 1 || paramsInfo[0].ParameterType != typeof(TArg))
            {
                throw new ArgumentException(
                    $"Constructor of {ctor.DeclaringType?.FullName} must have a single argument of type {typeof(TArg).FullName}",
                    nameof(ctor));
            }

            ParameterExpression arg = Expression.Parameter(typeof(TArg), "arg");
            Expression body = Expression.Convert(Expression.New(ctor, arg), typeof(TResult));
            return Expression.Lambda<Func<TArg, TResult>>(body, arg).Compile();
        }
    }
}
