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
        private readonly Dictionary<Type, Func<String, IAtomicReadModel>> _factoryFunctions = new Dictionary<Type, Func<String, IAtomicReadModel>>();

        /// <summary>
        /// Create an explicit readmodel given string Id, id should not be accessible
        /// from any code except the factory.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public T Create<T>(String id) where T : IAtomicReadModel
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
        public IAtomicReadModel Create(Type type, String id)
        {
            if (!_factoryFunctions.TryGetValue(type, out Func<String, IAtomicReadModel> factoryFunc))
            {
                var constructor = type.GetConstructor(new Type[] { typeof(String) });
                _factoryFunctions[type] = _ => (IAtomicReadModel) constructor.CreateInstance(new Object[] { _ });
            }
            return (IAtomicReadModel) _factoryFunctions[type](id);
        }

        public AtomicReadModelFactory AddFactory<T>(Func<String, IAtomicReadModel> function)
        {
            return AddFactory(typeof(T), function);
        }

        public AtomicReadModelFactory AddFactory(Type type, Func<String, IAtomicReadModel> function)
        {
            _factoryFunctions[type] = function;
            return this;
        }

        public int GetReamdodelVersion(Type type)
        {
            var reamodel = Create(type, "Fake Id to create an instance to grab readmodel version");
            return reamodel.ReadModelVersion;
        }
    }
}
