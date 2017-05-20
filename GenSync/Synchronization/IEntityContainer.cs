using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GenSync.Logging;

namespace GenSync.Synchronization
{
  public interface IEntityContainer<TEntityId, TEntity, TContext> : IDisposable
  {
    /// <summary>
    /// Returns the entities as a stream of "transient" entities. 
    /// It is not allowed to hold a reference to an entity after the enumerable advanced to the next position.
    /// </summary>
    Task<IEnumerable<EntityWithId<TEntityId, TEntity>>> GetTransientEntities(ICollection<TEntityId> ids, ILoadEntityLogger logger, TContext context);

    /// <summary>
    /// It is not allowed to hold a reference to an entity after another call to this interface
    /// Entities must be cleaned with IReadOnlyEntityRepository.Cleanup
    /// </summary>
    Task<IReadOnlyDictionary<TEntityId, TEntity>> GetEntities(ICollection<TEntityId> ids, ILoadEntityLogger logger, TContext context);
  }
}