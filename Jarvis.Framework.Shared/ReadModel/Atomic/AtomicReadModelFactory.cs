using Fasterflect;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
    /// <summary>
    /// This is an atomic readmodel factory that should be registered to 
    /// create atomic reamodels (<see cref="IAtomicReadModel"/>)
    /// </summary>
    public class AtomicReadModelFactory : IAtomicReadModelFactory
    {
        private readonly Dictionary<Type, Func<String, Object>> _factoryFunctions = new Dictionary<Type, Func<String, Object>>();

        /// <summary>
        /// Create an explicit readmodel given string Id, id should not be accessible
        /// from any code except the factory.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public T Create<T>(String id)
        {
            return (T)Create(typeof(T), id);
        }

        /// <summary>
        /// It is useful to have a function not generic that can create a redmodel given
        /// its Id.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public Object Create(Type type, String id)
        {
            if (!_factoryFunctions.TryGetValue(type, out Func<String, Object> factoryFunc))
            {
                var constructor = type.GetConstructor(new Type[] { typeof(String) });
                _factoryFunctions[type] = _ => constructor.CreateInstance(new Object[] { _ });
            }
            return _factoryFunctions[type](id);
        }

        public AtomicReadModelFactory AddFactory<T>(Func<String, Object> function)
        {
            return AddFactory(typeof(T), function);
        }

        public AtomicReadModelFactory AddFactory(Type type, Func<String, Object> function)
        {
            _factoryFunctions[type] = function;
            return this;
        }
    }
}
