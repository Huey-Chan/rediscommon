using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shop.Data.Interface;
using System.Data.Entity.Validation;

namespace Shop.Data.Implementing
{
    internal class EFRepository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        private DbSet<TEntity> EFSet { get; set; }
        private DbContext EFContext { get; set; }

        public EFRepository(DbContext efContext)
        {
            EFSet = efContext.Set<TEntity>();
            EFContext = efContext;
        }

        public TEntity Create()
        {
            return this.EFSet.Create();
        }
        public void Insert(TEntity entity)
        {
            EFSet.Add(entity);
        }

        public void InsertRange(IEnumerable<TEntity> entities, int batchSize = 100, bool autoCommitEnabled = false)
        {
            try
            {
                if (entities == null)
                    throw new ArgumentNullException("entities");

                if (entities.HasItems())
                {
                    if (batchSize <= 0)
                    {
                        // insert all in one step
                        entities.Each(x => EFSet.Add(x));
                    }
                    else
                    {
                        int i = 1;
                        bool saved = false;
                        foreach (var entity in entities)
                        {
                            EFSet.Add(entity);
                            saved = false;
                            if (i % batchSize == 0)
                            {
                                if (autoCommitEnabled)
                                    EFContext.SaveChanges();
                                i = 0;
                                saved = true;
                            }
                            i++;
                        }

                        if (!saved)
                        {
                            if (autoCommitEnabled)
                                EFContext.SaveChanges();
                        }
                    }
                }
            }
            catch (DbEntityValidationException ex)
            {
                throw ex;
            }
        }


        public void DeleteRange(IEnumerable<TEntity> entities, int batchSize = 100, bool autoCommitEnabled = false)
        {
            try
            {
                if (entities == null)
                    throw new ArgumentNullException("entities");

                if (entities.HasItems())
                {
                    if (batchSize <= 0)
                    {
                        // insert all in one step
                        entities.Each(x => EFSet.Add(x));
                    }
                    else
                    {
                        int i = 1;
                        bool saved = false;
                        foreach (var entity in entities)
                        {
                            EFSet.Remove(entity);
                            saved = false;
                            if (i % batchSize == 0)
                            {
                                i = 0;
                                saved = true;
                            }
                            i++;
                        }

                    }
                }
            }
            catch (DbEntityValidationException ex)
            {
                throw ex;
            }
        }


        public void Update(TEntity entity)
        {
            if (EFContext.Entry<TEntity>(entity).State == EntityState.Detached)
            {
                EFSet.Attach(entity);
            }
            else
            {
                EFContext.Entry<TEntity>(entity).CurrentValues.SetValues(entity);
            }

            EFContext.Entry<TEntity>(entity).State = EntityState.Modified;
        }

        public TEntity GetById(params object[] ids)
        {
            return EFSet.Find(ids);
        }

        public IQueryable<TEntity> Get()
        {
            return EFSet;
        }

        public IQueryable<TEntity> Expand(IQueryable<TEntity> query, string path)
        {
            return query.Include(path);
        }

        public IDictionary<string, object> GetModifiedProperties(TEntity entity)
        {
            var props = new Dictionary<string, object>();

            var ctx = EFContext;
            var entry = ctx.Entry(entity);
            var modifiedPropertyNames = from p in entry.CurrentValues.PropertyNames
                                        where entry.Property(p).IsModified
                                        select p;
            foreach (var name in modifiedPropertyNames)
            {
                props.Add(name, entry.Property(name).OriginalValue);
            }
            return props;
        }

        public IQueryable<TEntity> Get(System.Linq.Expressions.Expression<Func<TEntity, bool>> filter = null,
                                        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null)
        {
            IQueryable<TEntity> query = EFSet;

            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (orderBy != null)
            {
                return orderBy(query);
            }
            else
            {
                return query;
            }
        }

        public void DeleteById(params object[] ids)
        {
            Delete(GetById(ids));
        }

        public void Delete(TEntity entity)
        {
            if (EFContext.Entry<TEntity>(entity).State == EntityState.Detached)
            {
                EFSet.Attach(entity);
            }

            EFSet.Remove(entity);
        }
    }
}
