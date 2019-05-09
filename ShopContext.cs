using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Shop.Data.Implementing
{
    public class ShopContext : DbContext
    {
        static ShopContext()
        {
            Database.SetInitializer<ShopContext>(null);
        }
       
        public ShopContext()
            : base("Name=shopcontext")
        {
           // this.Configuration.ValidateOnSaveEnabled = false;
        }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var typesToRegister = from t in Assembly.GetExecutingAssembly().GetTypes()
                                  where !string.IsNullOrEmpty(t.Namespace) &&
                                        t.BaseType != null &&
                                        t.BaseType.IsGenericType
                                  let genericType = t.BaseType.GetGenericTypeDefinition()
                                  where genericType == typeof(EntityTypeConfiguration<>) || genericType == typeof(ComplexTypeConfiguration<>)
                                  select t;
         
            foreach (var type in typesToRegister)
            {
                dynamic configurationInstance = Activator.CreateInstance(type);
                modelBuilder.Configurations.Add(configurationInstance);
            }
            //...or do it manually below. For example,
            //modelBuilder.Configurations.Add(new LanguageMap());


            base.OnModelCreating(modelBuilder);
        }
    }

}
