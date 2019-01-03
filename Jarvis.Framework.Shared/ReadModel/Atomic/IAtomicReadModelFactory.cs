using System;

namespace Jarvis.Framework.Shared.ReadModel.Atomic
{
    /// <summary>
    /// Interface to create readmodels.
    /// </summary>
    public interface IAtomicReadModelFactory
    {
        /// <summary>
        /// Add a factorty function to create a specific readmodel.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="function"></param>
        /// <returns></returns>
        AtomicReadModelFactory AddFactory(Type type, Func<String, IAtomicReadModel> function);

        /// <summary>
        /// See <see cref="AddFactory(Type, Func{string, object})"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="function"></param>
        /// <returns></returns>
        AtomicReadModelFactory AddFactory<T>(Func<String, IAtomicReadModel> function);

        IAtomicReadModel Create(Type type, String id);

        T Create<T>(String id) where T : IAtomicReadModel;
    }
}