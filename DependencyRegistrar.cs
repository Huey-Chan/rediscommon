using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Integration.Mvc;
using Shop.Core;
using Shop.Core.Caching;
using Shop.Core.Fakes;
using Shop.Core.Infrastructure;
using Shop.Core.Infrastructure.DependencyManagement;
using Shop.Core.Plugins;
using Shop.Data;
using Shop.Data.Implementing;
using Shop.Data.Interface;
using Shop.Services.AdvertManage;
using Shop.Services.AppUpdate;
using Shop.Services.Authentication;
using Shop.Services.Column;
using Shop.Services.Configuration;
using Shop.Services.Coupon;
using Shop.Services.Customers;
using Shop.Services.Events;
using Shop.Services.Extension;
using Shop.Services.FriendLinks;
using Shop.Services.Localization;
using Shop.Services.Logging;
using Shop.Services.Lucene;
using Shop.Services.Media;
using Shop.Services.Messages;
using Shop.Services.Orders;
using Shop.Services.Products;
using Shop.Services.Promotion;
using Shop.Services.Security;
using Shop.Services.Seo;
using Shop.Services.Tasks;
//using Shop.Services.Promotion;
using Shop.Services.Topics;
using Shop.Services.Websites;
using Shop.Web.Framework.EmbeddedViews;
using Shop.Web.Framework.Mvc.Bundles;
using Shop.Web.Framework.Mvc.Routes;
using Shop.Web.Framework.UI;
using Shop.Web.Framework.UI.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using Shop.PayState;
using Shop.Services;
using Wine.Data.Implementing;
using Wine.Data.Interface;
//using YangTuo.Data.Implementing;
//using YangTuo.Data.Interface;
//using YangTuo.Services;
using Wine.Services.Culture;
using Wine.Services.Customer;
using Wine.Services.Favourite;
using Wine.Services.Raffle;
using Wine.Services.SystemMessage;
using Wine.Services.Wine;
using Shop.Services.MallOperations;
using Shop.Services.ExportImport;
using Shop.Services.CrossBorderPurchase;
using Shop.Services.SuitProducts;
using Shop.Services.Presell;
using Shop.Services.CrowdFundings;
using Shop.Services.PDetailTempl;
using Shop.Services.LVB;
using Shop.Data.Domain.Customers;
using Wine.Services.LVB;
using Shop.Services.Cooperation;
using Shop.Services.ParticipleDictionarys;

//using Shop.Services.SpecialDiscountManage;

