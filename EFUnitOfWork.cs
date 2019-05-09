using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shop.Data.Interface;
using System.Data.Entity.Infrastructure;
using System.Data;
using System.Data.SqlClient;

namespace Shop.Data.Implementing
{
    /// <summary>
    /// Implementation of IUnitOfWork interface. Methods is not described here because all of it is described
    /// in IUnitOfWork
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    public class EFUnitOfWork<TDbContext> : IShopUnitOfWork where TDbContext : DbContext
    {
        private IDictionary<Type, object> repositories = new Dictionary<Type, object>();
        private DbContext efContext;
        private bool disposed = false;

        public bool IsConnectionOpen { get { return efContext != null; } }


        public TEntity Create<TEntity>() where TEntity : class
        {
            InitializeEFContext();
            return GetRepo<TEntity>().Create();
        }

        public void Insert<TEntity>(TEntity entity) where TEntity : class
        {
            InitializeEFContext();

            GetRepo<TEntity>().Insert(entity);
        }

        public void InsertRange<TEntity>(IEnumerable<TEntity> entities, int batchSize = 100, bool autoCommitEnabled = false) where TEntity : class
        {
            InitializeEFContext();
            GetRepo<TEntity>().InsertRange(entities, batchSize, autoCommitEnabled);
        }

        public void DeleteRange<TEntity>(IEnumerable<TEntity> entities, int batchSize = 100, bool autoCommitEnabled = false) where TEntity : class
        {
            InitializeEFContext();
            GetRepo<TEntity>().DeleteRange(entities, batchSize, autoCommitEnabled);
        }

        public IQueryable<TEntity> Expand<TEntity>(IQueryable<TEntity> query, string path) where TEntity : class
        {
            InitializeEFContext();
            return query.Include(path);
        }

        public IDictionary<string, object> GetModifiedProperties<TEntity>(TEntity entity) where TEntity : class
        {
            InitializeEFContext();
            var props = new Dictionary<string, object>();
            var entry = efContext.Entry(entity);
            var modifiedPropertyNames = from p in entry.CurrentValues.PropertyNames
                                        where entry.Property(p).IsModified
                                        select p;
            foreach (var name in modifiedPropertyNames)
            {
                props.Add(name, entry.Property(name).OriginalValue);
            }
            return props;
        }


        public void Update<TEntity>(TEntity entity) where TEntity : class
        {
            InitializeEFContext();

            GetRepo<TEntity>().Update(entity);
        }

        public TEntity GetById<TEntity>(params object[] ids) where TEntity : class
        {
            InitializeEFContext();

            return GetRepo<TEntity>().GetById(ids);
        }

        public IQueryable<TEntity> Get<TEntity>() where TEntity : class
        {
            InitializeEFContext();

            return GetRepo<TEntity>().Get();
        }

        public IQueryable<TEntity> Get<TEntity>(System.Linq.Expressions.Expression<Func<TEntity, bool>> filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null) where TEntity : class
        {
            InitializeEFContext();

            return GetRepo<TEntity>().Get(filter, orderBy);
        }

        public IQueryable<TResult> Join<TEntityOuter, TEntityInner, TResult>(Func<TEntityOuter, object> outerKeySelector, Func<TEntityInner, object> innerKeySelector, Func<TEntityOuter, TEntityInner, TResult> resultSelector)
            where TEntityOuter : class
            where TEntityInner : class
        {
            InitializeEFContext();

            return GetRepo<TEntityOuter>().Get().Join(GetRepo<TEntityInner>().Get(), outerKeySelector, innerKeySelector, resultSelector).AsQueryable();
        }

        public IQueryable<TResult> Join<TEntityOuter, TEntityInner, TResult>(Func<TEntityOuter, object> outerKeySelector, Func<TEntityInner, object> innerKeySelector, Func<TEntityOuter, TEntityInner, TResult> resultSelector, IEqualityComparer<object> comparer)
            where TEntityOuter : class
            where TEntityInner : class
        {
            InitializeEFContext();

            return GetRepo<TEntityOuter>().Get().Join(GetRepo<TEntityInner>().Get(), outerKeySelector, innerKeySelector, resultSelector, comparer).AsQueryable();
        }

        public IQueryable<TResult> LeftJoin<TEntityOuter, TEntityInner, TResult>(Func<TEntityOuter, object> outerKeySelector, Func<TEntityInner, object> innerKeySelector, Func<TEntityOuter, TEntityInner, TResult> resultSelector)
            where TEntityOuter : class
            where TEntityInner : class
        {
            InitializeEFContext();

            return GetRepo<TEntityOuter>().Get().GroupJoin(GetRepo<TEntityInner>().Get(), outerKeySelector, innerKeySelector, (p, q) => resultSelector(p, q.FirstOrDefault())).AsQueryable();
        }

        public IQueryable<TResult> LeftJoin<TEntityOuter, TEntityInner, TResult>(Func<TEntityOuter, object> outerKeySelector, Func<TEntityInner, object> innerKeySelector, Func<TEntityOuter, TEntityInner, TResult> resultSelector, IEqualityComparer<object> comparer)
            where TEntityOuter : class
            where TEntityInner : class
        {
            InitializeEFContext();

            return GetRepo<TEntityOuter>().Get().GroupJoin(GetRepo<TEntityInner>().Get(), outerKeySelector, innerKeySelector, (p, q) => resultSelector(p, q.FirstOrDefault()), comparer).AsQueryable();
        }

        public void DeleteById<TEntity>(params object[] ids) where TEntity : class
        {
            InitializeEFContext();

            GetRepo<TEntity>().DeleteById(ids);
        }

        public void Delete<TEntity>(TEntity entity) where TEntity : class
        {
            InitializeEFContext();

            GetRepo<TEntity>().Delete(entity);
        }

        public void Query(Action query)
        {
            InitializeEFContext();

            query.Invoke();

            SaveChanges(true);
        }

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
        public IEnumerable<TElement> SqlQuery<TElement>(string sql, params object[] parameters)
        {
            return this.Context.Database.SqlQuery<TElement>(sql, parameters);
        }

        /// <summary>
        /// Executes the given DDL/DML command against the database.
        /// </summary>
        /// <param name="sql">The command string</param>
        /// <param name="timeout">Timeout value, in seconds. A null value indicates that the default value of the underlying provider will be used</param>
        /// <param name="parameters">The parameters to apply to the command string.</param>
        /// <returns>The result returned by the database after executing the command.</returns>
        public int ExecuteSqlCommand(string sql, int? timeout = null, params object[] parameters)
        {
            InitializeEFContext();

            int? previousTimeout = null;
            if (timeout.HasValue)
            {
                //store previous timeout
                previousTimeout = ((IObjectContextAdapter)this.efContext).ObjectContext.CommandTimeout;
                ((IObjectContextAdapter)this.efContext).ObjectContext.CommandTimeout = timeout;
            }

            // remove the GO statements
            //sql = Regex.Replace(sql, @"\r{0,1}\n[Gg][Oo]\r{0,1}\n", "\n");

            var result = this.efContext.Database.ExecuteSqlCommand(sql, parameters);

            if (timeout.HasValue)
            {
                //Set previous timeout back
                ((IObjectContextAdapter)this.efContext).ObjectContext.CommandTimeout = previousTimeout;
            }

            return result;
        }

        public DbContext Context
        {
            get { InitializeEFContext(); return efContext; }
        }

        public void SaveChanges(bool withDisposing = false, bool isAsync = false)
        {
            if (isAsync)
            {
                efContext.SaveChangesAsync();
            }
            else
            {
                efContext.SaveChanges();

            }

            if (withDisposing)
            {
                Dispose();
            }
        }
        public DataTable ExeSqlReturnDT(string sql, SqlParameter[] parameters)
        {
            var connection = (SqlConnection)Context.Database.Connection;
            SqlCommand cmd = new SqlCommand();
            cmd.Connection = connection;
            cmd.CommandText = sql;
            if (parameters != null && parameters.Length > 0)
            {
                foreach (var item in parameters)
                {
                    cmd.Parameters.Add(item);
                }
            }
            SqlDataAdapter adapter = new SqlDataAdapter(cmd);
            DataTable table = new DataTable();
            adapter.Fill(table);
            return table;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (efContext != null)
                    {
                        efContext.Dispose();
                        efContext = null;
                    }

                    repositories.Clear();
                }
            }
            this.disposed = true;
        }

        private void InitializeEFContext()
        {
            if (efContext == null)
            {
                efContext = typeof(TDbContext).GetConstructor(new Type[] { }).Invoke(new object[] { }) as DbContext;
                efContext.Configuration.ValidateOnSaveEnabled = false;
            }
        }

        private IRepository<TEntity> GetRepo<TEntity>() where TEntity : class
        {
            if (!repositories.ContainsKey(typeof(TEntity)))
            {
                repositories.Add(new KeyValuePair<Type, object>(typeof(TEntity), new EFRepository<TEntity>(efContext)));
            }

            return (IRepository<TEntity>)repositories[typeof(TEntity)];
        }


    }
}
