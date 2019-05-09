using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Shop.Data.Interface
{
    interface IRepository<TEntity> where TEntity : class
    {
        TEntity Create();
        void Insert(TEntity entity);
        void Update(TEntity entity);
        TEntity GetById(params object[] ids);
        IQueryable<TEntity> Get();
        IQueryable<TEntity> Get(Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null);
        void DeleteById(params object[] ids);
        void Delete(TEntity entity);

        void DeleteRange(IEnumerable<TEntity> entities, int batchSize = 100, bool autoCommitEnabled = false);

        void InsertRange(IEnumerable<TEntity> entities, int batchSize = 100, bool autoCommitEnabled = false);

        IQueryable<TEntity> Expand(IQueryable<TEntity> query, string path);

        IDictionary<string, object> GetModifiedProperties(TEntity entity);


    }
}
