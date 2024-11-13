﻿using Jarvis.Framework.Shared.ReadModel.Atomic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Atomic;

/// <summary>
/// Simple interface to expose <see cref="IQueryable{T}"/> interface,
/// so we can take advange of covariance in readmodels too.
/// </summary>
/// <typeparam name="TModel"></typeparam>
public interface IAtomicCollectionReaderQueryable<out TModel>
    where TModel : IAtomicReadModel
{
    /// <summary>
    /// Allow standard IQueryable interface
    /// </summary>
    IQueryable<TModel> AsQueryable();

    /// <summary>
    /// <para>
    /// This is the same of <see cref="AsQueryable"/>, but this will read preferred on secondary if we have replicaset.
    /// Actually we have all readmodels on mongodb, so this is somewhat leaked abstraction. If we will ever use a SQL
    /// Server or other storage, this method will be probably is equal to <see cref="AsQueryable"/>.
    /// </para>
    /// <para>
    /// This is used to allow reading on secondary in mongo, accepting the fact that we can read stale data but not 
    /// using the primary.
    /// </para>
    /// </summary>
    IQueryable<TModel> AsQueryableSecondaryPreferred();
}

/// <summary>
/// Simple wrapper for readmodel atomic collection. There is no need
/// for corresponding reader, this is the only interface that will
/// be used to access atomic readmodels, that are inherited by <see cref="AbstractAtomicReadModel"/>
/// </summary>
/// <typeparam name="TModel"></typeparam>
public interface IAtomicCollectionReader<TModel>
    : IAtomicCollectionReaderQueryable<TModel>
    where TModel : IAtomicReadModel
{
    /// <summary>
    /// Find a readmodel by id and does some checking like automatic rebuild
    /// of superseded readmodel, etc.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TModel> FindOneByIdAsync(String id, CancellationToken cancellationToken = default);

    /// <summary>
    /// This is similar to <see cref="FindOneByIdAsync(string)"/> but it will re-query event stream
    /// to project extra events. This ensure that you got the must up-to-date readmodel.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TModel> FindOneByIdAndCatchupAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find by query and allow for an optional parameter called <paramref name="fixVersion"/> that allows
    /// to fix for version if you need to be 100% sure to read latest version (upgrade skew)
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="fixVersion"></param>
    /// <returns></returns>
    Task<IReadOnlyCollection<TModel>> FindManyAsync(Expression<Func<TModel, bool>> filter, bool fixVersion = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads by id but project until a specific checkpoint passed as parameter. This checkpoint
    /// is the global checkpoint of the store (not Aggregate Version).
    /// </summary>
    /// <param name="id">aggregate id</param>
    /// <param name="chunkPosition">
    /// Position to project the aggregate to.
    /// </param>
    /// <param name="cancellationToken"></param>
    /// <remarks>Actually this method is a simple wrapper to a call to <see cref="ILiveAtomicReadModelProcessor"/>
    /// that was used internally by the reader.</remarks>
    Task<TModel> FindOneByIdAtCheckpointAsync(string id, long chunkPosition, CancellationToken cancellationToken = default);
}

/// <summary>
/// Needed for the fixer, a factory NON GENERIC that have a generic method to 
/// create CollectionWrapper
/// </summary>
public interface IAtomicCollectionWrapperFactory
{
    IAtomicCollectionWrapper<TModel> CreateCollectionWrappper<TModel>()
         where TModel : IAtomicReadModel;
}

/// <summary>
/// General interface to access storage for atomic readmodel.
/// </summary>
/// <typeparam name="TModel"></typeparam>
public interface IAtomicCollectionWrapper<TModel> :
    IAtomicCollectionReader<TModel>
    where TModel : IAtomicReadModel
{
    /// <summary>
    /// Insert or update a readmodel instance, avoid any AggregateVersion check, it will
    /// overwrite the value on the database even if the AggregateVersion of <paramref name="model"/> is
    /// lower than the version on disk
    /// </summary>
    /// <param name="model"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <remarks>Readmodel global signature check is always performed.</remarks>
    Task UpsertForceAsync(TModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert or update a readmodel instance, perform check for idempotency and
    /// readmodel versioning.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UpsertAsync(TModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>
    /// Update a readmodel instance into underling storage, it perform
    /// it will check for idempotency, if the object was already saved
    /// and it is newer it will not save again. 
    /// </para>
    /// </summary>
    /// <param name="model"></param>
    /// <param name="cancellationToken"></param>
    /// <remarks>If the readmodel is not present on database, nothing will be written.</remarks>
    /// <returns></returns>
    Task UpdateAsync(TModel model, CancellationToken cancellationToken = default);

    /// <summary>
    /// <para>
    /// Used in the following scenario.
    /// I have a readmodel in version X = 6
    /// Changeset 7 and 8 are applied but the readmodel does not handle events in those changeset
    /// Usually projection service will not update the readmodel on Storage because it is not changed by the
    /// events in the changeset.
    /// Problem: Aggregate on disk is on AggregateVersion=6 and events 7 and 8 are not in the list of handled commits.
    /// This generates lots of problem because it made almost impossible to diagnose errors.
    /// </para>
    /// <para>
    /// Possible solution: Alwasy save the readmodel even if it declare that events does not change it.
    /// Best solution: Call this method that will update only the relevant properties of the changes that will indicates
    /// the real list of <see cref="Changeset"/> applied.
    /// </para>
    /// </summary>
    /// <param name="model"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UpdateVersionAsync(TModel model, CancellationToken cancellationToken = default);
}

public static class AtomicCollectionWrapperExtensions
{
    public static Task<TModel> FindOneByIdAsync<TModel>(
        this IAtomicCollectionWrapper<TModel> wrapper,
        string id,
        CancellationToken cancellationToken = default)
        where TModel : IAtomicReadModel
    {
        return wrapper.FindOneByIdAsync(id, cancellationToken);
    }

    public static Task<TModel> FindOneByIdAndCatchupAsync<TModel>(
        this IAtomicCollectionWrapper<TModel> wrapper,
        string id,
        CancellationToken cancellationToken = default)
        where TModel : IAtomicReadModel
    {
        return wrapper.FindOneByIdAndCatchupAsync(id, cancellationToken);
    }

    public static Task<IReadOnlyCollection<TModel>> FindManyAsync<TModel>(
        this IAtomicCollectionWrapper<TModel> wrapper,
        Expression<Func<TModel, bool>> filter,
        bool fixVersion = false,
        CancellationToken cancellationToken = default)
        where TModel : IAtomicReadModel
    {
        return wrapper.FindManyAsync(filter, fixVersion, cancellationToken);
    }

    public static Task<TModel> FindOneByIdAtCheckpointAsync<TModel>(
        this IAtomicCollectionWrapper<TModel> wrapper,
        string id,
        long chunkPosition,
        CancellationToken cancellationToken = default)
        where TModel : IAtomicReadModel
    {
        return wrapper.FindOneByIdAtCheckpointAsync(id, chunkPosition, cancellationToken);
    }

    public static Task UpsertForceAsync<TModel>(
        this IAtomicCollectionWrapper<TModel> wrapper,
        TModel model,
        CancellationToken cancellationToken = default)
        where TModel : IAtomicReadModel
    {
        return wrapper.UpsertForceAsync(model, cancellationToken);
    }

    public static Task UpsertAsync<TModel>(
        this IAtomicCollectionWrapper<TModel> wrapper,
        TModel model,
        CancellationToken cancellationToken = default)
        where TModel : IAtomicReadModel
    {
        return wrapper.UpsertAsync(model, cancellationToken);
    }

    public static Task UpdateAsync<TModel>(
        this IAtomicCollectionWrapper<TModel> wrapper,
        TModel model,
        CancellationToken cancellationToken = default)
        where TModel : IAtomicReadModel
    {
        return wrapper.UpdateAsync(model, cancellationToken);
    }

    public static Task UpdateVersionAsync<TModel>(
        this IAtomicCollectionWrapper<TModel> wrapper,
        TModel model,
        CancellationToken cancellationToken = default)
        where TModel : IAtomicReadModel
    {
        return wrapper.UpdateVersionAsync(model, cancellationToken);
    }
}