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

        /// <summary>
        /// Create an instance of <see cref="IAtomicReadModel"/> based on Type and Id
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        IAtomicReadModel Create(Type type, String id);

        /// <summary>
        /// Create an instance of <see cref="IAtomicReadModel"/> based on Type and Id
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        T Create<T>(String id) where T : IAtomicReadModel;

        /// <summary>
        /// Get readmodel version associated with <see cref="IAtomicReadModel"/> of type <paramref name="type"/>
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        Int32 GetReamdodelVersion(Type type);
    }
}