namespace Shop.Web.Framework
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder)
        {
            var foundAssemblies = typeFinder.GetAssemblies().ToArray(); // codehint: sm-add

            // codehint: sm-edit
            builder.RegisterModule(new AutofacWebTypesModule());
            builder.Register(c =>
                //register FakeHttpContext when HttpContext is not available
                HttpContext.Current != null ?
                (new HttpContextWrapper(HttpContext.Current) as HttpContextBase) :
                (new FakeHttpContext("~/") as HttpContextBase))
                .As<HttpContextBase>()
                .InstancePerRequest();

            //web helper
            builder.RegisterType<WebHelper>().As<IWebHelper>().InstancePerRequest();

            //controllers
            builder.RegisterControllers(foundAssemblies);

            //// codehint: sm-add
            //// http controllers (web api)
            //builder.RegisterWebApiFilterProvider(GlobalConfiguration.Configuration);
            //builder.RegisterWebApiModelBinderProvider();
            //builder.RegisterWebApiModelBinders(foundAssemblies);
            //builder.RegisterApiControllers(foundAssemblies).PropertiesAutowired();

            //data layer

            builder.Register<IShopUnitOfWork>(c => new Shop.Data.Implementing.EFUnitOfWork<ShopContext>())
                .InstancePerRequest()
               .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            builder.Register<IWineUnitOfWork>(c => new Wine.Data.Implementing.EFUnitOfWork<WineContext>())
                .InstancePerRequest()
                   .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            //builder.Register<IYangTuoUnitOfWork>(c => new YangTuo.Data.Implementing.EFUnitOfWork<YangTuoContext>())
            //    .InstancePerRequest()
            //       .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
            //builder.Register<IDbContext>(c => new ShopObjectContext(WineWorldDb.WineShopDb))
            //        .InstancePerRequest()
            //    //.OnActivated(e => ((ObjectContextBase)e.Instance).Hooks = e.Context.Resolve<IEnumerable<IHook>>());
            //        .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            //builder.Register<IWineDbContext>(c => new WineObjectContext(WineWorldDb.WineDb))
            //      .InstancePerRequest()
            //    //.OnActivated(e => ((ObjectContextBase)e.Instance).Hooks = e.Context.Resolve<IEnumerable<IHook>>());
            //      .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);

            //builder.RegisterGeneric(typeof(EfRepository<>)).As(typeof(IRepository<>)).InstancePerRequest();

            //builder.RegisterGeneric(typeof(WineEfRepository<>)).As(typeof(IWineRepository<>)).InstancePerRequest();

            //// register DB Hooks (codehint: sm-add)
            // builder.RegisterType<LocalizedEntityPostDeleteHook>().As<IHook>();

            //plugins
            builder.RegisterType<PluginFinder>().As<IPluginFinder>().SingleInstance(); // xxx (http)

            //cache manager
            builder.RegisterType<StaticCache>().As<ICache>().Named<ICache>("static").SingleInstance();
            builder.RegisterType<RequestCache>().As<ICache>().Named<ICache>("request").InstancePerRequest();
            //builder.RegisterType<CouchBaseCache>().As<ICouchBaseCache>().InstancePerRequest();
            builder.RegisterType<RedisCacheOperator>().As<IRedisCacheOperator>().InstancePerRequest();

            builder.RegisterType<DefaultCacheManager>()
                .As<ICacheManager>()
                .Named<ICacheManager>("sm_cache_static")
                .WithParameter(ResolvedParameter.ForNamed<ICache>("static"))
                .InstancePerRequest();
            builder.RegisterType<DefaultCacheManager>()
                .As<ICacheManager>()
                .Named<ICacheManager>("sm_cache_per_request")
                .WithParameter(ResolvedParameter.ForNamed<ICache>("request"))
                .InstancePerRequest();

            //work context
            builder.RegisterType<WebWorkContext>().As<IWorkContext>()
                .InstancePerRequest();
            //store context
            builder.RegisterType<WebSiteContext>().As<IWebSiteContext>().InstancePerRequest();

            //services

            //pass MemoryCacheManager as cacheManager (cache settings between requests)
            //builder.RegisterType<ProductTagService>().As<IProductTagService>()
            //    .WithParameter(ResolvedParameter.ForNamed<ICacheManager>("sm_cache_static"))
            //    .InstancePerRequest();

            //pass MemoryCacheManager as cacheManager (cache settings between requests)
            //builder.RegisterType<PermissionService>().As<IPermissionService>()
            //    .WithParameter(ResolvedParameter.ForNamed<ICacheManager>("sm_cache_static"))
            //    .InstancePerRequest();

            //pass MemoryCacheManager as cacheManager (cache settings between requests)
            //builder.RegisterType<AclService>().As<IAclService>()
            //    .WithParameter(ResolvedParameter.ForNamed<ICacheManager>("sm_cache_static"))
            //    .InstancePerRequest();

            //codehint: sm-add

            #region customers

            builder.RegisterType<AccountService>().As<IAccountService>().InstancePerRequest();
            builder.RegisterType<FormsAuthenticationService>().As<IAuthenticationService>().InstancePerRequest();
            builder.RegisterType<AddressService>().As<IAddressService>().InstancePerRequest();

            builder.RegisterType<HonorCategoryService>().As<IHonorCategoryService>().InstancePerRequest();
            builder.RegisterType<AccountVerificationCodeService>().As<IAccountVerificationCodeService>().InstancePerRequest();

            builder.RegisterType<CustomerService>().As<ICustomerService>().InstancePerRequest();
            builder.RegisterType<AccountLoginService>().As<IAccountLoginService>().InstancePerRequest();
            builder.RegisterType<AccountRegLogService>().As<IAccountRegLogService>().InstancePerRequest();

            builder.RegisterType<AccountSecretAnswerService>().As<IAccountSecretAnswerService>().InstancePerRequest();
            builder.RegisterType<SecurityQuestionService>().As<ISecurityQuestionService>().InstancePerRequest();
            builder.RegisterType<WXCounponCardCodeMappingService>().As<IWXCounponCardCodeMappingService>().InstancePerRequest();
            builder.RegisterType<WXOpenIdAccountMappingService>().As<IWXOpenIdAccountMappingService>().InstancePerRequest();
            builder.RegisterType<FeedBackService>().As<IFeedBackService>().InstancePerRequest();
            builder.RegisterType<InternetUserService>().As<IInternetUserService>().InstancePerRequest();
            builder.RegisterType<UserCollectValueService>().As<IUserCollectValueService>().InstancePerRequest();
            builder.RegisterType<OrderJiHuaGiftService>().As<IOrderJiHuaGiftService>().InstancePerRequest();
            builder.RegisterType<VisitService>().As<IVisitService>().InstancePerRequest();

            builder.RegisterType<SalesmanService>().As<ISalesmanService>().InstancePerRequest();
            #endregion customers

            #region ParameterDic

            builder.RegisterType<ParameterDicService>().As<IParameterDicService>().InstancePerRequest();

            #endregion

            #region products
            builder.RegisterType<AppUpdateService>().As<IAppUpdateService>().InstancePerRequest();
            builder.RegisterType<ProductService>().As<IProductService>().InstancePerRequest();
            builder.RegisterType<ProductTagService>().As<IProductTagService>().InstancePerRequest();
            builder.RegisterType<ProductReviewService>().As<IProductReviewService>().InstancePerRequest();
            builder.RegisterType<RecommendService>().As<IRecommendService>().InstancePerRequest();

            #endregion products

            #region FriendLink 友情链接

            builder.RegisterType<FriendLinkService>().As<IFriendLinkService>().InstancePerRequest();

            #endregion FriendLink 友情链接

            #region Coupon 代金券

            builder.RegisterType<CouponService>().As<ICouponService>().InstancePerRequest();
            builder.RegisterType<WeiXinCouponService>().As<IWeiXinCardCoupon>().InstancePerRequest();

            #endregion

            #region Promotions

            builder.RegisterType<PromotionsService>().As<IPromotionsService>().InstancePerRequest();

            #endregion Promotions

            //builder.RegisterType<SpecialDiscountService>().As<ISpecialDiscountService>().InstancePerRequest();

            #region Log

            builder.RegisterType<AccountActivityService>().As<IAccountActivityService>().InstancePerRequest();

            #endregion Log

            #region Configuration

            builder.RegisterType<CategoryService>().As<ICategoryService>().InstancePerRequest();
            builder.RegisterType<AttributeService>().As<IAttributeService>().InstancePerRequest();
            builder.RegisterType<AttributeOptionService>().As<IAttributeOptionService>().InstancePerRequest();
            builder.RegisterType<DistrictService>().As<IDistrictService>().InstancePerRequest();

            #endregion Configuration

            #region Order

            builder.RegisterType<ShoppingCartService>().As<IShoppingCartService>().InstancePerRequest();
            builder.RegisterType<OrderService>().As<IOrderService>().InstancePerRequest();
            builder.RegisterType<OrderProductService>().As<IOrderProductService>().InstancePerRequest();
            builder.RegisterType<PaymentTypeService>().As<IPaymentTypeService>().InstancePerRequest();

            #endregion Order

            #region Topic

            builder.RegisterType<TopicService>().As<ITopicService>().InstancePerRequest();
            builder.RegisterType<RaffleService>().As<IRaffleService>().InstancePerRequest();

            #endregion Topic

            #region Configuration

            builder.RegisterType<MobileMessageTemplService>().As<IMobileMessageTemplService>().InstancePerRequest();
            builder.RegisterType<QueueMobileService>().As<IQueueMobileService>().InstancePerRequest();
            builder.RegisterType<QueuedAppService>().As<IQueuedAppMessageService>().InstancePerRequest();

            builder.RegisterType<MassMassMobileMessageTemplService>().As<IMassMobileMessageTemplService>().InstancePerRequest();
            builder.RegisterType<MassMobileMessageService>().As<IMassMobileMessageService>().InstancePerRequest();

            #endregion Configuration

            #region Fav

            builder.RegisterType<ArticleFavouriteService>().As<IArticleFavouriteService>().InstancePerRequest();
            builder.RegisterType<WineFavouriteService>().As<IWineFavouriteService>().InstancePerRequest();
            //builder.RegisterType<ArticleService>().As<IArticleService>().InstancePerRequest();

            #endregion Fav

            #region advert

            builder.RegisterType<AdvertPlaceService>().As<IAdvertPlaceService>().InstancePerRequest();
            builder.RegisterType<AdvertService>().As<IAdvertService>().InstancePerRequest();

            #endregion advert

            #region SystemMessage

            builder.RegisterType<AccountMessageService>().As<IAccountMessageService>().InstancePerRequest();
            builder.RegisterType<ProclamationService>().As<IProclamationService>().InstancePerRequest();

            #endregion SystemMessage

            #region Wine

            builder.RegisterType<WineCommentService>().As<IWineCommentService>().InstancePerRequest();
            builder.RegisterType<WineArticleService>().As<IWineArticleService>().InstancePerRequest();

            builder.RegisterType<WineService>().As<IWineService>().InstancePerRequest();
            #endregion Wine

            #region LVB
            builder.RegisterType<LiveService>().As<ILiveService>().InstancePerRequest();
            builder.RegisterType<IMHistoryMsgService>().As<IIMHistoryMsgService>().InstancePerRequest();
            #endregion

            builder.RegisterType<BuyProductCDKeyService>().As<IBuyProductCDKeyService>().InstancePerRequest();
            builder.RegisterType<ColumnService>().As<IColumnService>().InstancePerRequest();
            builder.RegisterType<QRCodeEncrptProductService>().As<IQRCodeEncrptProductService>().InstancePerRequest();
            builder.RegisterType<RFIDProductService>().As<IRFIDProductService>().InstancePerRequest();
            builder.RegisterType<ValueAddTaxService>().As<IValueAddTaxService>().InstancePerRequest();  //增值税发票
            builder.RegisterType<WebsiteService>().As<IWebSiteService>().InstancePerRequest();
            //pass MemoryCacheManager as cacheManager (cache settings between requests)
            //builder.RegisterType<WebSiteMappingService>().As<IWebSiteMappingService>()
            //    .WithParameter(ResolvedParameter.ForNamed<ICacheManager>("sm_cache_static"))
            //    .InstancePerRequest();

            //builder.RegisterType<DiscountService>().As<IDiscountService>().InstancePerRequest();

            //pass MemoryCacheManager as cacheManager (cache settings between requests)
            builder.RegisterType<SettingService>().As<ISettingService>()
                .WithParameter(ResolvedParameter.ForNamed<ICacheManager>("sm_cache_static"))
                .InstancePerRequest();
            builder.RegisterSource(new SettingsSource());
            //pass MemoryCacheManager as cacheManager (cache locales between requests)
            builder.RegisterType<LocalizationService>().As<ILocalizationService>()
                .WithParameter(ResolvedParameter.ForNamed<ICacheManager>("sm_cache_static"))
                .InstancePerRequest();

            //pass MemoryCacheManager as cacheManager (cache locales between requests)
            builder.RegisterType<LocalizedEntityService>().As<ILocalizedEntityService>()
                .WithParameter(ResolvedParameter.ForNamed<ICacheManager>("sm_cache_static"))
                .InstancePerRequest();
            builder.RegisterType<LanguageService>().As<ILanguageService>().InstancePerRequest();

            builder.RegisterType<DownloadService>().As<IDownloadService>().InstancePerRequest();
            builder.RegisterType<ImageCache>().As<IImageCache>().InstancePerRequest(); // codehint: sm-add
            builder.RegisterType<ImageResizerService>().As<IImageResizerService>().SingleInstance(); // xxx (http) // codehint: sm-add
            builder.RegisterType<PictureService>().As<IPictureService>().InstancePerRequest();
            builder.RegisterType<PTemplService>().As<IPTemplService>().InstancePerRequest();

            builder.RegisterType<EmailMessageTemplateService>().As<IEmailMessageTemplateService>().InstancePerRequest();
            builder.RegisterType<QueuedEmailService>().As<IQueuedEmailService>().InstancePerRequest();
            builder.RegisterType<NewsLetterSubscriptionService>().As<INewsLetterSubscriptionService>().InstancePerRequest();
            //builder.RegisterType<CampaignService>().As<ICampaignService>().InstancePerRequest();
            builder.RegisterType<EmailAccountService>().As<IEmailAccountService>().InstancePerRequest();
            builder.RegisterType<WorkflowMessageService>().As<IWorkflowMessageService>().InstancePerRequest();
            builder.RegisterType<MessageTokenProvider>().As<IMessageTokenProvider>().InstancePerRequest();
            builder.RegisterType<Tokenizer>().As<ITokenizer>().InstancePerRequest();
            builder.RegisterType<EmailSender>().As<IEmailSender>().SingleInstance(); // xxx (http)

            builder.RegisterType<EncryptionService>().As<IEncryptionService>().InstancePerRequest();

            //pass MemoryCacheManager to UrlRecordService as cacheManager (cache settings between requests)
            builder.RegisterType<UrlRecordService>().As<IUrlRecordService>()
                .WithParameter(ResolvedParameter.ForNamed<ICacheManager>("sm_cache_static"))
                .InstancePerRequest();

            builder.RegisterType<PermissionService>().As<IPermissionService>()
            .WithParameter(ResolvedParameter.ForNamed<ICacheManager>("sm_cache_static"))
            .InstancePerRequest();

            //-----------------TODO：李志杰           

            builder.RegisterType<DefaultLogger>().As<ILogger>().InstancePerRequest();
            builder.RegisterType<WineMenuService>().As<IWineMenuService>().InstancePerRequest();

            builder.RegisterType<WineMenuDetailService>().As<IWineMenuDetailService>().InstancePerRequest();

            builder.RegisterType<ConfirmBankPay>().InstancePerRequest();
            //-----------------TODO：李志杰

            builder.RegisterType<SitemapGenerator>().As<ISitemapGenerator>().InstancePerRequest();
            builder.RegisterType<PageTitleBuilder>().As<IPageTitleBuilder>().InstancePerRequest();

            builder.RegisterType<ScheduleTaskService>().As<IScheduleTaskService>().InstancePerRequest();

            builder.RegisterType<TelerikLocalizationServiceFactory>().As<Telerik.Web.Mvc.Infrastructure.ILocalizationServiceFactory>().InstancePerRequest();

            builder.RegisterType<EmbeddedViewResolver>().As<IEmbeddedViewResolver>().SingleInstance();
            builder.RegisterType<RoutePublisher>().As<IRoutePublisher>().SingleInstance();
            // codehint: sm-add

            builder.RegisterType<BundlePublisher>().As<IBundlePublisher>().SingleInstance();

            //HTML Editor services
            builder.RegisterType<NetAdvDirectoryService>().As<INetAdvDirectoryService>().InstancePerRequest();
            builder.RegisterType<NetAdvImageService>().As<INetAdvImageService>().SingleInstance(); // xxx (http)

            //Register event consumers
            var consumers = typeFinder.FindClassesOfType(typeof(IConsumer<>)).ToList();
            foreach (var consumer in consumers)
            {
                builder.RegisterType(consumer)
                    .As(consumer.FindInterfaces((type, criteria) =>
                    {
                        var isMatch = type.IsGenericType && ((Type)criteria).IsAssignableFrom(type.GetGenericTypeDefinition());
                        return isMatch;
                    }, typeof(IConsumer<>)))
                    .InstancePerRequest();
            }
            builder.RegisterType<EventPublisher>().As<IEventPublisher>().SingleInstance();
            builder.RegisterType<SubscriptionService>().As<ISubscriptionService>().SingleInstance();

            // register theming services (codehint: sm-add)

            // register UI component renderers (codehint: sm-add)
            builder.RegisterType<TabStripRenderer>().As<ComponentRenderer<TabStrip>>();
            builder.RegisterType<PagerRenderer>().As<ComponentRenderer<Pager>>();
            builder.RegisterType<WindowRenderer>().As<ComponentRenderer<Window>>();

            //// codehint: sm-add (enable mvc action filter property injection) >>> CRASHES! :-(
            //builder.RegisterFilterProvider();
            builder.RegisterType<TemplService>().As<ITemplService>().InstancePerRequest();

            builder.RegisterType<LuceneService>().As<ILuceneService>().InstancePerRequest();
            //#region 特殊优惠
            //builder.RegisterType<SpecialDiscountService>().As<ISpecialDiscountService>().InstancePerRequest();
            //#endregion

            //系统消息
            builder.RegisterType<SystemMessageService>().As<ISystemMessageService>().InstancePerRequest();
            //站内消息
            builder.RegisterType<StationMessageService>().As<IStationMessageService>().InstancePerRequest();
            //用户组
            builder.RegisterType<GroupService>().As<IGroupService>().InstancePerRequest();
            //Vip等级
            builder.RegisterType<VipGradeService>().As<IVipGradeService>().InstancePerRequest();
            //用户站内消息关联表
            builder.RegisterType<AccountStationMessageService>().As<IAccountStationMessageService>().InstancePerRequest();

            builder.RegisterType<ExtensionService>().As<IExtensionService>().InstancePerRequest();
            // 红酒券
            builder.RegisterType<WineWorldCouponService>().As<IWineWorldCouponService>().InstancePerRequest();

            builder.RegisterType<ImportExcelService>().As<IImportExcelService>().InstancePerRequest();
            builder.RegisterType<ImportManager>().As<IImportManager>().InstancePerRequest();

            //跨境电商商品服务
            builder.RegisterType<CBPProductService>().As<ICBPProductService>().InstancePerRequest();

            //套装商品服务
            builder.RegisterType<SuitProductService>().As<ISuitProductService>().InstancePerRequest();

            //预售
            builder.RegisterType<PresellService>().As<IPresellService>().InstancePerRequest();

            //众筹
            builder.RegisterType<CrowdFundingService>().As<ICrowdFundingService>().InstancePerHttpRequest();


            builder.RegisterType<LVBService>().As<ILVBService>().InstancePerHttpRequest();
            builder.RegisterType<IMService>().As<IIMService>().InstancePerHttpRequest();


            builder.RegisterType<ParticipleDictionaryService>().As<IParticipleDictionaryService>().InstancePerHttpRequest();

            builder.RegisterType<RebateWebSiteService>().As<IRebateWebSiteService>().InstancePerRequest();
            builder.RegisterType<RebateCheck_AccountService>().As<IRebateCheck_AccountService>().InstancePerRequest();
            builder.RegisterType<RebateWebSiteRateService>().As<IRebateWebSiteRateService>().InstancePerRequest();
            builder.RegisterType<ProductClassifyService>().As<IProductClassifyService>().InstancePerRequest();
            builder.RegisterType<Product_ProductClassify_MappingService>().As<IProduct_ProductClassify_MappingService>().InstancePerRequest();
        }

        public int Order
        {
            get { return 0; }
        }
    }

    public class SettingsSource : IRegistrationSource
    {
        private static readonly MethodInfo BuildMethod = typeof(SettingsSource).GetMethod(
            "BuildRegistration",
            BindingFlags.Static | BindingFlags.NonPublic);

        public IEnumerable<IComponentRegistration> RegistrationsFor(
                Service service,
                Func<Service, IEnumerable<IComponentRegistration>> registrations)
        {
            var ts = service as TypedService;
            if (ts != null && typeof(ISettings).IsAssignableFrom(ts.ServiceType))
            {
                var buildMethod = BuildMethod.MakeGenericMethod(ts.ServiceType);
                yield return (IComponentRegistration)buildMethod.Invoke(null, null);
            }
        }

        private static IComponentRegistration BuildRegistration<TSettings>() where TSettings : ISettings, new()
        {
            return RegistrationBuilder
                .ForDelegate((c, p) =>
                {
                    var currentWebSiteId = c.Resolve<IWebSiteContext>().CurrentWebsite.Id;
                    //uncomment the code below if you want load settings per store only when you have two stores installed.
                    //var currentWebSiteId = c.Resolve<IWebSiteService>().GetAllWebSites().Count > 1
                    //    c.Resolve<IWebSiteContext>().CurrentWebSite.Id : 0;

                    //although it's better to connect to your database and execute the following SQL:
                    //DELETE FROM [Setting] WHERE [WebSiteId] > 0
                    return c.Resolve<ISettingService>().LoadSetting<TSettings>(currentWebSiteId);
                })
                .InstancePerRequest()
                .CreateRegistration();
        }

        public bool IsAdapterForIndividualComponents { get { return false; } }
    }
}