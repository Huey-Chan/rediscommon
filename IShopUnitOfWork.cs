using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Shop.Data.Interface
{
    /// <summary>
    /// Main interface of this library. It include every needed method to access to DB which method is implemented
    /// in proper UnitOfWork classes using Repositories classes(pattern). Actually, exist only one implementation of this
    /// interface - EFUnitOfWork which is implementation of UnitOfWork pattern based on Entity Framework.
    /// Every method which return any result - return Queryable objects to give possible expand query before it go to DB.
    /// </summary>
    public interface IShopUnitOfWork : IDisposable
    {

        TEntity Create<TEntity>() where TEntity : class;

        /// <summary>
        /// Property get if connection is open
        /// </summary>
        bool IsConnectionOpen { get; }

        /// <summary>
        /// Insert entity to database
        /// </summary>
        /// <typeparam name="TEntity">Type of repository on which insert method will be execute</typeparam>
        /// <param name="entity">Entity which will be inserted</param>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext%gt;();
        /// EntityTypeFromModel sampleEntity = new EntityTypeFromModel { Property1 = ... };
        /// UoW.Insert&lt;EntityTypeFromModel&gt;(sampleEntity);
        /// UoW.SaveChanges(true);
        /// </example>
        void Insert<TEntity>(TEntity entity) where TEntity : class;

        /// <summary>
        /// Update existing entity in databse
        /// </summary>
        /// <typeparam name="TEntity">Type of repository on which update method will be execute</typeparam>
        /// <param name="entity">Entity which will be updated</param>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext&gt;();
        /// EntityTypeFromModel sampleEntity = UoW.GetById&lt;EntityTypeFromModel&gt;(1);
        /// sampleEntity.Property1 = ...;
        /// UoW.Update&lt;EntityTypeFromModel&gt;(sampleEntity);
        /// UoW.SaveChanges(true);
        /// </example>
        void Update<TEntity>(TEntity entity) where TEntity : class;

        /// <summary>
        /// Get entity by its id or ids
        /// </summary>
        /// <typeparam name="TEntity">Type of repository from which entity will be returned</typeparam>
        /// <param name="ids">ID keys</param>
        /// <returns>Single entity of type of repository type</returns>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext&gt;();
        /// EntityTypeFromModel sampleEntity = UoW.GetById&lt;EntityTypeFromModel&gt;(1);
        /// UoW.Dispose();
        /// </example>
        TEntity GetById<TEntity>(params object[] ids) where TEntity : class;

        /// <summary>
        /// Get all entities of given type. Method get queryable object to allow further quering before
        /// finally query go to database
        /// </summary>
        /// <typeparam name="TEntity">Type of repository from which entities will be returned</typeparam>
        /// <returns>Queryable object of entities of repository type</returns>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext&gt;();
        /// var sampleEntities = UoW.Get&lt;EntityTypeFromModel&gt;().ToList();
        /// UoW.Dispose();
        /// </example>
        IQueryable<TEntity> Get<TEntity>() where TEntity : class;

        /// <summary>
        /// Get all entities of given type. Method get queryable object to allow further quering before
        /// finally query go to database
        /// </summary>
        /// <typeparam name="TEntity">Type of repository from which entities will be returned</typeparam>
        /// <param name="filter">Filter which will be use in where caluse inside method</param>
        /// <param name="orderBy">Order for query</param>
        /// <returns>Queryable object of entities of repository type</returns>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext&gt;();
        /// var sampleEntities = UoW.Get&lt;EntityTypeFromModel&gt;(q => q.ID == 1, q => q.OrderBy(q.Name)).ToList();
        /// UoW.Dispose();
        /// </example>
        IQueryable<TEntity> Get<TEntity>(Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null) where TEntity : class;

        /// <summary>
        /// Delete designated entity by its id or ids
        /// </summary>
        /// <typeparam name="TEntity">Type of repository from which entities will be deleted</typeparam>
        /// <param name="ids">id or ids of delted entity</param>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext&gt;();
        /// UoW.DeleteById%lt;EntityTypeFromModel&gt;(1);
        /// UoW.SaveChanges(true);
        /// </example>
        void DeleteById<TEntity>(params object[] ids) where TEntity : class;

        /// <summary>
        /// Delete given entity
        /// </summary>
        /// <typeparam name="TEntity">Type of repository from which entities will be deleted</typeparam>
        /// <param name="entity">Entity to delte</param>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext&gt;();
        /// UoW.Delete&lt;EntityTypeFromModel&gt;(UoW.GetById&lt;EntityTypeFromModel(1)&gt;);
        /// UoW.SaveChanges(true);
        /// </example>
        void Delete<TEntity>(TEntity entity) where TEntity : class;

        void DeleteRange<TEntity>(IEnumerable<TEntity> entities, int batchSize = 100, bool autoCommitEnabled = false) where TEntity : class;

        /// <summary>
        /// Inner join on two sequences of entities
        /// </summary>
        /// <typeparam name="TEntityOuter">Type of repository from which outer set is getting</typeparam>
        /// <typeparam name="TEntityInner">Type of repository from which inner set is getting</typeparam>
        /// <typeparam name="TResult">Type of result after join above sets</typeparam>
        /// <param name="outerKeySelector">Property from outer entity which must be equal next property to join two elements</param>
        /// <param name="innerKeySelector">Property from inner entity which must be equal with previous to join two elements</param>
        /// <param name="resultSelector">Choosen properties which will be returned from this two entities</param>
        /// <returns>Queryable object of entities of result type</returns>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext&gt;();
        /// var joinedEntities = UoW.Join&lt;Entity1, Entity2, CustomType&gt;(p => p.ID, q => q.Entity1ID, (p, q) => new CustomType { p.ID, q.Name }).ToList();
        /// UoW.SaveChanges(true);
        /// </example>
        IQueryable<TResult> Join<TEntityOuter, TEntityInner, TResult>(Func<TEntityOuter, object> outerKeySelector,
            Func<TEntityInner, object> innerKeySelector,
            Func<TEntityOuter, TEntityInner, TResult> resultSelector)
            where TEntityInner : class
            where TEntityOuter : class;

        /// <summary>
        /// Inner join on two sequences of entities
        /// </summary>
        /// <typeparam name="TEntityOuter">Type of repository from which outer set is getting</typeparam>
        /// <typeparam name="TEntityInner">Type of repository from which inner set is getting</typeparam>
        /// <typeparam name="TResult">Type of result after join above sets</typeparam>
        /// <param name="outerKeySelector">Property from outer entity which must be equal next property to join two elements</param>
        /// <param name="innerKeySelector">Property from inner entity which must be equal with previous to join two elements</param>
        /// <param name="resultSelector">Choosen properties which will be returned from this two entities</param>
        /// <param name="comparer">Compare class to compare keys of two enities</param>
        /// <returns>Queryable object of entities of result type</returns>        
        IQueryable<TResult> Join<TEntityOuter, TEntityInner, TResult>(Func<TEntityOuter, object> outerKeySelector,
            Func<TEntityInner, object> innerKeySelector,
            Func<TEntityOuter, TEntityInner, TResult> resultSelector,
            IEqualityComparer<object> comparer)
            where TEntityInner : class
            where TEntityOuter : class;

        /// <summary>
        /// Left join on two sequences of entities
        /// </summary>
        /// <typeparam name="TEntityOuter">Type of repository from which outer set is getting</typeparam>
        /// <typeparam name="TEntityInner">Type of repository from which inner set is getting</typeparam>
        /// <typeparam name="TResult">Type of result after join above sets</typeparam>
        /// <param name="outerKeySelector">Property from outer entity which must be equal next property to join two elements</param>
        /// <param name="innerKeySelector">Property from inner entity which must be equal with previous to join two elements</param>
        /// <param name="resultSelector">Choosen properties which will be returned from this two entities</param>
        /// <returns>Queryable object of entities of result type</returns>
        /// <remarks>!Important is that only one inner entity should match to outer or nothing(of course, this is obvious
        /// for LEFT join). When more inner entities match to outer entity than only first will be conncted.
        /// If you want to GroupJoin you should use GroupJoin from LINQ after Get() method on outer entity.</remarks>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext&gt;();
        /// var joinedEntities = UoW.Join&lt;Entity1, Entity2, CustomType&gt;(p => p.ID, q => q.Entity1ID, (p, q) => new CustomType { p.ID, q != null ? q.Name : string.Empty }).ToList();
        /// UoW.SaveChanges(true);
        /// </example>
        IQueryable<TResult> LeftJoin<TEntityOuter, TEntityInner, TResult>(Func<TEntityOuter, object> outerKeySelector,
            Func<TEntityInner, object> innerKeySelector,
            Func<TEntityOuter, TEntityInner, TResult> resultSelector)
            where TEntityInner : class
            where TEntityOuter : class;

        /// <summary>
        /// Inner join on two sequences of entities
        /// </summary>
        /// <typeparam name="TEntityOuter">Type of repository from which outer set is getting</typeparam>
        /// <typeparam name="TEntityInner">Type of repository from which inner set is getting</typeparam>
        /// <typeparam name="TResult">Type of result after join above sets</typeparam>
        /// <param name="outerKeySelector">Property from outer entity which must be equal next property to join two elements</param>
        /// <param name="innerKeySelector">Property from inner entity which must be equal with previous to join two elements</param>
        /// <param name="resultSelector">Choosen properties which will be returned from this two entities</param>
        /// <param name="comparer">Compare class to compare keys of two enities</param>        
        /// <returns>Queryable object of entities of result type</returns>        
        /// <remarks>!Important is that only one inner entity should match to outer or nothing(of course, this is obvious
        /// for LEFT join). When more inner entities match to outer entity than only first will be conncted.
        /// If you want to GroupJoin you should use GroupJoin from LINQ after Get() method on outer entity.</remarks>
        IQueryable<TResult> LeftJoin<TEntityOuter, TEntityInner, TResult>(Func<TEntityOuter, object> outerKeySelector,
            Func<TEntityInner, object> innerKeySelector,
            Func<TEntityOuter, TEntityInner, TResult> resultSelector,
            IEqualityComparer<object> comparer)
            where TEntityInner : class
            where TEntityOuter : class;

        /// <summary>
        /// Query method send all queries included in parameter to DB and concern of open connection immedaitely before first
        /// query and close immediately after last query. Obviouslu, method also save changes in DB.
        /// </summary>
        /// <param name="query">Predefined delegate with queries</param>
        /// <example>
        /// Example:
        /// IUnitOfWork UoW = EFUnitOfWork&lt;YourDbContext&gt;();
        /// UoW.Query(() => 
        ///     var entity = UoW.GetById&lt;EntityFromModel&gt;(1);
        ///     entity.Property1 = ...;
        ///     UoW.Update&lt;EntityFromModel&gt;(entity);
        /// );        
        /// </example>
        /// <remarks>This method is strongly recommended to invoke every query with this library.</remarks>
        void Query(Action query);

        /// <summary>
        /// Save all changes in cotnext to DB
        /// </summary>
        /// <param name="withDisposing">If true then connection is close after save. Default - false</param>
        /// Important is to know that whole save operation is involved by transaction.
        void SaveChanges(bool withDisposing = false, bool isAsync = false);

        DbContext Context { get; }

        void InsertRange<TEntity>(IEnumerable<TEntity> entities, int batchSize = 100, bool autoCommitEnabled = false) where TEntity : class;

        IQueryable<TEntity> Expand<TEntity>(IQueryable<TEntity> query, string path) where TEntity : class;

        IDictionary<string, object> GetModifiedProperties<TEntity>(TEntity entity) where TEntity : class;


        /// <summary>
        /// Creates a raw SQL query that will return elements of the given generic type.  
        /// The type can be any type that has properties that match the names of the columns returned from the query, 
        /// or can be a simple primitive type. The type does not have to be an entity type. 
        /// The results of this query are never tracked by the context even if the type of object returned is an entity type.
        /// </summary>
        /// <typeparam name="TElement">The type of object returned by the query.</typeparam>
        /// <param name="sql">The SQL query string.</param>
        /// <param name="parameters">The parameters to apply to the SQL query string.</param>
        /// <returns>Result</returns>
        IEnumerable<TElement> SqlQuery<TElement>(string sql, params object[] parameters);

        /// <summary>
        /// Executes the given DDL/DML command against the database.
        /// </summary>
        /// <param name="sql">The command string</param>
        /// <param name="timeout">Timeout value, in seconds. A null value indicates that the default value of the underlying provider will be used</param>
        /// <param name="parameters">The parameters to apply to the command string.</param>
        /// <returns>The result returned by the database after executing the command.</returns>
        int ExecuteSqlCommand(string sql, int? timeout = null, params object[] parameters);

        DataTable ExeSqlReturnDT(string sql, SqlParameter[] parameters);

    }
}
