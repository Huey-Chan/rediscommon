using Shop.Core;
using Shop.Core.Caching;
using Shop.Core.Infrastructure;
using Shop.Data.Domain;
using Shop.Data.Interface;
using Shop.Services.Column;
using Shop.Services.Coupon;
using Shop.Services.Customers;
using Shop.Services.Events;
using Shop.Services.Logging;
using Shop.Services.Messages;
using Shop.Services.Products;
using Shop.Services.Promotion;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml;
using Tools;
using Web.Model.ShopProject.Coupon;
using Web.Model.ShopProject.Orders;
using Web.Model.ShopProject.ProductCoupon;
using Web.Model.ShopProject.JDM;
using Shop.Data.Domain.Customers;
using System.Text.RegularExpressions;
using Shop.Services.Extension;
using Shop.Services.CrossBorderPurchase;
using System.IO;
using Lucene.Net.Support;
using Shop.Data.Mapping;
using Shop.Services.Presell;
using Shop.Services.Common;
using Shop.PayState;
using Shop.Services.CrowdFundings;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Web.Model.ShopProject.Customer;
using Web.Model.ShopProject.Product;
using Shop.Services.Lucene;
using Shop.Data.Domain.Messages;
using Shop.Services.UmengNotification;

namespace Shop.Services.Orders
{
    public partial class OrderService : IOrderService
    {
        #region 全局变量

        private static object lockObj = new object();
        private readonly string ZM_KEY = "zhongmin.cn_wine";
        private readonly int HongJiuCustomerId = ConfigurationManager.AppSettings["HongJiuCustomerId"].ToInt();

        #endregion 全局变量

        #region Fields
        private readonly IShopUnitOfWork _shopUnitOfWork;
        private readonly IEventPublisher _eventPublisher;
        private readonly IWorkContext _workContext;
        private readonly OrderSettings _orderSettings;
        private readonly IPaymentTypeService _paymentTypeService;
        private readonly IProductService _productService;
        private readonly ILogger _logger;
        private readonly IPromotionsService _promotionsService;
        private readonly IAccountService _accountService;
        private readonly ICouponService _couponService;
        private readonly IColumnService _columnService;
        private readonly IPresellService _presellService;
        private readonly ICBPProductService _cBPProductService;
        private readonly ConfirmBankPay _confirmBankPay;
        private readonly ICrowdFundingService _crowdFundingService;
        private readonly IBuyProductCDKeyService _buyProductCDKeyService;
        private readonly ILuceneService _luceneService;
        private readonly IStationMessageService _stationMessageService;
        #endregion Fields

        #region Ctor
        public OrderService(
            IEventPublisher eventPublisher,
            IShopUnitOfWork shopUnitOfWork,
            IWorkContext workContext,
            OrderSettings orderSettings,
            IPaymentTypeService paymentTypeService,
            IProductService productService,
            ILogger logger,
            IPromotionsService promotionsService,
            IAccountService accountService,
            ICouponService couponService,
            IColumnService columnService,
            ICBPProductService cBPProductService,
            IPresellService presellService,
            ConfirmBankPay confirmBankPay,
            ICrowdFundingService crowdFundingService,
            IBuyProductCDKeyService buyProductCDKeyService,
            ILuceneService luceneService,
            IStationMessageService stationMessageService)
        {
            this._eventPublisher = eventPublisher;
            this._shopUnitOfWork = shopUnitOfWork;
            this._workContext = workContext;
            this._orderSettings = orderSettings;
            this._paymentTypeService = paymentTypeService;
            this._productService = productService;
            this._logger = logger;
            this._promotionsService = promotionsService;
            this._accountService = accountService;
            this._couponService = couponService;
            this._columnService = columnService;
            this._cBPProductService = cBPProductService;
            this._presellService = presellService;
            this._confirmBankPay = confirmBankPay;
            this._crowdFundingService = crowdFundingService;
            this._buyProductCDKeyService = buyProductCDKeyService;
            this._luceneService = luceneService;
            this._stationMessageService = stationMessageService;
        }

        #endregion Ctor

        #region Methods

        #region Orders

        /// <summary>
        /// Gets an order
        /// </summary>
        /// <param name="orderId">The order identifier</param>
        /// <returns>Order</returns>
        public virtual Order GetOrderById(int orderId)
        {
            if (orderId == 0)
                return null;

            return _shopUnitOfWork.GetById<Order>(orderId);
        }

        public virtual Delivery100 GetDelivery100ById(int Id)
        {
            if (Id == 0)
                return null;

            return _shopUnitOfWork.GetById<Delivery100>(Id);
        }
        /// <summary>
        /// 获取所有的以保存的快递公司
        /// </summary>
        /// <returns> 快递公司列表</returns>
        public virtual List<Delivery100> GetDelivery100ByIsvalid()
        {
            var query = _shopUnitOfWork.Get<Delivery100>().Where(x => x.Isvalid);
            return query.ToList();
        }
        public virtual int GetDelivery100IdForJiuMi(string jiuMiDelivery)
        {
            var result = (from d in _shopUnitOfWork.Get<Delivery100>() where d.JiuMiDelivery.Equals(jiuMiDelivery) select d.Id).FirstOrDefault();
            return result;
        }
        public Delivery100 GetDelivery100ByEfastDelivery(string efastDelivery)
        {
            if (efastDelivery.IsNullOrEmpty())
            {
                return null;
            }
            var result = _shopUnitOfWork.Get<Delivery100>().Where(t => t.EfastDelivery == efastDelivery).FirstOrDefault();
            return result;
        }
        public virtual string GetCompanyName(int type)
        {
            string CompanyName = (from o in _shopUnitOfWork.Get<Delivery100>()
                                  where o.Id == type
                                  select o.CompanyName).FirstOrDefault();
            return CompanyName;
        }

        /// <summary>
        /// Get orders by identifiers
        /// </summary>
        /// <param name="orderIds">Order identifiers</param>
        /// <returns>Order</returns>
        public virtual IList<Order> GetOrdersByIds(int[] orderIds)
        {
            if (orderIds == null || orderIds.Length == 0)
                return new List<Shop.Data.Domain.Order>();

            var query = from o in _shopUnitOfWork.Get<Order>()
                        where orderIds.Contains(o.Id)
                        select o;
            var orders = query.ToList();
            //sort by passed identifiers
            var sortedOrders = new List<Shop.Data.Domain.Order>();
            foreach (int id in orderIds)
            {
                var order = orders.Find(x => x.Id == id);
                if (order != null)
                    sortedOrders.Add(order);
            }
            return sortedOrders;
        }
        /// <summary>
        /// 根据时间段获取已经付款的有效订单
        /// </summary>
        /// <param name="BeginTime">开始时间</param>
        /// <param name="EndTime">结束时间</param>
        /// <returns>订单</returns>
        public virtual IQueryable<AccountExtend> GetOrdersByTime(DateTime BeginTime, DateTime EndTime)
        {
            var query = from o in _shopUnitOfWork.Get<Order>()
                        where o.State == OrderState.Complete || o.State == OrderState.Paid || o.State == OrderState.Shipped || o.State == OrderState.PaidNotCompleted
                        select o;
            var list = query.Where(x => x.OrderGenerateDate >= BeginTime && x.OrderGenerateDate <= EndTime).Select(y => y.AccountExtend).Distinct();
            //var quer= list.Select(x => new { x.UserName,x.Mobile }).ToList();
            return list;
        }
        public IList<Order> GetOrdersByIdString(string ids)
        {
            string sql = string.Format("select * from [order] o where o.id in ({0});", ids);
            return _shopUnitOfWork.Context.Database.SqlQuery<Order>(sql).ToList();
        }
        public virtual Order GetOrderByNumber(string orderNumber)
        {
            if (orderNumber.IsEmpty())
            {
                return null;
            }

            var query = from o in _shopUnitOfWork.Get<Order>()
                        where o.SerialNumber == orderNumber
                        select o;
            var order = query.FirstOrDefault();

            int id = 0;
            if (order == null && int.TryParse(orderNumber, out id) && id > 0)
            {
                return this.GetOrderById(id);
            }

            return order;
        }

        public virtual Order GetOrderByJiuYeOrderId(string jiuYeOrderId)
        {
            if (jiuYeOrderId.IsEmpty())
            {
                return null;
            }

            var query = from o in _shopUnitOfWork.Get<Order>()
                        where o.JiuYeOrderId == jiuYeOrderId
                        select o;

            var order = query.FirstOrDefault();

            return order;
        }

        /// <summary>
        /// Deletes an order
        /// </summary>
        /// <param name="order">The order</param>
        public virtual void DeleteOrder(Shop.Data.Domain.Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            order.Isvalid = false;
            UpdateOrder(order);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="searchOrderContext"></param>
        /// <returns></returns>
        public virtual IPagedList<Order> SearchOrders(SearchOrderContext searchOrderContext)
        {
            var query = PrepareSearchUsualOrderQuery(searchOrderContext);
            return new PagedList<Order>(query, searchOrderContext.PageIndex, searchOrderContext.PageSize);
        }

        public IPagedList<Order> SearchCooperationOrders(SearchOrderContext searchOrderContext, string rebateWebSiteId, string PlatformCode)
        {
            var query = PrepareSearchUsualOrderQuery(searchOrderContext).Where(x => x.Isvalid && (x.RebateWebSiteId == rebateWebSiteId || x.PlatformCode == PlatformCode));
            return new PagedList<Order>(query, searchOrderContext.PageIndex, searchOrderContext.PageSize);
        }

        #region 获取当前活动ID下的已付款订单 by yzc;注意判断优惠类型DiscountType（参加活动优惠或参加会员优惠）
        public virtual IPagedList<Order> SearchOrdersByPromotionsId(int PromotionsId, int PageIndex, int PageSize, bool isAll = false)
        {
            PromotionsCategory PromotionsCategoryId = _shopUnitOfWork.Get<Promotions>().Where(p => p.Id == PromotionsId).FirstOrDefault().PromotionsCategoryId;
            IQueryable<Order> query = null;
            switch (PromotionsCategoryId)
            {   //满免订单
                case PromotionsCategory.FullFree: query = GetOrderByPromotionsId(PromotionsId, PromotionsCategory.FullFree); break;
                //买赠订单
                case PromotionsCategory.Gift: query = GetGiftOrderByPromotionsId(PromotionsId); break;
                //支付买赠
                case PromotionsCategory.PayGift: query = GetPayGiftOrderByPromotionsId(PromotionsId); break;
                //首次APP下载，优惠活动
                case PromotionsCategory.APPCoupon: query = GetAPPCouponOrderByPromotionsId(PromotionsId); break;

                //APP推广促销活动(线上没有这个活动)
                case PromotionsCategory.APPPromotion: query = null; break;
                //限时优惠活动
                case PromotionsCategory.NewPricePromotion: query = GetNewPricePromotionOrderByPromotionsId(PromotionsId); break;
                //赠券活动
                case PromotionsCategory.GiveCouponPromotion: query = GetGiveCouponPromotionOrderByPromotionsId(PromotionsId); break;
                //加价换购
                case PromotionsCategory.FareIncrease: query = GetFareIncreaseOrderByPromotionsId(PromotionsId); break;
                //满折订单
                case PromotionsCategory.FullDiscount: query = GetOrderByPromotionsId(PromotionsId, PromotionsCategory.FullDiscount); break;
                //多买促销（M元任选N件）订单
                case PromotionsCategory.BuyOptional: query = GetOrderByPromotionsId(PromotionsId, PromotionsCategory.BuyOptional); break;
                case PromotionsCategory.IntegralAccelerate: query = GetIntegralPromotionOrderByPromotionsId(PromotionsId); break;
                default: query = null; break;
            }
            if (query != null)
            {
                query = query.Where(p => p.OrderType == 1).Distinct(p => p.Id).OrderByDescending(p => p.OrderGenerateDate).AsQueryable();
            }
            else
            { //没有订单返回空
                query = _shopUnitOfWork.Get<Order>().Where(p => p.Id == -1).OrderByDescending(p => p.OrderGenerateDate);
            }
            if (isAll)
            {
                PageSize = query.Count();
            }
            return new PagedList<Order>(query, PageIndex, PageSize);
        }
        /// <summary>
        /// 根据活动ID获取订单(可能没满足活动条件)
        /// </summary>
        /// <param name="PromotionsId">活动ID</param>
        /// <returns></returns>
        public IQueryable<Order> GetOrderByPromotionsId(int PromotionsId, PromotionsCategory cayegory)
        {
            string Sql = string.Format("SELECT distinct o.id FROM dbo.[Order] o INNER JOIN dbo.OrderProduct op ON op.OrderId=o.Id AND op.DiscountType={1} " +
"INNER JOIN dbo.Product p ON p.id=op.ProductId INNER JOIN dbo.Product_Promotions_Mapping ppm ON ppm.ProductId=p.Id " +
"INNER JOIN dbo.Promotions ps ON ps.id=ppm.PromotionsId WHERE o.OrderGenerateDate BETWEEN ps.StartDate AND ps.EndDate AND o.state IN(3,4,5)AND ps.Id={0}",
PromotionsId, (int)DiscountType.Promotion);
            var f = _shopUnitOfWork.Get<FullPromotion>().Where(p => p.Id == PromotionsId).FirstOrDefault();
            var buyPromotion = _promotionsService.GetBuyOptionalPromotions(PromotionsId);
            int condition = 0;

            switch (cayegory)
            {
                case PromotionsCategory.FullDiscount:
                    var gradientDiscount = _shopUnitOfWork.Get<FullDiscountGradient>().Where(p => p.FullPromotionsId == PromotionsId).OrderBy(p => p.Condition).FirstOrDefault();
                    condition = gradientDiscount == null ? 0 : gradientDiscount.Condition;
                    break;
                case PromotionsCategory.FullFree:
                    var gradientFree = _shopUnitOfWork.Get<FullGradient>().Where(p => p.FullPromotionsId == PromotionsId).OrderBy(p => p.Condition).FirstOrDefault();
                    condition = gradientFree == null ? 0 : gradientFree.Condition;
                    break;
                case PromotionsCategory.BuyOptional:
                    condition = buyPromotion == null ? 0 : buyPromotion.Condition;
                    break;
                default:
                    break;
            }

            if (cayegory == PromotionsCategory.BuyOptional)
            {
                if (buyPromotion == null || condition == 0)
                {
                    return null;
                }
                if (buyPromotion.ConditionCategory == FullPromotionConditionCategory.ToNum)//瓶数
                {
                    if (buyPromotion.ConditionType == FullPromotionConditionType.ToToal)//总量
                    {
                        Sql = Sql + " group by o.Id having (SUM(op.Quantity)%" + condition + " )=0";
                    }
                    if (buyPromotion.ConditionType == FullPromotionConditionType.ToSingle)//单量
                    {
                        Sql = Sql + " and (op.quantity%" + condition + " )=0";
                    }
                }
                else//暂不考虑价格条件
                {
                    return null;
                }
            }
            else
            {

                //还没有设置满免条件，直接返回空
                if (f == null || condition == 0)
                {
                    return null;
                }
                if (f.ConditionCategory == FullPromotionConditionCategory.ToNum && f.ConditionType == FullPromotionConditionType.ToSingle)
                {
                    Sql = Sql + " and op.quantity>=" + condition;
                }
                if (f.ConditionCategory == FullPromotionConditionCategory.ToNum && f.ConditionType == FullPromotionConditionType.ToToal)
                {
                    Sql = Sql + " group by o.Id having SUM(op.Quantity)>=" + condition;
                }
                if (f.ConditionCategory == FullPromotionConditionCategory.ToPrice && f.ConditionType == FullPromotionConditionType.ToSingle)
                {
                    Sql = Sql + " and op.price>=" + condition;
                }
                if (f.ConditionCategory == FullPromotionConditionCategory.ToPrice && f.ConditionType == FullPromotionConditionType.ToToal)
                {
                    Sql = Sql + "group by o.Id having SUM(op.Price)>=" + condition;
                }
            }
            List<int> orderId = _shopUnitOfWork.Context.Database.SqlQuery<int>(Sql).ToList();
            var result = _shopUnitOfWork.Get<Order>().Where(p => orderId.Contains(p.Id));
            return result;
        }
        /// <summary>
        /// 获取买赠订单
        /// </summary>
        /// <param name="PromotionsId">当前活动id为买赠类型</param>
        /// <returns></returns>
        public IQueryable<Order> GetGiftOrderByPromotionsId(int PromotionsId)
        {
            string Sql = string.Format("SELECT distinct o.id FROM dbo.[Order] o " +
                                "INNER JOIN dbo.OrderProduct op ON op.OrderId=o.Id " +
                                "INNER JOIN dbo.OrderProductGifts opg ON opg.OrderProductId=op.Id or opg.OrderId=o.Id " +
                                "INNER JOIN dbo.Promotions ps ON ps.id=opg.GiftPromotionsId " +
                                "WHERE o.OrderGenerateDate BETWEEN ps.StartDate AND ps.EndDate AND o.state IN(3,4,5) " +
                                "AND ps.Id={0} and o.Isvalid=1 " +
                                "union " +
                                "SELECT o.id FROM dbo.[Order] o " +
                                "INNER JOIN dbo.OrderProduct op ON op.OrderId=o.Id " +
                                "INNER JOIN dbo.OrderProductCouponGifts opcg ON opcg.OrderProductId=op.Id or opcg.OrderId=o.Id " +
                                "INNER JOIN dbo.Promotions ps ON ps.id=opcg.GiftPromotionsId " +
                                "WHERE o.OrderGenerateDate BETWEEN ps.StartDate AND ps.EndDate AND o.state IN(3,4,5) " +
                                "AND ps.Id={0} and o.Isvalid=1", PromotionsId);
            List<int> orderId = _shopUnitOfWork.Context.Database.SqlQuery<int>(Sql).ToList();
            var result = _shopUnitOfWork.Get<Order>().Where(p => orderId.Contains(p.Id));
            return result;
        }
        /// <summary>
        /// 获取支付买赠订单
        /// </summary>
        /// <param name="PromotionsId"></param>
        /// <returns></returns>
        public IQueryable<Order> GetPayGiftOrderByPromotionsId(int PromotionsId)
        {
            //获取当前支付买赠的支付方式
            var gp = _shopUnitOfWork.Get<GiftPromotions>().Where(p => p.Id == PromotionsId).FirstOrDefault();
            if (gp == null) { return null; }
            var PayCode = gp.PayCode;
            List<string> payType = PayCode.Split(',').ToList();
            //满足买赠活动，并且支付方式是满足活动要求
            var query = GetGiftOrderByPromotionsId(PromotionsId).Where(p => payType.Contains(p.PaymentType.Code));
            return query;
        }
        /// <summary>
        /// APP首次下载赠券活动
        /// </summary>
        /// <param name="PromotionsId"></param>
        /// <returns></returns>
        public IQueryable<Order> GetAPPCouponOrderByPromotionsId(int PromotionsId)
        {
            //APP赠券渠道码
            string APPDownLoadChanelCode1 = System.Configuration.ConfigurationManager.AppSettings["APPDownLoadChanelCode1"];
            string APPDownLoadChanelCode2 = System.Configuration.ConfigurationManager.AppSettings["APPDownLoadChanelCode2"];
            string Sql = string.Format("SELECT distinct o.id FROM dbo.[Order] o " +
                    "INNER JOIN dbo.OrderProduct op ON op.OrderId=o.Id " +
                    "INNER JOIN dbo.Order_Product_Coupon opc on op.Id=opc.OrderProductId " +
                    "INNER JOIN dbo.Coupon c ON c.Id=opc.CouponId " +
                    "INNER JOIN dbo.CouponCategory_Type_Chanel_Mapping ctcm on c.CouponCategory_Type_Chanel_MappingId=ctcm.Id " +
                    "WHERE o.state IN(3,4,5) and ctcm.ChanelCode in('{0}','{1}') and o.Isvalid=1", APPDownLoadChanelCode1, APPDownLoadChanelCode2);
            List<int> orderId = _shopUnitOfWork.Context.Database.SqlQuery<int>(Sql).ToList();
            var result = _shopUnitOfWork.Get<Order>().Where(p => orderId.Contains(p.Id));
            return result;
        }
        /// <summary>
        /// 加价换购
        /// </summary>
        /// <param name="PromotionsId"></param>
        /// <returns></returns>
        public IQueryable<Order> GetFareIncreaseOrderByPromotionsId(int PromotionsId)
        {
            string Sql = string.Format("SELECT distinct o.id FROM dbo.[Order] o " +
                                "INNER JOIN dbo.OrderProduct op ON op.OrderId=o.Id " +
                                "INNER JOIN dbo.OrderProductGifts opg ON opg.OrderProductId=op.Id or opg.OrderId=o.Id " +
                                "INNER JOIN dbo.Promotions ps ON ps.id=opg.GiftPromotionsId " +
                                "WHERE ps.Id={0} AND o.OrderGenerateDate BETWEEN ps.StartDate AND ps.EndDate AND o.state IN(3,4,5) " +
                                "and o.Isvalid=1", PromotionsId);
            List<int> orderId = _shopUnitOfWork.Context.Database.SqlQuery<int>(Sql).ToList();
            var result = _shopUnitOfWork.Get<Order>().Where(p => orderId.Contains(p.Id));
            return result;
        }
        /// <summary>
        /// 赠券活动
        /// </summary>
        /// <param name="PromotionsId"></param>
        /// <returns></returns>
        public IQueryable<Order> GetGiveCouponPromotionOrderByPromotionsId(int PromotionsId)
        {
            string Sql = string.Format("SELECT distinct o.id FROM " +
                        "(select p.StartDate,p.EndDate, p.Id from Promotions p where Id='{0}') as ps " +
                        "inner join dbo.GiveCouponPromotion gcp on ps.Id=gcp.PromotionId " +
                        "inner  join Product_Promotions_Mapping ppm on ppm.PromotionsId=ps.Id " +
                        "inner join OrderProduct op on op.ProductId=ppm.ProductId " +
                        "INNER JOIN dbo.Order_Product_Coupon opc on op.Id=opc.OrderProductId " +
                        "INNER JOIN dbo.Coupon c ON c.Id=opc.CouponId " +
                        "INNER JOIN dbo.CouponCategory_Type_Chanel_Mapping ctcm on c.CouponCategory_Type_Chanel_MappingId=ctcm.Id " +
                        "and ctcm.ChanelCode=gcp.ChanelCode " +
                        "inner join [Order] o on o.Id=op.OrderId " +
                        "where o.OrderGenerateDate BETWEEN ps.StartDate AND ps.EndDate and o.state IN(3,4,5) and o.Isvalid=1;", PromotionsId);
            List<int> orderId = _shopUnitOfWork.Context.Database.SqlQuery<int>(Sql).ToList();
            var result = _shopUnitOfWork.Get<Order>().Where(p => orderId.Contains(p.Id));
            return result;
        }
        /// <summary>
        /// 限时优惠
        /// </summary>
        /// <param name="PromotionsId"></param>
        /// <returns></returns>
        public IQueryable<Order> GetNewPricePromotionOrderByPromotionsId(int PromotionsId)
        {
            //付款未发货，已发货，已完成
            List<OrderState> states = new List<OrderState> { OrderState.Paid, OrderState.Shipped, OrderState.Complete };
            var query = from op in _shopUnitOfWork.Get<OrderProduct>().Where(p => p.Isvalid && p.DiscountType == DiscountType.Promotion)
                        join ppm in _shopUnitOfWork.Get<Product_Promotions_Mapping>().Where(p => p.UnitPrice != null) on op.ProductId equals ppm.ProductId
                        join orders in _shopUnitOfWork.Get<Order>().Where(p => p.Isvalid && states.Contains(p.State)) on op.OrderId equals orders.Id
                        join p in _shopUnitOfWork.Get<Promotions>().Where(p => p.Id == PromotionsId) on ppm.PromotionsId equals p.Id
                        where orders.OrderGenerateDate >= p.StartDate && orders.OrderGenerateDate <= p.EndDate
                        select orders;
            return query;
        }

        /// <summary>
        /// 中民积分加速
        /// </summary>
        /// <param name="PromotionsId"></param>
        /// <returns></returns>
        public IQueryable<Order> GetIntegralPromotionOrderByPromotionsId(int promotionsId)
        {
            //付款未发货，已发货，已完成
            List<OrderState> states = new List<OrderState> { OrderState.Paid, OrderState.Shipped, OrderState.Complete };
            var query = from op in _shopUnitOfWork.Get<OrderProduct>().Where(p => p.Isvalid && p.DiscountType == DiscountType.Promotion)
                        join ppm in _shopUnitOfWork.Get<Product_Promotions_Mapping>().Where(p => p.GetIntegrationValue != null) on op.ProductId equals ppm.ProductId
                        join orders in _shopUnitOfWork.Get<Order>().Where(p => p.Isvalid && states.Contains(p.State)) on op.OrderId equals orders.Id
                        join p in _shopUnitOfWork.Get<Promotions>().Where(p => p.Id == promotionsId) on ppm.PromotionsId equals p.Id
                        where orders.OrderGenerateDate >= p.StartDate && orders.OrderGenerateDate <= p.EndDate
                        select orders;
            return query;
        }
        #endregion
        public IPagedList<Order> GetAllOrdersByPage(SearchOrderContext searchOrderContext)
        {
            var query = _shopUnitOfWork.Get<Order>().Where(t => t.Isvalid && t.AccountId == searchOrderContext.CustomerId).OrderByDescending(o => o.OrderGenerateDate);
            return new PagedList<Order>(query, searchOrderContext.PageIndex, searchOrderContext.PageSize);
        }

        //public IPagedList<Order> GetPagedExchangedOrders(int pageIndex, int pageSize)
        //{
        //    var query = _shopUnitOfWork.Get<Order>()
        //        .Where(t => t.Isvalid)
        //        //只正常的单（不提货码的单）
        //        .Where(p => p.OrderType == (int)OrderStyle.ExchangedCard)
        //        .OrderByDescending(p => p.CreatedTime);
        //    return new PagedList<Order>(query, pageIndex, pageSize);
        //}

        public virtual IPagedList<Order> SearchRebateOrders(RebateOrderSearchContext searchOrderContext)
        {
            var query = PrepareSearchRebateOrderQuery(searchOrderContext);
            return new PagedList<Order>(query, searchOrderContext.PageIndex, searchOrderContext.PageSize);
        }

        public virtual IList<Order> SearchRebateOrdersNoPages(RebateOrderSearchContext searchOrderContext)
        {
            var query = PrepareSearchRebateOrderQuery(searchOrderContext);
            return query.ToList();
        }

        public IQueryable<Order> PrepareSearchRebateOrderQuery(RebateOrderSearchContext searchOrderContext)
        {
            var query = _shopUnitOfWork.Get<Order>().Where(t => t.Isvalid && t.RebateOrderProducts.Count > 0 && !string.IsNullOrEmpty(t.RebateWebSiteId) && !string.IsNullOrEmpty(t.RebateWebSite_Uid));

            if (!string.IsNullOrEmpty(searchOrderContext.SearchRebateWebSiteId))
            {
                query = query.Where(o => o.RebateWebSiteId.Equals(searchOrderContext.SearchRebateWebSiteId));
            }

            if (searchOrderContext.SearchOderNumber.HasValue())
            {
                query = query.Where(o => o.SerialNumber.Contains(searchOrderContext.SearchOderNumber));
            }

            if (!searchOrderContext.RecipeNameOrMobile.IsNullOrEmpty())
            {
                query = query.Where(o => o.Address.ReceiptName.Contains(searchOrderContext.RecipeNameOrMobile) || o.Address.MobileNumber.Contains(searchOrderContext.RecipeNameOrMobile));
            }

            if (searchOrderContext.SearchRebateState != 0)
            {
                switch (searchOrderContext.SearchRebateState)
                {
                    case 1:
                        query = query.Where(o => o.State == OrderState.NotPay);
                        break;

                    case -1:
                        query = query.Where(o => o.State == OrderState.Invalid || o.State == OrderState.Cancelled || o.State == OrderState.RevokeOrder);
                        break;

                    case 2:
                        query = query.Where(o => o.State == OrderState.Paid || o.State == OrderState.Shipped);
                        break;

                    case 3:
                        query = query.Where(o => o.State == OrderState.Complete);
                        break;
                }
            }

            if (searchOrderContext.SearchOrderStartTime.HasValue)
            {
                query = query.Where(o => o.OrderGenerateDate >= searchOrderContext.SearchOrderStartTime.Value);
            }
            if (searchOrderContext.SearchOrderEndTime.HasValue)
            {
                query = query.Where(o => o.OrderGenerateDate <= searchOrderContext.SearchOrderEndTime.Value);
            }

            return query.Where(o => o.Isvalid).OrderByDescending(o => o.OrderGenerateDate);
        }

        public void SearchOrdersStatistics(SearchOrderContext searchOrderContext,
            ref int footerIntegral,
            ref int footerGetIntegration,
            ref decimal footerCoupon,
            ref double footerWCoupon,
            ref double footerWineWorldCoupon,
            ref decimal footerMoney,
            ref decimal footerPrice,
            ref decimal footerFullFreePrice,
            ref decimal footerFullDiscountPrice,
            out double footerMyProductCoupon)
        {
            var query = PrepareSearchUsualOrderQuery(searchOrderContext);
            if (query.Count() > 0)
            {
                footerIntegral = query.Where(x => x.IntegralValue != null).Sum(x => x.IntegralValue);
                footerGetIntegration = query.Where(x => x.GetIntegrationValue != null).Sum(x => x.GetIntegrationValue);
                footerCoupon = query.Where(x => x.ZMCoupon != null).Sum(x => x.ZMCoupon);
                footerWCoupon = query.Where(x => x.WineCoupon != null).Sum(x => x.WineCoupon);
                footerWineWorldCoupon = query.Where(x => x.WineWorldCoupon != null).Sum(x => x.WineWorldCoupon);
                footerMoney = query.Where(x => x.FactPrice != null).Sum(x => x.FactPrice);
                footerMyProductCoupon = query.Where(x => x.ProductCoupon != null).Sum(x => x.ProductCoupon);
                footerFullFreePrice = query.Where(x => x.FullFreePrice != null).Sum(x => x.FullFreePrice);
                footerFullDiscountPrice = query.Where(x => x.FullDiscountPrice != null).Sum(x => x.FullDiscountPrice);
                footerPrice = footerCoupon + (decimal)footerWCoupon + footerMoney + footerFullFreePrice + (decimal)footerMyProductCoupon + footerFullDiscountPrice;
            }
            else
            {
                footerIntegral = 0;
                footerCoupon = 0;
                footerWCoupon = 0;
                footerWineWorldCoupon = 0;
                footerMoney = 0;
                footerPrice = 0;
                footerFullFreePrice = 0;
                footerFullDiscountPrice = 0;
                footerMyProductCoupon = 0;
            }
        }
        #region 导出初次购买的用户
        public DataTable ExportFirstBuysUserName(Nullable<DateTime> StartTime, Nullable<DateTime> EndTime, OrderStyle orderStyle = OrderStyle.Usual)
        {
            var sql = new StringBuilder();
            sql.Append(@"select top 1 '订单号','用户名','收货人','收货地址','收货人电话','商品名称','商品价格','商品数量','订单类型','订单总价' from [order] union all ");
            sql.Append(@"select t.SerialNumber, t.userName,t.CustomerName ,t.AddressDetail,t.MobilePhone,p.Name,CONVERT(varchar(100),op.Price),CONVERT(varchar(100),op.Quantity),
		case when t.OrderType=1 then '正常单' 
			 when t.OrderType=2 then '换购卡' 
			 when t.orderType=3 then '邀请值兑换订单'
			 when t.orderType=4 then '跨境电商单'
			 when t.orderType=5 then '套装订单'
			 when t.orderType=6 then '海外直购订单'
			 when t.orderType=7 then '期酒订单'
			 when t.orderType=8 then '众筹订单'
			 when t.orderType=9 then '合作商订单'
		end,CONVERT(varchar(100),t.SumPrice)
	from
	(
		select row_number() over(partition  by username order by OrderGenerateDate) as rownum, a.UserName ,o.SerialNumber,o.CustomerName,o.AddressDetail,o.MobilePhone,o.OrderType,o.SumPrice,o.OrderGenerateDate,o.Id from [order] o  
			,AccountExtend a
        where o.[state] in (3,4,5,9,11) and a.id=o.AccountId");
            switch (orderStyle)
            {
                case OrderStyle.Usual:
                    sql.Append(" and o.OrderType =1 "); break;
                case OrderStyle.CBP:
                    sql.Append(" and o.OrderType =4  "); break;
                case OrderStyle.Expect:
                    sql.Append(" and o.OrderType =7 "); break;
            }
            sql.Append(@"  )t , OrderProduct as op,Product as p
                        where t.rownum=1  and t.Id=op.OrderId and op.ProductId=p.Id");
            //开始时间
            if (StartTime.HasValue)
            {
                sql.AppendFormat(" and t.OrderGenerateDate>='{0}' ", StartTime.Value);
            }
            //结束时间
            if (EndTime.HasValue)
            {
                sql.AppendFormat(" and t.OrderGenerateDate<='{0}' ", EndTime.Value);
            }
            return ExeSqlReturnDT(sql.ToString(), null);
        }
        #endregion

        public DataTable ExportResult(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo)
        {
            var sb = new StringBuilder();
            int type = Convert.ToInt32(searchOrderContext.OrderStyle);
            string customerFileds = ",'用户名','收货人手机号','收货地址','通讯邮箱'";

            #region 拼接字符串
            sb.Append(@"
                        select top 1
                        '订单号',
                        '支付号',
                        '订单状态',
                        '现金',
                        '支付方式',
                        '中民积分',
                        '中民积分（新_总）',
                        '中民积分（新）来源（中民积分宝）',
                        '中民积分（新）来源（中民保险网）',
                        '中民积分（新）来源（红酒世界网）',
                        '中民券',
                        '中民红酒券',
                        '红酒券',
                        '代金券',
                        '满减优惠',
                        '总价',
                        '发票',
                        '备注',
                        '订单生成时间',
                        '姓名',                        
                        '业务员',
                        '收货人姓名'".Replace("\r\n", " "));
            if (excelContainCustomerInfo)
            {
                sb.Append(customerFileds);
            }
            sb.Append(" from [order] union all select ");

            sb.Append(@"
                        o.SerialNumber as '订单号',
                        o.PayNumber,
                        case
                        when o.State=1 then '未付款'
                        when o.State=2 then '已失效'
                        when o.State=3 then '已付款'
                        when o.State=4 then '已发货'
                        when o.State=5 then '已完成'
                        when o.State=6 then '已取消'
                        when o.State=7 then '已退单'
                        when o.State=10 then '待确认'
                        when o.State=11 then '付款未完结'
                        else 'Error'
                        end as '订单状态',
                        CONVERT(VARCHAR(100),o.FactPrice) as '现金',
                        case o.factprice when 0 then '' else pay.Name end as '支付方式', 
                        CONVERT(VARCHAR(100),o.ZMIntegralValue) as '中民积分', 
                        CONVERT(VARCHAR(100),o.IntegralValue) as '中民积分（新）',
(select top 1 CONVERT(nVARCHAR(100),value) from dbo.ordermoneysource where orderid=o.id and [key]='中民积分' and [source]=1),
(select top 1 CONVERT(nVARCHAR(100),value) from dbo.ordermoneysource where orderid=o.id and [key]='中民积分' and [source]=2),
(select top 1 CONVERT(nVARCHAR(100),value) from dbo.ordermoneysource where orderid=o.id and [key]='中民积分' and [source]=3),
                        CONVERT(VARCHAR(100),o.ZMCoupon) as '中民券',
                        CONVERT(VARCHAR(100),o.WineCoupon) as '中民红酒券',
                        CONVERT(VARCHAR(100),o.WineWorldCoupon) as '红酒券',
                        CONVERT(VARCHAR(100),o.ProductCoupon) as '代金券',
                        CONVERT(VARCHAR(100),o.FullFreePrice) as '满减优惠',
                        CONVERT(VARCHAR(100),o.FactPrice+o.FullFreePrice+ o.ZMIntegralValue+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon+ (o.IntegralValue/100)) as '总价',
                        case
                        when o.IsNeedInvoice=0 then ''
                        else
                            case
                            when o.FactPrice!=0 then '需要'
                            else ''
                            end
                        end as '发票',
                        o.adminremark as  '备注',
                        CONVERT(VARCHAR(100),o.OrderGenerateDate,25) as '订单生成时间',a.name,s.name,o.CustomerName ");
            if (excelContainCustomerInfo)
            {
                sb.Append(" ,a.username,o.MobilePhone,o.AddressDetail,a.ReceiveEmail ");
            }
            sb.Append(@"from
                        [order] o
                        left join accountextend a on o.accountId = a.id
                        left join dbo.paymenttype pay on o.paymenttypeid = pay.id
                        left join address on address.id = o.addressid
                        left join dbo.Salesman s on s.id = a.AffiliatedSalesman
                        where o.ordertype = ");
            sb.Append(type.ToString());

            #endregion

            sb.Append(PrepareWhere(searchOrderContext));
            return ExeSqlReturnDT(sb.ToString(), null);
        }
        public DataTable ExportOrderList(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo)
        {
            var sb = new StringBuilder();
            int type = Convert.ToInt32(searchOrderContext.OrderStyle);
            string customerFileds = ",'用户名','收货人手机号','收货地址','通讯邮箱'";

            #region 拼接字符串

            sb.Append(@"
                        select top 1
                        '订单号',
                        '支付号',
                        '订单状态',
                        '现金',
                        '支付方式',
                        '中民券',
                        '中民红酒券',
                        '红酒券',
                        '代金券',
                        '满减优惠',
                        '总价',
                        '发票',
                        '备注',
                        '订单生成时间',
                        '姓名',
                        '业务员','收货人姓名' ".Replace("\r\n", " "));
            if (excelContainCustomerInfo)
            {
                sb.Append(customerFileds);
            }
            sb.Append(" from [order] union all select ");

            sb.Append(@"
                        o.SerialNumber as '订单号',
                        o.PayNumber,
                        case
                        when o.State=1 then '未付款'
                        when o.State=2 then '已失效'
                        when o.State=3 then '已付款'
                        when o.State=4 then '已发货'
                        when o.State=5 then '已完成'
                        when o.State=6 then '已取消'
                        when o.State=7 then '已退单'
                        when o.State=10 then '待确认'
                        when o.State=11 then '付款未完结'
                        else 'Error'
                        end as '订单状态',
                        CONVERT(VARCHAR(100),o.FactPrice) as '现金',
                        case o.factprice when 0 then '' else pay.Name end as '支付方式', 
                        CONVERT(VARCHAR(100),o.ZMCoupon) as '中民券',
                        CONVERT(VARCHAR(100),o.WineCoupon) as '中民红酒券',
                        CONVERT(VARCHAR(100),o.WineWorldCoupon) as '红酒券',
                        CONVERT(VARCHAR(100),o.ProductCoupon) as '代金券',
                        CONVERT(VARCHAR(100),o.FullFreePrice) as '满减优惠',
                        CONVERT(VARCHAR(100),o.FactPrice+o.FullFreePrice+ o.ZMIntegralValue+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon+ (o.IntegralValue/100)) as '总价',
                        case
                        when o.IsNeedInvoice=0 then ''
                        else
                            case
                            when o.FactPrice!=0 then '需要'
                            else ''
                            end
                        end as '发票',
                        o.adminremark as  '备注',
                        CONVERT(VARCHAR(100),o.OrderGenerateDate,25) as '订单生成时间',a.name,s.name,o.CustomerName ");
            if (excelContainCustomerInfo)
            {
                sb.Append(" ,a.username,o.MobilePhone,o.AddressDetail,a.ReceiveEmail ");
            }
            sb.Append(@"from
                        [order] o
                        left join accountextend a on o.accountId = a.id
                        left join dbo.paymenttype pay on o.paymenttypeid = pay.id
                        left join address on address.id = o.addressid
                        left join dbo.Salesman s on s.id = a.AffiliatedSalesman
                        where o.ordertype = ");
            sb.Append(type.ToString());

            #endregion

            sb.Append(PrepareWhere(searchOrderContext));
            return ExeSqlReturnDT(sb.ToString(), null);
        }
        public DataTable ExportRebateOrderList(SearchOrderContext searchOrderContext)
        {
            var sb = new StringBuilder();
            int type = Convert.ToInt32(searchOrderContext.OrderStyle);

            #region 拼接查询字符串

            sb.Append(@"
                        if(exists(select name from tempdb..sysobjects where name like'%tempOrder%' and type='U'))
						drop table #tempOrder
						select
						o.Id,
                        o.SerialNumber,
                        o.PayNumber,
                        o.State,
                        o.FactPrice,                       
						pay.Name as payName, 
						o.FullFreePrice,
						o.FactPrice+o.FullFreePrice+ o.ZMIntegralValue+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon+ (o.IntegralValue/100) as totalPrice,              						
						op.UnitPrice * op.Quantity * re.CommissionRate as totalRebate,
                        o.adminremark,
                        o.OrderGenerateDate,
						a.Name as customer,
						s.name as salesman
						into #tempOrder
						from [Order] o
						left join OrderProduct op on o.Id = op.OrderId
						left join Product p on op.ProductId = p.Id
						left join Product_ProductClassify_Mapping pc on pc.ProductId = p.Id
						left join ProductClassify c on c.Id = pc.ClassifyId 
                        left join RebateWebSiteRate re on c.Id = re.ClassifyId ".Replace("\r\n", " "));

            sb.AppendFormat("and re.RebateWebSiteId = {0}", searchOrderContext.RebateWebSiteId);

            if (searchOrderContext.OrderStyle == OrderStyle.Usual)
            {
                sb.AppendFormat(" and re.SellChannel ={0}", (int)OrderStyle.Usual);
            }
            else if (searchOrderContext.OrderStyle == OrderStyle.CBP)
            {
                sb.AppendFormat(" and re.SellChannel ={0}", (int)OrderStyle.CBP);
            }
            else if (searchOrderContext.OrderStyle == OrderStyle.Expect)
            {
                sb.AppendFormat(" and re.SellChannel ={0}", (int)OrderStyle.Expect);
            }

            sb.Append(@"                        
						left join accountextend a on o.accountId = a.id
                        left join dbo.paymenttype pay on o.paymenttypeid = pay.id
                        left join address on address.id = o.addressid
						left join dbo.Salesman s on s.id = a.AffiliatedSalesman
                        where o.ordertype = ".Replace("\r\n", " ") + type.ToString());

            sb.Append(PrepareWhere(searchOrderContext));


            sb.Append(@"
                        select top 1 '订单号', '支付号','订单状态','现金','支付方式',
						'满减优惠','总价','该订单总返利金额','备注', '订单生成时间','姓名', '业务员' 
						from [#tempOrder] union all select
						SerialNumber AS '订单号',
						PayNumber AS '支付号',
						 case
                        when [State]=1 then '未付款'
                        when [State]=2 then '已失效'
                        when [State]=3 then '已付款'
                        when [State]=4 then '已发货'
                        when [State]=5 then '已完成'
                        when [State]=6 then '已取消'
                        when [State]=7 then '已退单'
                        when [State]=10 then '待确认'
                        when [State]=11 then '付款未完结'
                        else 'Error'
                        end as '订单状态',
                        CONVERT(VARCHAR(100),FactPrice) AS '现金',                       
						payName AS '支付方式', 
						CONVERT(VARCHAR(100),FullFreePrice) AS '满减优惠',
						CONVERT(VARCHAR(100),totalPrice) AS '总价',
						CONVERT(VARCHAR(100),SUM(totalRebate)) AS '该订单总返利金额',
						adminremark AS '备注',
                        CONVERT(VARCHAR(100),OrderGenerateDate,25 )AS '订单生成时间',
						customer AS '姓名',
						salesman AS '业务员' 
						from #tempOrder
						GROUP BY Id,SerialNumber,PayNumber,[State],
                        FactPrice,payName,FullFreePrice,totalPrice,
						adminremark,OrderGenerateDate,customer,
						salesman
						Drop table #tempOrder ".Replace("\r\n", " "));
            #endregion

            return ExeSqlReturnDT(sb.ToString(), null);
        }
        public DataTable SearchRebateOrderList(SearchOrderContext searchOrderContext)
        {
            var sb = new StringBuilder();
            int type = Convert.ToInt32(searchOrderContext.OrderStyle);

            #region 拼接查询字符串

            sb.Append(@"
                        if(exists(select name from tempdb..sysobjects where name like'%tempOrder%' and type='U'))
						drop table #tempOrder
						select
						o.Id,
                        o.SerialNumber,
                        o.PayNumber,
                        o.State,
                        o.FactPrice,                       
						pay.Name as payName, 
						o.FullFreePrice,
						o.FactPrice+o.FullFreePrice+ o.ZMIntegralValue+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon+ (o.IntegralValue/100) as totalPrice,              						
						op.UnitPrice * op.Quantity * re.CommissionRate as totalRebate,
                        o.adminremark,
                        o.OrderGenerateDate,
						a.Name as customer,
						s.name as salesman
						into #tempOrder
						from [Order] o
						left join OrderProduct op on o.Id = op.OrderId
						left join Product p on op.ProductId = p.Id
						left join Product_ProductClassify_Mapping pc on pc.ProductId = p.Id
						left join ProductClassify c on c.Id = pc.ClassifyId 
						left join RebateWebSiteRate re on c.Id = re.ClassifyId
						left join accountextend a on o.accountId = a.id
                        left join dbo.paymenttype pay on o.paymenttypeid = pay.id
                        left join address on address.id = o.addressid
						left join dbo.Salesman s on s.id = a.AffiliatedSalesman
                        where o.ordertype = ".Replace("\r\n", " ") + type.ToString());

            sb.Append(PrepareWhere(searchOrderContext));

            sb.Append(@"
                        select
						SerialNumber ,
						PayNumber,
						[State],
                        FactPrice,                       
						payName, 
						FullFreePrice,
						totalPrice,
						SUM(totalRebate) as totalRebate,
						adminremark,
                        OrderGenerateDate ,
						customer ,
						salesman  
						from #tempOrder
						GROUP BY Id,SerialNumber,PayNumber,[State],
                        FactPrice,payName,FullFreePrice,totalPrice,
						adminremark,OrderGenerateDate,customer,
						salesman
						Drop table #tempOrder ".Replace("\r\n", " "));
            #endregion

            return ExeSqlReturnDT(sb.ToString(), null);
        }
        public DataTable ExportRebateOrderDetail(SearchOrderContext searchOrderContext)
        {
            var sb = new StringBuilder();
            int type = Convert.ToInt32(searchOrderContext.OrderStyle);
            #region 拼接字符串
            sb.Append(@" select top 1
                        '订单号','支付号','酒业订单号','交易流水号',
                        '订单状态','现金','支付方式',				
                        '中民积分（新）',							
						'满减优惠','返利金额','总价','商品',
                        '单价', '数量','类型', '发票','开票类型',
                        '开票抬头','备注','订单生成时间'  ".Replace("\r\n", " "));

            sb.Append(@" from [order]  union all  select ");
            sb.Append(@"
                        CONVERT(VARCHAR(100),o.SerialNumber) as '订单号',
                        o.PayNumber,
                        ''''+CONVERT(VARCHAR(100),o.JiuYeOrderId) as '酒业订单号',
                        ''''+CONVERT(VARCHAR(100),o.TradeNO) as '交易流水号',
                        case
                        when o.State=1 then '未付款'
                        when o.State=2 then '已失效'
                        when o.State=3 then '已付款'
                        when o.State=4 then '已发货'
                        when o.State=5 then '已完成'
                        when o.State=6 then '已取消'
                        when o.State=7 then '已退单'
                        when o.State=10 then '待确认'
                        when o.State=11 then '付款未完结'
                        else ''
                        end as '订单状态',
                        CONVERT(VARCHAR(100),o.FactPrice) as '现金',
                        case o.factprice when 0 then '' else pay.Name end as '支付方式',
                        CONVERT(VARCHAR(100),o.IntegralValue) as '中民积分（新）',
                        CONVERT(VARCHAR(100),o.FullFreePrice) as '满减优惠',
						CONVERT(VARCHAR(100),orderproduct.UnitPrice * orderproduct.Quantity * re.CommissionRate) as '返利金额',
                        CONVERT(VARCHAR(100),o.FactPrice+o.FullFreePrice+o.ZMIntegralValue+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon+(o.IntegralValue/100)) as '总价',
                        p.Name as '商品',
                        CONVERT(VARCHAR(100),orderproduct.UnitPrice) as '单价',
                        CONVERT(VARCHAR(100),orderproduct.Quantity) as '数量', 
                        c.ClassifyName AS '类型',
                        case
                        when o.IsNeedInvoice=0 then ''
                        else
                            case
                            when o.FactPrice!=0 then '需要'
                            else ''
                            end
                        end as '发票',
                        CONVERT(VARCHAR(100),orderinvoice.InvoiceTitleType) as '开票类型',
                        case
                        when orderinvoice.InvoiceTitleType=1 then address.ReceiptName
                        when orderinvoice.InvoiceTitleType=2 then orderinvoice.InvoiceTitleInfo
                        else ''
                        end as '开票抬头',                      
                        o.adminremark as  '备注',
                        CONVERT(VARCHAR(100),o.OrderGenerateDate,25) as '订单生成时间'  ".Replace("\r\n", " "));

            sb.Append(@" from
                        [order] o
                        left join accountextend a on o.accountId=a.id
                        left join address on address.id=o.addressid                        
                        left join orderproduct on orderproduct.orderid=o.id
                        left join dbo.paymenttype pay on o.paymenttypeid=pay.id
                        left join product p on p.id=orderproduct.productid
                        left join orderinvoice on orderinvoice.id=o.id
                        left join dbo.Salesman s on s.id=a.AffiliatedSalesman
						left join Product_ProductClassify_Mapping pc on pc.ProductId = p.Id
						left join ProductClassify c on c.Id = pc.ClassifyId
						left join RebateWebSiteRate re on c.Id = re.ClassifyId ".Replace("\r\n", " "));


            sb.AppendFormat("and re.RebateWebSiteId = {0}", searchOrderContext.RebateWebSiteId);

            if (searchOrderContext.OrderStyle == OrderStyle.Usual)
            {
                sb.AppendFormat(" and re.SellChannel ={0}", (int)OrderStyle.Usual);
            }
            else if (searchOrderContext.OrderStyle == OrderStyle.CBP)
            {
                sb.AppendFormat(" and re.SellChannel ={0}", (int)OrderStyle.CBP);
            }
            else if (searchOrderContext.OrderStyle == OrderStyle.Expect)
            {
                sb.AppendFormat(" and re.SellChannel ={0}", (int)OrderStyle.Expect);
            }

            sb.Append(@"where o.ordertype = ".Replace("\r\n", " ") + type.ToString());

            sb.Append(PrepareWhere(searchOrderContext));
            #endregion


            return ExeSqlReturnDT(sb.ToString(), null);
        }
        public DataTable ExportResultDetail(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo, bool show_tc, bool isExpect)
        {
            var sb = new StringBuilder();
            int type = Convert.ToInt32(searchOrderContext.OrderStyle);
            string customerFileds = "'用户名','收货人手机号','收货地址','通讯邮箱',";
            #region 拼接字符串
            sb.Append(@"
                        select top 1
                        '订单号',
                        '支付号',
                        '酒业订单号',
                        '交易流水号',
                        '订单状态',
                        '现金',
                        '支付方式',
                        '中民积分',
                        '中民积分（新）',
                        '中民券',
                        '中民红酒券',
                        '红酒券',
                        '代金券',
                        '满减优惠',
                        '总价',
                        '商品',
                        '原价',
                        'VIP折后价',
                        '单价',
                        '数量',
                        '类型',                         
					    '发票', 
                        '开票类型',
                        '开票抬头',
                        '备注',
                        '订单生成时间', 
                        '姓名','收货人姓名', ".Replace("\r\n", " "));
            if (excelContainCustomerInfo)
            {
                sb.Append(customerFileds);
            }
            sb.Append(@"'业务员'");
            if (show_tc)
            {
                sb.Append(",'折扣比率','均摊实付金额' ,'提成','提成金额'");
            }
            sb.Append(" from [order]  union all  select ");

            sb.Append(@"
                        CONVERT(VARCHAR(100),o.SerialNumber) as '订单号',
                        o.PayNumber,
                        ''''+CONVERT(VARCHAR(100),o.JiuYeOrderId) as '酒业订单号',
                        ''''+CONVERT(VARCHAR(100),o.TradeNO) as '交易流水号',
                        case
                        when o.State=1 then '未付款'
                        when o.State=2 then '已失效'
                        when o.State=3 then '已付款'
                        when o.State=4 then '已发货'
                        when o.State=5 then '已完成'
                        when o.State=6 then '已取消'
                        when o.State=7 then '已退单'
                        when o.State=10 then '待确认'
                        when o.State=11 then '付款未完结'
                        else ''
                        end as '订单状态',
                        CONVERT(VARCHAR(100),o.FactPrice) as '现金',
                        case o.factprice when 0 then '' else pay.Name end as '支付方式',
                        CONVERT(VARCHAR(100),o.ZMIntegralValue) as '中民积分',
                        CONVERT(VARCHAR(100),o.IntegralValue) as '中民积分（新）',
                        CONVERT(VARCHAR(100),o.ZMCoupon) as '中民券',
                        CONVERT(VARCHAR(100),o.WineCoupon) as '中民红酒券',
                        CONVERT(VARCHAR(100),o.WineWorldCoupon) as '红酒券',
                        CONVERT(VARCHAR(100),o.ProductCoupon) as '代金券',
                        CONVERT(VARCHAR(100),o.FullFreePrice) as '满减优惠',
                        CONVERT(VARCHAR(100),o.FactPrice+o.FullFreePrice+o.ZMIntegralValue+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon+(o.IntegralValue/100)) as '总价',
                        p.Name as '商品',
                        CONVERT(VARCHAR(100),orderproduct.OriginalUnitPrice) as '原价',
                        CONVERT(VARCHAR(100),orderproduct.MemberUnitPrice) as 'VIP折后价',
                        CONVERT(VARCHAR(100),orderproduct.UnitPrice) as '单价',
                        CONVERT(VARCHAR(100),orderproduct.Quantity) as '数量',                           
						c.ClassifyName as '类型', 
                        case
                        when o.IsNeedInvoice=0 then ''
                        else
                            case
                            when o.FactPrice!=0 then '需要'
                            else ''
                            end
                        end as '发票',
                        CONVERT(VARCHAR(100),orderinvoice.InvoiceTitleType) as '开票类型',
                        case
                        when orderinvoice.InvoiceTitleType=1 then address.ReceiptName
                        when orderinvoice.InvoiceTitleType=2 then orderinvoice.InvoiceTitleInfo
                        else ''
                        end as '开票抬头',                      
                        o.adminremark as  '备注',
                        CONVERT(VARCHAR(100),o.OrderGenerateDate,25) as '订单生成时间',a.name,o.CustomerName,".Replace("\r\n", " "));
            if (excelContainCustomerInfo)
            {
                sb.Append("a.username,o.MobilePhone,o.AddressDetail,a.ReceiveEmail,");
            }

            sb.Append(@" case when s.name is null then '运营中心' else s.name end as name");
            if (show_tc)
            {
                if (!isExpect)
                {

                    sb.Append(@",case when orderproduct.OriginalUnitPrice=0 then '0' else CONVERT(VARCHAR(100),(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice))) end");
                    sb.Append(@",case when CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))=0 then '0' else CONVERT(VARCHAR(100), orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))) end ");
                    sb.Append(@",case when s.name is null 
                            then case when c.ClassifyName='A' or c.ClassifyName='C' then '2%'
                                      when c.ClassifyName='B' then '4%' else '0%' end
                            else  case when c.ClassifyName='A' or c.ClassifyName='C' then '4%'
                                      when c.ClassifyName='B' then '8%' else '0%' end  end");
                    sb.Append(@",case when orderproduct.UnitIntegrationValue=0  and CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))<>0
                        THEN Case when s.name is null 
                               then case 
                                    when c.ClassifyName='A' or c.ClassifyName='B' AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.95 
                                    THEN  CASE 
                                           when c.ClassifyName='A' THEN CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.02)
                                           when c.ClassifyName='B' THEN CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.04)
                                            ELSE '0' END
                                    when c.ClassifyName='C' AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.5
                                    THEN  CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.02)
                                    ELSE '0' END
                                else
                                    case 
                                    when (c.ClassifyName='A' or c.ClassifyName='B') AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.95
                                    THEN  CASE 
                                           when c.ClassifyName='A' THEN CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.04)
                                           when c.ClassifyName='B' THEN CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.08)
                                           ELSE '0' END
                                    when (c.ClassifyName='C') AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.5
                                    THEN  CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.04)
                                    ELSE '0' END
                                  END 
                            ELSE '0' END  ");
                }
                else
                {
                    sb.Append(@",case when orderproduct.OriginalUnitPrice=0 then '0' else CONVERT(VARCHAR(100),(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice))) end");
                    sb.Append(@",case when CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))=0 then '0' else CONVERT(VARCHAR(100), orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))) end ");

                    sb.Append(@",case when s.name is null 
                            then '0.5%'
                            else  '2%' end");
                    sb.Append(@",case when orderproduct.UnitIntegrationValue=0 and CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))<>0
                        THEN Case when s.name is null 
                               then case 
                                    when c.ClassifyName='A' or c.ClassifyName='B' AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.95 
                                    THEN  CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.005)
                                    when c.ClassifyName='C' AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.5
                                    THEN  CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.005)
                                    ELSE '0' END
                                else
                                    case 
                                    when (c.ClassifyName='A' or c.ClassifyName='B') AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.95
                                    THEN  CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.02)
                                    when (c.ClassifyName='C') AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.5
                                    THEN  CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.02)
                                    ELSE '0' END
                                  END 
                            ELSE '0' END  ");
                }
            }
            sb.Append(@" from
                        [order] o                        
                        left join accountextend a on o.accountId=a.id
                        left join address on address.id=o.addressid                        
                        left join orderproduct on orderproduct.orderid=o.id
                        left join dbo.paymenttype pay on o.paymenttypeid=pay.id
                        left join product p on p.id=orderproduct.productid
                        left join Product_ProductClassify_Mapping pc on pc.ProductId = p.Id
					  left join ProductClassify c on c.Id = pc.ClassifyId
                        left join orderinvoice on orderinvoice.id=o.id
                        left join dbo.Salesman s on s.id=a.AffiliatedSalesman
                        where o.ordertype=".Replace("\r\n", " ") + type.ToString());

            #endregion
            sb.Append(PrepareWhere(searchOrderContext));
            var result = ExeSqlReturnDT(sb.ToString(), null);
            return result;
        }

        public DataTable ExportCBPResult(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo)
        {
            var sb = new StringBuilder();
            int type = Convert.ToInt32(searchOrderContext.OrderStyle);
            string customerFileds = ",'用户名','收货人手机号','收货地址','通讯邮箱','身份证姓名','身份证号码'";
            #region 拼接字符串
            sb.Append(@"
                        select top 1
                        '订单号',
                        '支付号',
                        '订单状态',
                        '现金',
                        '支付方式',
                        '中民积分',
                        '中民积分（新_总）',
                        '中民积分（新）来源（中民积分宝）',
                        '中民积分（新）来源（中民保险网）',
                        '中民积分（新）来源（红酒世界网）',
                        '中民券',
                        '中民红酒券',
                        '红酒券',
                        '代金券',
                        '满减优惠',
                        '总价',
                        '发票',
                        '备注',
                        '订单生成时间', 
                        '姓名',
                        '业务员','收货人姓名'".Replace("\r\n", " "));

            if (excelContainCustomerInfo)
            {
                sb.Append(customerFileds);
            }
            sb.Append(" from [order] union all ");

            sb.Append(" select ");

            sb.Append(@"
                        o.SerialNumber as '订单号',
                        o.PayNumber,
                        case
                        when o.State=1 then '未付款'
                        when o.State=2 then '已失效'
                        when o.State=3 then '已付款'
                        when o.State=4 then '已发货'
                        when o.State=5 then '已完成'
                        when o.State=6 then '已取消'
                        when o.State=7 then '已退单'
                        when o.State=10 then '待确认'
                        when o.State=11 then '付款未完结'
                        else 'Error'
                        end as '订单状态',
                        CONVERT(VARCHAR(100),o.FactPrice) as '现金',
                        case o.factprice when 0 then '' else pay.Name end as '支付方式', 
                        CONVERT(VARCHAR(100),o.ZMIntegralValue) as '中民积分', 
                        CONVERT(VARCHAR(100),o.IntegralValue) as '中民积分（新）',
(select top 1 CONVERT(nVARCHAR(100),value) from dbo.ordermoneysource where orderid=o.id and [key]='中民积分' and [source]=1),
(select top 1 CONVERT(nVARCHAR(100),value) from dbo.ordermoneysource where orderid=o.id and [key]='中民积分' and [source]=2),
(select top 1 CONVERT(nVARCHAR(100),value) from dbo.ordermoneysource where orderid=o.id and [key]='中民积分' and [source]=3),                 
                        CONVERT(VARCHAR(100),o.ZMCoupon) as '中民券',
                        CONVERT(VARCHAR(100),o.WineCoupon) as '中民红酒券',
                        CONVERT(VARCHAR(100),o.WineWorldCoupon) as '红酒券',
                        CONVERT(VARCHAR(100),o.ProductCoupon) as '代金券',
                        CONVERT(VARCHAR(100),o.FullFreePrice) as '满减优惠',
                        CONVERT(VARCHAR(100),o.FactPrice+o.FullFreePrice+ o.ZMIntegralValue+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon +(o.IntegralValue/100)) as '总价',
                        case
                        when o.IsNeedInvoice=0 then ''
                        else
                            case
                            when o.FactPrice!=0 then '需要'
                            else ''
                            end
                        end as '发票',
                        o.adminremark as  '备注',
                        CONVERT(VARCHAR(100),o.OrderGenerateDate,25) as '订单生成时间',
a.name,s.name,o.CustomerName");
            if (excelContainCustomerInfo)
            {
                sb.Append(@",a.username,o.MobilePhone,o.AddressDetail,a.ReceiveEmail,cbpo.PayerName,cbpo.CardNumber ");

            }
            sb.Append(@" from
                        [order] o
                        left join accountextend a on o.accountId=a.id
                        left join dbo.paymenttype pay on o.paymenttypeid=pay.id
                        left join address on address.id=o.addressid
                        left join dbo.Salesman s on s.id=a.AffiliatedSalesman
                        LEFT JOIN dbo.CBPOrderInfo cbpo ON cbpo.OrderId = o.Id
                        where o.ordertype=".Replace("\r\n", " ") + type.ToString());
            #endregion

            sb.Append(PrepareWhere(searchOrderContext));
            return ExeSqlReturnDT(sb.ToString(), null);
        }

        public DataTable ExportCBPResultDetail(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo, bool show_tc)
        {
            var sb = new StringBuilder();
            int type = Convert.ToInt32(searchOrderContext.OrderStyle);
            #region 拼接字符串
            sb.Append(@"
                        select top 1
                        '订单号',
                        '支付号',
                        '酒业订单号',
                        '交易流水号',
                        '订单状态',
                        '现金',
                        '支付方式',
                        '中民积分',
                        '中民积分（新）',
                        '中民券',
                        '中民红酒券',
                        '红酒券',
                        '代金券',
                        '满减优惠',
                        '总价',
                        '商品',
                        '原价',
                        'VIP折后价',
                        '单价',
                        '数量',
                        '类型',
                        '发票',
                        '开票类型',
                        '开票抬头',
                        '备注',
                        '订单生成时间', 
                        '产品海关备案码',
                        '姓名' ".Replace("\r\n", " "));
            if (excelContainCustomerInfo)
            {
                sb.Append(@",'用户名',
                        '收货人姓名',
                        '收货人手机号',
                        '收货地址',
                        '通讯邮箱',
                        '身份证姓名',
					  '身份证号码' ".Replace("\r\n", " "));
            }
            sb.Append(@",'业务员'");
            if (show_tc)
            {
                sb.Append(" ,'折扣比率','均摊实付金额','提成','提成金额'");
            }
            sb.Append(" from [order] union all select");

            sb.Append(@"
                        CONVERT(VARCHAR(100),o.SerialNumber) as '订单号',
                        o.PayNumber,
                        ''''+CONVERT(VARCHAR(100),o.JiuYeOrderId) as '酒业订单号',
                        ''''+CONVERT(VARCHAR(100),o.TradeNO) as '交易流水号',
                        case
                        when o.State=1 then '未付款'
                        when o.State=2 then '已失效'
                        when o.State=3 then '已付款'
                        when o.State=4 then '已发货'
                        when o.State=5 then '已完成'
                        when o.State=6 then '已取消'
                        when o.State=7 then '已退单'
                        when o.State=10 then '待确认'
                        when o.State=11 then '付款未完结'
                        else ''
                        end as '订单状态',
                        CONVERT(VARCHAR(100),o.FactPrice) as '现金',
                        case o.factprice when 0 then '' else pay.Name end as '支付方式',
                        CONVERT(VARCHAR(100),o.ZMIntegralValue) as '中民积分',
                        CONVERT(VARCHAR(100),o.IntegralValue) as '中民积分（新）',
                        CONVERT(VARCHAR(100),o.ZMCoupon) as '中民券',
                        CONVERT(VARCHAR(100),o.WineCoupon) as '中民红酒券',
                        CONVERT(VARCHAR(100),o.WineWorldCoupon) as '红酒券',
                        CONVERT(VARCHAR(100),o.ProductCoupon) as '代金券',
                        CONVERT(VARCHAR(100),o.FullFreePrice) as '满减优惠',
                        CONVERT(VARCHAR(100),o.FactPrice+o.FullFreePrice+o.ZMIntegralValue+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon+(o.IntegralValue/100)) as '总价',
                        p.Name as '商品',
                        CONVERT(VARCHAR(100),orderproduct.OriginalUnitPrice) as '原价',
                        CONVERT(VARCHAR(100),orderproduct.MemberUnitPrice) as 'VIP折后价',
                        CONVERT(VARCHAR(100),orderproduct.UnitPrice) as '单价',
                        CONVERT(VARCHAR(100),orderproduct.Quantity) as '数量',                     
				      c.ClassifyName as '类型', 
                        case
                        when o.IsNeedInvoice=0 then ''
                        else
                            case
                            when o.FactPrice!=0 then '需要'
                            else ''
                            end
                        end as '发票',
                        CONVERT(VARCHAR(100),orderinvoice.InvoiceTitleType) as '开票类型',
                        case
                        when orderinvoice.InvoiceTitleType=1 then address.ReceiptName
                        when orderinvoice.InvoiceTitleType=2 then orderinvoice.InvoiceTitleInfo
                        else ''
                        end as '开票抬头',                      
                        o.adminremark as  '备注',
                        CONVERT(VARCHAR(100),o.OrderGenerateDate,25) as '订单生成时间',
cbp.CustomsProductCode,a.name");
            if (excelContainCustomerInfo)
            {
                sb.Append(",a.username,o.CustomerName,o.MobilePhone,o.AddressDetail,a.ReceiveEmail,cbpo.PayerName,cbpo.CardNumber ");
            }
            sb.Append(@",case when s.name is null then '运营中心' else s.name end as name");
            if (show_tc)
            {
                sb.Append(@",case when orderproduct.OriginalUnitPrice=0 then '0' else CONVERT(VARCHAR(100),(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice))) end");
                sb.Append(@",case when CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))=0 then '0' else CONVERT(VARCHAR(100), orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))) end ");

                sb.Append(@",case when s.name is null 
                            then case when c.ClassifyName='A' or c.ClassifyName='C' then '2%'
                                      when c.ClassifyName='B' then '4%' else '0%' end
                            else  case when c.ClassifyName='A' or c.ClassifyName='C' then '4%'
                                      when c.ClassifyName='B' then '8%' else '0%' end  end");
                sb.Append(@",case when orderproduct.UnitIntegrationValue=0 and  CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))<>0
                        THEN Case when s.name is null 
                               then case 
                                    when c.ClassifyName='A' or c.ClassifyName='B' AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.95 
                                    THEN  CASE 
                                           when c.ClassifyName='A' THEN CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.02)
                                           when c.ClassifyName='B' THEN CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.04)
                                            ELSE '0' END
                                    when c.ClassifyName='C' AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.5
                                    THEN  CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.02)
                                    ELSE '0' END
                                else
                                    case 
                                    when (c.ClassifyName='A' or c.ClassifyName='B') AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.95
                                    THEN  CASE 
                                           when c.ClassifyName='A' THEN CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.04)
                                           when c.ClassifyName='B' THEN CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.08)
                                           ELSE '0' END
                                    when (c.ClassifyName='C') AND  CONVERT(float,(CONVERT(float, orderproduct.UnitPrice)/CONVERT(float, orderproduct.OriginalUnitPrice)))>=0.5
                                    THEN  CONVERT(VARCHAR(100), CONVERT(float, (orderproduct.UnitPrice*orderproduct.Quantity-(o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon)*(orderproduct.UnitPrice*orderproduct.Quantity/(CONVERT(float,(o.FactPrice+o.FullFreePrice+o.ZMCoupon+WineCoupon+o.WineWorldCoupon+o.ProductCoupon))))))*0.04)
                                    ELSE '0' END
                                  END 
                            ELSE '0' END  ");
            }
            sb.Append(@" from [order] o
                        left join accountextend a on o.accountId=a.id
                        left join address on address.id=o.addressid                        
                        left join orderproduct on orderproduct.orderid=o.id
                        left join dbo.paymenttype pay on o.paymenttypeid=pay.id
                        left join product p on p.id=orderproduct.productid
                        left join Product_ProductClassify_Mapping pc on pc.ProductId = p.Id
					  left join ProductClassify c on c.Id = pc.ClassifyId
                        left join orderinvoice on orderinvoice.id=o.id
                        left join dbo.Salesman s on s.id=a.AffiliatedSalesman
                        LEFT JOIN dbo.CBPProduct cbp ON cbp.ProductId = orderproduct.productid
						LEFT JOIN dbo.CBPOrderInfo cbpo ON cbpo.OrderId = o.Id
                        where o.ordertype=".Replace("\r\n", " ") + type.ToString());

            #endregion
            sb.Append(PrepareWhere(searchOrderContext));

            return ExeSqlReturnDT(sb.ToString(), null);
        }

        private string PrepareWhere(SearchOrderContext searchOrderContext)
        {
            var sb = new StringBuilder();
            if (searchOrderContext.OrderId != null)
            {
                if (searchOrderContext.OrderId.Trim() != "")
                {
                    sb.AppendFormat(" and o.id in({0})", searchOrderContext.OrderId);
                }
                else
                {
                    sb.AppendFormat(" and o.id in(-1)", searchOrderContext.OrderId);
                    return sb.ToString();
                }
            }
            //开始时间
            if (searchOrderContext.StartTime.HasValue)
            {
                sb.AppendFormat(" and o.OrderGenerateDate>='{0}' ", searchOrderContext.StartTime.Value);
            }
            //结束时间
            if (searchOrderContext.EndTime.HasValue)
            {
                sb.AppendFormat(" and o.OrderGenerateDate<='{0}' ", searchOrderContext.EndTime.Value);
            }
            //账号
            if (!string.IsNullOrEmpty(searchOrderContext.AccountName))
            {
                sb.AppendFormat(" and a.UserName='{0}' ", searchOrderContext.AccountName);
            }
            //收货人
            if (!string.IsNullOrEmpty(searchOrderContext.RecipeName))
            {
                sb.AppendFormat(" and address.ReceiptName='{0}' ", searchOrderContext.RecipeName);
            }
            //订单状态

            #region 订单状态

            if (searchOrderContext.OrderAllState != OrderAllState.NoCondition)
            {
                switch (searchOrderContext.OrderAllState)
                {
                    case OrderAllState.NotPay:
                        sb.Append(" and o.State=1 ");
                        break;

                    case OrderAllState.Invalid:
                        sb.Append(" and o.State=2 ");
                        break;

                    case OrderAllState.Paid:
                        sb.Append(" and o.State=3 ");
                        break;

                    case OrderAllState.Shipped:
                        sb.Append(" and o.State=4 ");
                        break;

                    case OrderAllState.Complete:
                        sb.Append(" and o.State=5 ");
                        break;

                    case OrderAllState.Cancelled:
                        sb.Append(" and o.State=6 ");
                        break;

                    case OrderAllState.CancelOrder:
                        sb.Append(" and o.State=7 ");
                        break;
                    case OrderAllState.ValidOrder:
                        sb.Append(" and o.State in(3,4,5,11) ");
                        break;
                    case OrderAllState.PaidExceptionOrder:
                        sb.Append(" and o.State=9 ");
                        break;
                    case OrderAllState.ToConfirm:
                        sb.Append(" and o.State=10 ");
                        break;
                    case OrderAllState.PaidNotCompleted:
                        sb.Append(" and o.State=11 ");
                        break;

                }

            }

            #endregion 订单状态

            //订单号
            if (!string.IsNullOrEmpty(searchOrderContext.OrderNumber))
            {
                sb.AppendFormat(" and o.SerialNumber='{0}' ", searchOrderContext.OrderNumber);
            }
            //需要发票
            if (searchOrderContext.NeedInvoice)
            {
                sb.Append(" and o.IsNeedInvoice=1 and o.FactPrice>0 ");
            }
            //待下单
            if (searchOrderContext.IsTransferJiuYe)
            {
                sb.Append(" and o.State=3 and o.IsTransferJiuYe=0 ");
            }
            //中民积分实收
            if (searchOrderContext.PaymentAllStyles != PaymentAllStyles.NoCondition)
            {
                switch (searchOrderContext.PaymentAllStyles)
                {
                    case PaymentAllStyles.AllMoney:
                        sb.Append(@"
                                    and o.TotalPrice=o.FactPrice
                                    ".Replace("\r\n", " "));
                        break;

                    case PaymentAllStyles.AllIntegral:
                        sb.Append(@"
                                    and o.TotalPrice=o.ZMIntegralValue
                                    ".Replace("\r\n", " "));
                        break;

                    case PaymentAllStyles.AllCoupon:
                        sb.Append(@"
                                    and o.TotalPrice=o.ZMCoupon
                                    ".Replace("\r\n", " "));
                        break;

                    case PaymentAllStyles.PartIntegral:
                        sb.Append(@"
                                    and o.ZMIntegralValue>0
                                    ".Replace("\r\n", " "));
                        break;

                    case PaymentAllStyles.PartCoupon:
                        sb.Append(@"
                                    and o.ZMCoupon>0
                                    ".Replace("\r\n", " "));
                        break;

                    case PaymentAllStyles.PartMyProductCoupon:
                        sb.Append(@"
                                    and o.ProductCoupon>0
                                    ".Replace("\r\n", " "));
                        break;
                }
            }
            //角色，员工角色
            if (searchOrderContext.IsStaffOrders.HasValue)
            {
                if (searchOrderContext.IsStaffOrders.Value)
                {
                    sb.Append(string.Format(@"  and exists( SELECT 1  FROM  [dbo].[Account_Role_Mapping] AS [Extent7]
                                  INNER JOIN [dbo].[Role] AS [Extent8] ON [Extent7].[Role_Id] = [Extent8].[Id]
                                  WHERE (a.id = [Extent7].[AccountExtend_Id]) AND ([Extent8].[Code] = '{0}'))".Replace("\r\n", " "), SystemAccountRoleNames.Staff));
                }
                else
                {
                    sb.Append(string.Format(@"  and not exists( SELECT 1  FROM  [dbo].[Account_Role_Mapping] AS [Extent7]
                                  INNER JOIN [dbo].[Role] AS [Extent8] ON [Extent7].[Role_Id] = [Extent8].[Id]
                                  WHERE (a.id = [Extent7].[AccountExtend_Id]) AND ([Extent8].[Code] = '{0}'))".Replace("\r\n", " "), SystemAccountRoleNames.Staff));
                }
            }
            //返利
            if (searchOrderContext.RebateWebSiteId != 0 || searchOrderContext.PlatformCode != null)
            {
                if (searchOrderContext.PlatformCode == null)
                    searchOrderContext.PlatformCode = "";

                sb.AppendFormat(" and (o.RebateWebSiteId = {0} or o.PlatformCode = '{1}')", searchOrderContext.RebateWebSiteId, searchOrderContext.PlatformCode);
            }

            return sb.ToString();
        }
        /// <summary>
        /// 获取中民积分没有赠送成功的订单
        /// </summary>
        /// <returns></returns>
        public List<Order> GetOrdersByGetJiFen()
        {
            var query = _shopUnitOfWork.Get<Order>()
                .Where(t => t.Isvalid && !t.IsGetZMJiFen && t.GetIntegrationValue > 0);
            return query.Where(x => x.State == OrderState.Paid || x.State == OrderState.Shipped || x.State == OrderState.Complete || x.State == OrderState.PaidExceptionOrder || x.State == OrderState.PaidNotCompleted).ToList();
        }

        private DataTable ExeSqlReturnDT(string sql, SqlParameter[] parameters)
        {
            var connection = (SqlConnection)_shopUnitOfWork.Context.Database.Connection;
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

        public IList<Order> SearchOrdersWithNoPage(SearchOrderContext searchOrderContext)
        {
            if (searchOrderContext.PayNumber.IsNullOrEmpty())
            {
                return PrepareSearchUsualOrderQuery(searchOrderContext).Include(t => t.Address).Include(t => t.AccountExtend).Include(t => t.OrderProducts).ToList();
            }
            else
            {
                return PrepareSearchUsualOrderQuery(searchOrderContext).Include(t => t.AccountExtend).Include(t => t.OrderProducts).ToList();
            }
        }

        /// <summary>
        /// 获得未付款订单
        /// </summary>
        /// <returns></returns>
        public IList<Order> GetNotPaiedOrders()
        {
            var query = _shopUnitOfWork.Get<Order>()
                .Where(t => t.Isvalid);
            return query.Where(t => t.State == OrderState.NotPay).ToList();
        }

        /// <summary>
        /// 获取未付款订单
        /// </summary>
        /// <param name="accountId"></param>
        /// <returns></returns>
        public IList<Order> GetNeedPayOrders(int accountId)
        {
            return _shopUnitOfWork.Get<Order>()
                .Where(p => p.AccountId == accountId && p.Isvalid && p.State == OrderState.NotPay).OrderByDescending(t => t.OrderGenerateDate).ToList();
        }

        /// <summary>
        /// 根据订单状态 获取当前用户 待付款的订单数
        /// </summary>
        /// <param name="accountId"></param>
        /// <param name="orderState"></param>
        /// <returns></returns>
        public int GetOrderNumByOrderState(int accountId, OrderState orderState)
        {
            return _shopUnitOfWork.Get<Order>()
                .Where(p => p.AccountId == accountId && p.Isvalid && p.State == orderState).Select(p => p.Id).Count();
        }

        public IQueryable<Order> PrepareSearchUsualOrderQuery(SearchOrderContext searchOrderContext)
        {
            //var query = _shopUnitOfWork.Get<Order>()
            //    .Where(t => t.Isvalid);

            var query = _shopUnitOfWork.Get<Order>(); //TODO:李志杰，用于后台查看已删除订单


            if (searchOrderContext.PayNumber.HasValue())
            {
                try
                {
                    PayNumber paynumberModel = _shopUnitOfWork.Get<PayNumber>().Where(t => t.PayNo == searchOrderContext.PayNumber).FirstOrDefault();
                    List<int> ids = new List<int>();
                    var maps = _shopUnitOfWork.Get<Order_PayNumber_Mapping>()
                        .Where(t => t.PayNumberId == paynumberModel.Id)
                        .ToList();
                    foreach (Order_PayNumber_Mapping map in maps)
                    {
                        ids.Add(map.OrderId);
                    }
                    query = query.Where(t => ids.Contains(t.Id)).OrderByDescending(t => t.Id);
                    return query;
                    //_logger.Error(string.Format("订单总数:{0}", query.Count()));
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format("根据支付号搜索订单失败，支付号:{0},{1}", searchOrderContext.PayNumber, ex.Message));
                    return null;
                }
            }

            if (searchOrderContext.OrderStyle.HasValue)
            {
                query = query.Where(p => p.OrderType == (int)searchOrderContext.OrderStyle);

                if (searchOrderContext.OrderStyle == OrderStyle.Expect)
                {

                    int[] idList = { 3, 4, 5, 9, 11 };

                    var ids = from orderForm in _shopUnitOfWork.Get<OrderFormPrintStatus>()
                              join order in _shopUnitOfWork.Get<Order>() on orderForm.OrderId equals order.Id
                              where orderForm.Isvalid && order.Isvalid && orderForm.PrintState == PrintState.HadPrint && idList.Contains((int)order.State)
                              select order.Id;

                    var noIds = from order in _shopUnitOfWork.Get<Order>()
                                where order.Isvalid && idList.Contains((int)order.State) && !ids.Contains(order.Id)
                                select order.Id;
                    if (searchOrderContext.PrintState == PrintState.HadPrint)
                    {
                        query = query.Where(p => ids.Contains(p.Id));
                    }
                    else if (searchOrderContext.PrintState == PrintState.NoPrint)
                    {
                        query = query.Where(p => noIds.Contains(p.Id));
                    }
                }
            }
            // 没有传送至酒业订单
            if (searchOrderContext.IsTransferJiuYe)
            {
                query = query.Where(o => o.State == OrderState.Paid && !o.IsTransferJiuYe);
            }
            if (searchOrderContext.CustomerId != 0)
            {
                query = query.Where(o => o.AccountId == searchOrderContext.CustomerId);
            }
            if (!searchOrderContext.AccountName.IsNullOrEmpty())
            {
                //query = query.Where(o => o.AccountExtend.UserName.Contains(searchOrderContext.AccountName));
                query = query.Where(o => o.AccountExtend.UserName == searchOrderContext.AccountName);
            }
            if (searchOrderContext.StartTime.HasValue)
            {
                query = query.Where(o => o.OrderGenerateDate >= searchOrderContext.StartTime.Value);
            }
            if (searchOrderContext.EndTime.HasValue)
            {
                query = query.Where(o => o.OrderGenerateDate <= searchOrderContext.EndTime.Value);
            }
            if (searchOrderContext.OrderAllState != OrderAllState.NoCondition)
            {
                if (searchOrderContext.OrderAllState == OrderAllState.ValidOrder)
                {
                    query = query.Where(o => o.State == OrderState.Paid
                        || o.State == OrderState.Shipped
                        || o.State == OrderState.Complete || o.State == OrderState.PaidNotCompleted);
                }
                else
                {
                    switch (searchOrderContext.OrderAllState)
                    {
                        case OrderAllState.NotPay:
                            query = query.Where(o => o.State == OrderState.NotPay);
                            break;

                        case OrderAllState.Invalid:
                            query = query.Where(o => o.State == OrderState.Invalid);
                            break;

                        case OrderAllState.Paid:
                            query = query.Where(o => o.State == OrderState.Paid);
                            break;

                        case OrderAllState.Shipped:
                            query = query.Where(o => o.State == OrderState.Shipped);
                            break;

                        case OrderAllState.Complete:
                            query = query.Where(o => o.State == OrderState.Complete);
                            break;

                        case OrderAllState.Cancelled:
                            query = query.Where(o => o.State == OrderState.Cancelled);
                            break;

                        case OrderAllState.CancelOrder:
                            query = query.Where(o => o.State == OrderState.RevokeOrder);
                            break;

                        case OrderAllState.PaidExceptionOrder:
                            query = query.Where(o => o.State == OrderState.PaidExceptionOrder);
                            break;
                        case OrderAllState.ToConfirm:
                            query = query.Where(o => o.State == OrderState.ToConfirm);
                            break;
                        case OrderAllState.PaidNotCompleted:
                            query = query.Where(o => o.State == OrderState.PaidNotCompleted);
                            break;
                        case OrderAllState.PaidNotConfirm:
                            query = query.Where(o => o.State == OrderState.PaidNotConfirm);
                            break;
                        case OrderAllState.paidConfirmed:
                            query = query.Where(o => o.State == OrderState.paidConfirmed);
                            break;

                    }
                }
            }
            if (!searchOrderContext.RecipeName.IsNullOrEmpty())
            {
                //query = query.Where(o => o.Address.ReceiptName.Contains(searchOrderContext.RecipeName));
                query = query.Where(o => o.Address.ReceiptName == searchOrderContext.RecipeName || o.CustomerName == searchOrderContext.RecipeName);
            }
            if (!searchOrderContext.RecipeMobile.IsNullOrEmpty())
            {
                query = query.Where(o => o.Address.MobileNumber.Contains(searchOrderContext.RecipeMobile));
            }
            if (searchOrderContext.NeedInvoice)
            {
                query = query.Where(o => o.OrderInvoice != null && o.IsNeedInvoice == true && o.FactPrice != 0);
            }
            if (searchOrderContext.PaymentAllStyles != PaymentAllStyles.NoCondition)
            {
                switch (searchOrderContext.PaymentAllStyles)
                {
                    case PaymentAllStyles.AllMoney: query = query.Where(t => t.FactPrice == t.OrderProducts.Sum(p => p.Price)); break;
                    case PaymentAllStyles.AllIntegral: query = query.Where(t => t.ZMIntegralValue == t.OrderProducts.Sum(p => p.Price)); break;
                    case PaymentAllStyles.AllCoupon: query = query.Where(t => t.ZMCoupon == t.OrderProducts.Sum(p => p.Price)); break;
                    case PaymentAllStyles.PartIntegral: query = query.Where(t => t.OrderProducts.Sum(p => p.Price) > 0 && t.ZMIntegralValue > 0 && t.FactPrice > 0); break;
                    case PaymentAllStyles.PartCoupon: query = query.Where(t => t.OrderProducts.Sum(p => p.Price) > 0 && t.ZMCoupon > 0 && t.FactPrice > 0); break;
                    case PaymentAllStyles.PartMyProductCoupon: query = query.Where(t => t.ProductCoupon > 0); break;
                }
            }

            if (searchOrderContext.OrderNumber.HasValue())
            {
                //query = query.Where(o => o.SerialNumber.Contains(searchOrderContext.OrderNumber));
                query = query.Where(o => o.SerialNumber == searchOrderContext.OrderNumber);
            }
            //return query.Where(o => o.Isvalid).OrderByDescending(o => o.OrderGenerateDate);   
            return query.OrderByDescending(o => o.OrderGenerateDate);   //TODO:李志杰，用于后台查看已删除订单
        }

        /// <summary>
        /// Inserts an order
        /// </summary>
        /// <param name="order">Order</param>
        public virtual void InsertOrder(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            _shopUnitOfWork.Insert<Order>(order);
            _shopUnitOfWork.SaveChanges();
            //event notification
            _eventPublisher.EntityInserted(order);
        }

        #region 给购物车分组,拆单

        protected virtual List<OrderGroup> GroupCarts(IList<ShoppingCart> carts)
        {
            if (carts == null)
            {
                return new List<OrderGroup>();
            }
            else if (carts.Count <= 0)
            {
                return new List<OrderGroup>();
            }
            else
            {
                int otherKey = -10;
                var cartGroups = new List<OrderGroup>();

                //正常购物车
                var usualCarts = carts.Where(x => x.CartType == CartType.Usual || x.CartType == CartType.WineMenu);
                foreach (var ucart in usualCarts)
                {
                    GroupUsualCart(ucart, cartGroups, ref otherKey);
                }
                otherKey--;
                //预售购物车
                var presellCarts = carts.Where(x => x.CartType == CartType.PreSale).ToList();
                if (presellCarts.Count > 0)
                {
                    GroupPresellCart(presellCarts, cartGroups, ref otherKey);
                }


                //期酒购物车
                var expectCarts = carts.Where(x => x.CartType == CartType.Expect);
                foreach (var ecart in expectCarts)
                {
                    GroupExpectCart(ecart, cartGroups, ref otherKey);
                }

                //跨境电商购物车
                var cBPCarts = carts.Where(x => x.CartType == CartType.CrossBorder).ToList();
                if (cBPCarts.Count > 0)
                {
                    GroupCBPCart(cBPCarts, cartGroups, ref otherKey);
                }
                //众筹购物车
                var cFPCarts = carts.Where(x => x.CartType == CartType.CrowdFunding);
                foreach (var ccart in cFPCarts)
                {
                    GroupCFPCart(ccart, cartGroups, ref otherKey);
                }
                return cartGroups;
            }
        }

        protected virtual void GroupUsualCart(ShoppingCart cart, IList<OrderGroup> cartGroups, ref int otherKey)
        {
            var productUnitPrice = cart.Product.GetSalePrice();
            List<FareCartModel> fareCarts = null;


            EngineContext.Current.Resolve<IShoppingCartService>().CheckDiscountType(cart);
            var isPromotion = (cart.DiscountType == DiscountType.Promotion) && (_promotionsService.IsApartPromotions(cart.ProductId));//优惠类型是否为活动
            bool isStaff = _workContext.CurrentAccount.IsStaff();//是否是内部员工
            if (isPromotion)
            {
                // 获取商品的活动价
                var promotionPrice = _promotionsService.GetProductPromotionPrice(cart.Product.Id);
                if (promotionPrice != null)
                {
                    productUnitPrice = (decimal)promotionPrice;
                }

                fareCarts = _promotionsService.UpdateFareCarts(cart.Id);//加价购
            }
            else if ((_workContext.CurrentAccount.IsVipMember() || isStaff) && _promotionsService.IsApartMemberDiscount(cart.ProductId))
            {
                var isAgentProduct = _productService.IsAgentProduct(cart.Product.Id);//是否是独代酒款
                if (!isAgentProduct)
                {
                    productUnitPrice = GetVipPrice(productUnitPrice);
                }
            }
            if (cart.Product.IsCombination) //如果是组合装
            {
                int stockQuantity = GetCombinationProductStockQuantity(cart.Product.Id);  //得到库存

                if (stockQuantity > 0)
                {
                    var arrivedGroup = cartGroups.Where(t => t.Key == 0).FirstOrDefault();
                    if (arrivedGroup == null)
                    {
                        var og = new OrderGroup()
                        {
                            Key = 0,
                            Carts = new List<ShoppingCart>(),
                            Price = productUnitPrice * cart.Quantity + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum)),
                            IntegrationValue = _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity
                        };
                        og.Carts.Add(cart);
                        cartGroups.Add(og);
                    }
                    else
                    {
                        arrivedGroup.Carts.Add(cart);
                        arrivedGroup.Price += (productUnitPrice * cart.Quantity) + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum));
                        arrivedGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity; //(cart.Product.GetIntegrationValue() * cart.Quantity);
                    }
                }
            }
            else if (cart.Product.CollaboratorNum != null)//各合作商商品拆成一单
            {
                int collKey = int.Parse(cart.Product.CollaboratorNum, System.Globalization.NumberStyles.AllowHexSpecifier);//将供应商编号
                var arrivedGroup = cartGroups.Where(t => t.Key == collKey).FirstOrDefault();
                if (arrivedGroup == null)
                {
                    var og = new OrderGroup()
                    {
                        Key = collKey,
                        Carts = new List<ShoppingCart>(),
                        OrderType = OrderStyle.Collaborator,
                        Price = productUnitPrice * cart.Quantity + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum)),
                        IntegrationValue = _productService.GetIntegrationValueByProductId(cart.Product.Id,
                        EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity
                    };
                    og.Carts.Add(cart);
                    cartGroups.Add(og);
                }
                else
                {
                    arrivedGroup.Carts.Add(cart);
                    arrivedGroup.Price += (productUnitPrice * cart.Quantity) + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum));
                    arrivedGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cart.Product.Id,
                        EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity; //(cart.Product.GetIntegrationValue() * cart.Quantity);
                }

            }
            else if (cart.Product.StockQuantity > 0 || (_shopUnitOfWork.Get<ProductBatch_Product_Mapping>()
                .Where(t => t.ProductId == cart.ProductId).Count() > 0 && _shopUnitOfWork.Get<ProductBatch_Product_Mapping>()
                .Where(t => t.ProductId == cart.ProductId && !t.ProductBatch.IsArrived).Count() == 0)) // 有库存或批次到货坼成一单
            {
                #region 套装
                var isSuit = false;
                if (isSuit)
                {
                    var temKey = otherKey;
                    var arrivedGroup = cartGroups.Where(t => t.Key == temKey).FirstOrDefault();
                    if (arrivedGroup == null)
                    {
                        var og = new OrderGroup()
                        {
                            Key = temKey,
                            Carts = new List<ShoppingCart>(),
                            OrderType = OrderStyle.Suit,
                            Price = productUnitPrice * cart.Quantity,
                            IntegrationValue = _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity
                        };
                        og.Carts.Add(cart);
                        cartGroups.Add(og);
                    }
                    else
                    {
                        arrivedGroup.Carts.Add(cart);
                        arrivedGroup.Price += (productUnitPrice * cart.Quantity);
                        arrivedGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity; // (cart.Product.GetIntegrationValue() * cart.Quantity);
                    }
                }
                #endregion
                else
                {
                    var arrivedGroup = cartGroups.Where(t => t.Key == 0).FirstOrDefault();
                    if (arrivedGroup == null)
                    {
                        var og = new OrderGroup()
                        {
                            Key = 0,
                            Carts = new List<ShoppingCart>(),
                            Price = productUnitPrice * cart.Quantity + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum)),
                            IntegrationValue = _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity //cart.Product.GetIntegrationValue() * cart.Quantity
                        };
                        og.Carts.Add(cart);
                        cartGroups.Add(og);
                    }
                    else
                    {
                        arrivedGroup.Carts.Add(cart);
                        arrivedGroup.Price += (productUnitPrice * cart.Quantity) + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum));
                        arrivedGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity; // (cart.Product.GetIntegrationValue() * cart.Quantity);
                    }
                }
            }
            else //无库存批次未到货坼成一单
            {
                var batch = _shopUnitOfWork.Get<ProductBatch_Product_Mapping>().
                Where(t => t.ProductId == cart.ProductId && !t.ProductBatch.IsArrived).OrderBy(t => t.CreatedTime).FirstOrDefault();
                if (batch != null)
                {
                    var NotArrivedGroup = cartGroups.Where(t => t.Key == batch.ProductBatchId).FirstOrDefault();
                    if (NotArrivedGroup == null)
                    {
                        var og = new OrderGroup()
                        {
                            Key = batch.ProductBatchId,
                            Carts = new List<ShoppingCart>(),
                            Price = productUnitPrice * cart.Quantity + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum)),
                            IntegrationValue = _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity //cart.Product.GetIntegrationValue() * cart.Quantity
                        };
                        og.Carts.Add(cart);
                        cartGroups.Add(og);
                    }
                    else
                    {
                        NotArrivedGroup.Carts.Add(cart);
                        NotArrivedGroup.Price += (productUnitPrice * cart.Quantity) + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum));
                        NotArrivedGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity; // (cart.Product.GetIntegrationValue() * cart.Quantity);
                    }
                }
                else
                {
                    var otherGroup = cartGroups.Where(t => t.Key == -1).FirstOrDefault();
                    if (otherGroup == null)
                    {
                        var og = new OrderGroup()
                        {
                            Key = -1,
                            Carts = new List<ShoppingCart>(),
                            Price = productUnitPrice * cart.Quantity + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum)),
                            IntegrationValue = _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity //cart.Product.GetIntegrationValue() * cart.Quantity
                        };
                        og.Carts.Add(cart);
                        cartGroups.Add(og);
                    }
                    else
                    {
                        otherGroup.Carts.Add(cart);
                        otherGroup.Price += (productUnitPrice * cart.Quantity) + (fareCarts == null ? 0 : fareCarts.Sum(i => i.AdFee * i.CartNum));
                        otherGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cart.Product.Id,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity; // (cart.Product.GetIntegrationValue() * cart.Quantity);
                    }
                }
            }
        }
        /// <summary>
        /// 预售的购物车分组
        /// </summary>
        /// <param name="cart"></param>
        /// <param name="cartGroups"></param>
        protected virtual void GroupPresellCart(IList<ShoppingCart> carts, IList<OrderGroup> cartGroups, ref int otherKey)
        {
            foreach (var cart in carts)
            {
                var onekey = otherKey;
                var presellproduct = _presellService.GetPresellProductByProductId(cart.Product.Id);
                var presellPrice = _productService.GetSalePriceByProductId(cart.ProductId, -1);
                var arrivedGroup = cartGroups.Where(t => t.Key == onekey).FirstOrDefault();
                if (arrivedGroup == null)
                {
                    var og = new OrderGroup()
                    {
                        Key = onekey,
                        Carts = new List<ShoppingCart>(),
                        Price = presellPrice * cart.Quantity,
                        OrderType = OrderStyle.PresellOrder,
                        IntegrationValue = _productService.GetIntegrationValueByProductId(cart.Product.Id,
                                EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity,//cart.Product.GetIntegrationValue() * cart.Quantity
                        Protocol = "红酒世界海外直购酒款购买须知",
                        AgreeProtocolState = "已同意"
                    };
                    og.Carts.Add(cart);
                    cartGroups.Add(og);
                }
                else
                {
                    arrivedGroup.Carts.Add(cart);
                    arrivedGroup.Price += (presellPrice * cart.Quantity);
                    arrivedGroup.OrderType = OrderStyle.PresellOrder;
                    arrivedGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cart.Product.Id,
                                EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity; // (cart.Product.GetIntegrationValue() * cart.Quantity);
                }
            }
            otherKey--;
        }

        protected virtual void GroupExpectCart(ShoppingCart cart, IList<OrderGroup> cartGroups, ref int otherKey)
        {
            var mapping = _productService.GetExpectProduct_Product_MappingByProductId(cart.ProductId);
            if (mapping == null)
            {
                // have no mapping , to do something
            }
            else
            {
                var expectProduct = _productService.GetExpectProductById(mapping.ExpectProductId);
                if (expectProduct == null)
                {
                    // have no expectproduct , to do something
                }
                else if (expectProduct.State != ProductState.Published)
                {
                    // have no published , to do something
                }
                else if (!expectProduct.IsCanSingleSale)
                {
                    // can  not singlesale, to do something
                }
                else if (cart.Quantity > 0 && expectProduct.AvailableQuantity >= cart.Quantity)
                {
                    var unitPrice = cart.Product.GetSalePrice((Int32)ProductPriceColumnAct.Expect);
                    if (unitPrice <= 0)
                    {
                        //have no unitprice , to do something
                    }
                    else
                    {
                        int tempKey = otherKey;
                        var existGroup = cartGroups.Where(t => t.Key == tempKey).FirstOrDefault();
                        if (existGroup == null)
                        {
                            var oGroup = new OrderGroup()
                            {
                                Key = otherKey,
                                Carts = new List<ShoppingCart>(),
                                Price = unitPrice * cart.Quantity,
                                OrderType = OrderStyle.Expect,
                                IntegrationValue = _productService.GetIntegrationValueByProductId(cart.Product.Id,
                                EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity,
                                Protocol = "红酒世界波尔多期酒购买须知",
                                AgreeProtocolState = "已同意"
                            };
                            if (_productService.isOtherExpectProductByExpectId(mapping.ExpectProductId))
                            {
                                oGroup.Protocol = "红酒世界跨境期酒购买须知";
                            }
                            oGroup.Carts.Add(cart);
                            cartGroups.Add(oGroup);
                            otherKey--;
                        }
                        else
                        {
                            existGroup.Carts.Add(cart);
                            existGroup.Price += (unitPrice * cart.Quantity);
                            existGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cart.Product.Id,
                                EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity;
                            existGroup.OrderType = OrderStyle.Expect;
                            otherKey--;
                        }
                    }
                }
                else
                {
                    // beyond  the  availablequantity  limit , to do  something
                }
            }
        }

        /// <summary>
        /// 跨境电商购物车分组
        /// </summary>
        /// <param name="cbpCarts"></param>
        /// <param name="cartGroups"></param>
        /// <param name="otherKey"></param>

        public virtual void GroupCBPCart(IList<ShoppingCart> cbpCarts, IList<OrderGroup> cartGroups, ref int otherKey)
        {
            foreach (var cart in cbpCarts)
            {
                int tempKey = otherKey;
                var cBPUnitPrice = cart.Product.GetSalePrice((Int32)ProductPriceColumnAct.CrossBorder);
                var cBPGroup = cartGroups.Where(t => t.Key == tempKey).FirstOrDefault();
                if (cBPGroup == null)
                {
                    var oGroup = new OrderGroup()
                    {
                        Key = tempKey,
                        Carts = new List<ShoppingCart>(),
                        Price = cBPUnitPrice * cart.Quantity,
                        IntegrationValue = _productService.GetIntegrationValueByProductId(cart.Product.Id,
                                EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity,
                        OrderType = OrderStyle.CBP,
                        Protocol = "消费者告知书",
                        AgreeProtocolState = "已同意"
                    };
                    oGroup.Carts.Add(cart);
                    cartGroups.Add(oGroup);

                }
                else
                {
                    cBPGroup.Carts.Add(cart);
                    cBPGroup.Price += (cBPUnitPrice * cart.Quantity);
                    cBPGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cart.Product.Id,
                                EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType)) * cart.Quantity;
                    cBPGroup.OrderType = OrderStyle.CBP;

                }
            }
            otherKey--;


        }

        /// <summary>
        /// 众筹购物车分组
        /// </summary>
        /// <param name="cbpCarts"></param>
        /// <param name="cartGroups"></param>
        /// <param name="otherKey"></param>
        public virtual void GroupCFPCart(ShoppingCart cfpCart, IList<OrderGroup> cartGroups, ref int otherKey)
        {
            int tempKey = otherKey;
            var cFPUnitPrice = cfpCart.Product.GetSalePrice((Int32)ProductPriceColumnAct.CrowdFunding);
            var cFPGroup = cartGroups.Where(t => t.Key == tempKey).FirstOrDefault();
            if (cFPGroup == null)
            {
                var oGroup = new OrderGroup()
                {
                    Key = tempKey,
                    Carts = new List<ShoppingCart>(),
                    Price = cFPUnitPrice * cfpCart.Quantity,
                    IntegrationValue = _productService.GetIntegrationValueByProductId(cfpCart.Product.Id,
                                EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cfpCart.CartType)) * cfpCart.Quantity,
                    OrderType = OrderStyle.CFP
                };
                oGroup.Carts.Add(cfpCart);
                cartGroups.Add(oGroup);

            }
            else
            {
                cFPGroup.Carts.Add(cfpCart);
                cFPGroup.Price += (cFPUnitPrice * cfpCart.Quantity);
                cFPGroup.IntegrationValue += _productService.GetIntegrationValueByProductId(cfpCart.Product.Id,
                                EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cfpCart.CartType)) * cfpCart.Quantity;
                cFPGroup.OrderType = OrderStyle.CFP;

            }
            otherKey--;

        }
        #endregion 给购物车分组,拆单


        public virtual IList<Order> SubmitOrder(
            IList<ShoppingCart> carts,
            Address address,
            string accountRemark,
            bool isNeedInvoice = false,
            InvoiceTitle titleType = InvoiceTitle.Personal,
            string titleInfo = "",
            PaymentType pay = null,
            int useZmCoupon = 0,
            double useWineCoupon = 0.0,
            double useWineWorldCoupon = 0.0,
            Dictionary<int, List<CouponModel>> order_ProductId_UseCouponModelList = null,
            bool isMobile = false,
            string rebateWebSiteId = null,
            string rebateWebSite_Uid = null,
            bool IsZXGift = false,
            bool isGenerateCode = false,
            JiHua jihua = null,
            string cardNumber = "",
            string payerName = "",
            string buycode = "",
            string platform = null,
            Order_OwnTakeModel order_OwnTakeModel = null
            )
        {
            if (carts.IsNullOrEmpty())
                throw new WineException("订单已修改，请刷新页面");

            if (carts.Select(t => t.Quantity).Sum() <= 0)
            {
                throw new WineException("购买商品数量有误，请重新输入！");
            }

            if (!isMobile && pay == null)  //手机版跳过验证
                throw new WineException("未选择支付方式！");

            var result = new List<Order>();

            bool isStaff = _workContext.CurrentAccount.IsStaff();//是否是内部员工
            bool isVip = _workContext.CurrentAccount.IsVipMember();//是否是会员

            var cartGroups = GroupCarts(carts); //给购物车分组,拆单

            var workflowMessageService = EngineContext.Current.Resolve<IWorkflowMessageService>();

            #region 校验券

            if (_workContext.CurrentInternetUser_ZM != null && (useZmCoupon > 0 || useWineCoupon > 0 || useWineWorldCoupon > 0))
            {
                string jc = EngineContext.Current.Resolve<IWorkflowMessageService>().GetAll(_workContext.CurrentInternetUser_ZM.UserName);

                double currentCoupon = 0.0;
                double currentWineCoupon = 0.0;
                double currentWineWorldCoupon = 0.0;
                int zmJiFenNew = 0;
                if (jc != string.Empty)
                {

                    currentCoupon = Convert.ToDouble(jc.Split('|')[1].Convert<double>());
                    currentWineCoupon = Convert.ToDouble(jc.Split('|')[2].Convert<double>());
                    currentWineWorldCoupon = Convert.ToDouble(jc.Split('|')[3].Convert<double>());
                    zmJiFenNew = Convert.ToInt32(jc.Split('|')[4].Convert<double>());
                }

                //红酒网红酒券与中民红酒券这里做一个处理，优先抵扣红酒网红酒券，抵扣金额为正整数
                if (useZmCoupon > 0 && currentCoupon < useZmCoupon)
                {
                    throw new WineException("中民券不足！");
                }
                if (useWineCoupon > 0 && currentWineCoupon < useWineCoupon)
                {
                    throw new WineException("中民红酒券不足");
                }
                if (useWineWorldCoupon > 0 && ((int)currentWineWorldCoupon + (int)currentWineCoupon) < useWineWorldCoupon)
                {
                    throw new WineException("红酒券不足");
                }
                else
                {
                    if (useWineWorldCoupon > (int)currentWineWorldCoupon)
                    {
                        useWineCoupon = useWineWorldCoupon - (int)currentWineWorldCoupon;
                        useWineWorldCoupon = (int)currentWineWorldCoupon;
                    }
                }
                var sumPrice = cartGroups.Where(x => x.OrderType != OrderStyle.CBP).Sum(t => t.Price).Convert<int>();

                // 所用中民积分和券值大于价格
                if (useZmCoupon + (int)useWineCoupon + (int)useWineWorldCoupon >= sumPrice)
                {
                    if (useZmCoupon >= sumPrice)// 中民券 足够支付订单
                    {
                        useZmCoupon = sumPrice;
                        useWineCoupon = 0;
                        useWineWorldCoupon = 0;
                    }
                    else if (useZmCoupon + (int)useWineWorldCoupon >= sumPrice)
                    {
                        useWineWorldCoupon = sumPrice - useZmCoupon;
                        useWineCoupon = 0;
                    }
                    else if (useZmCoupon + useWineCoupon + useWineWorldCoupon >= sumPrice)
                    {
                        useWineCoupon = sumPrice - useWineWorldCoupon - useZmCoupon;
                    }
                }
            }

            #endregion 校验券等

            int orderid = 0; //订单ID
            int tempFakeSeed = 0;

            foreach (var g in cartGroups)
            {
                var serialNumber = string.Empty;
                string payCodeNew = "";
                bool ISinsertPaygift = false;
                if (string.IsNullOrEmpty(platform))
                {
                    platform = "unknown"; //订单来源的平台
                    if (isMobile)
                    {
                        platform = "Mobile";
                    }
                    else
                    {
                        platform = "PC";
                    }
                }

                if (isGenerateCode)
                {
                    serialNumber = GetOrderSerialNumber("HG");//GP
                }
                else
                {
                    serialNumber = GetOrderSerialNumber("XH");//XP
                    if (g.OrderType == OrderStyle.CBP)
                    {
                        serialNumber = GetOrderSerialNumber("KJ");//KP
                    }
                    else if (g.OrderType == OrderStyle.PresellOrder)
                    {
                        serialNumber = GetOrderSerialNumber("HW");//HP
                    }
                    else if (g.OrderType == OrderStyle.Expect)
                    {
                        serialNumber = GetOrderSerialNumber("QJ");//QP
                    }
                    else if (g.OrderType == OrderStyle.CFP)
                    {
                        serialNumber = GetOrderSerialNumber("ZC");//ZP
                    }
                    else if (g.OrderType == OrderStyle.Collaborator)
                    {
                        serialNumber = GetOrderSerialNumber("HZ");//合作商订单
                    }
                }

                try
                {
                    //var addressZero = address == null || (isGenerateCode && !isNeedInvoice);
                    //var addressId = addressZero ? 0 : address.Id;
                    //var addressDetail = addressZero ? "" : address.GetAddress();
                    var orderType = isGenerateCode ? (int)OrderStyle.ExchangedCard : (int)g.OrderType;

                    var addressId = address == null ? 0 : address.Id;
                    OwnTakeWarehouse tempOwnTakeWarehouse = null;
                    if (order_OwnTakeModel != null)
                    {
                        tempOwnTakeWarehouse = GetOwnTakeWarehouseById(order_OwnTakeModel.OwnTakeWarehouseId);
                    }
                    var addressDetail = address == null ? (tempOwnTakeWarehouse != null ? tempOwnTakeWarehouse.FullAddress : null) : address.GetAddress();

                    var order = new Order()
                    {
                        AccountId = _workContext.CurrentAccount.Id,
                        AccountRemark = accountRemark,
                        AddressId = addressId,
                        AddressDetail = addressDetail,
                        DeliveryType = 1,//默认为顺丰
                        CreatedBy = _workContext.CurrentAccount.Id,
                        OrderGenerateDate = DateTime.Now,
                        OrderGuid = Guid.NewGuid(),
                        OrderInvalidDate = _orderSettings.OrderInvalidMin == 0 ? DateTime.Now.AddHours(48) : DateTime.Now.AddMinutes(_orderSettings.OrderInvalidMin),
                        SerialNumber = serialNumber,
                        Payment = Payment.PayAll,
                        IsNeedInvoice = (orderType == 4 ? false : isNeedInvoice),// 跨境电商不能开发票
                        FactPrice = g.Price,
                        SumPrice = g.Price,
                        IntegralValue = g.IntegrationValue,
                        State = OrderState.NotPay,
                        RebateWebSiteId = rebateWebSiteId,
                        RebateWebSite_Uid = rebateWebSite_Uid,
                        RebateWebSiteNotifyFlag = NotifyFlag.NoNotify,
                        OrderType = orderType,
                        CustomerName = address == null ? order_OwnTakeModel != null ? order_OwnTakeModel.OwnTakerName : string.Empty : address.ReceiptName,
                        MobilePhone = address == null ? order_OwnTakeModel != null ? order_OwnTakeModel.OwnTakerPhone : string.Empty : address.MobileNumber,
                        Protocol = g.Protocol,
                        AgreeProtocolState = g.AgreeProtocolState,
                        PlatformCode = platform
                    };

                    if (!isMobile) //非手机提交订单时要求存在支付方式
                    {
                        order.PaymentTypeId = pay.Id;
                    }
                    else //手机版提交订单未选择支付方式
                    {
                        order.PaymentTypeId = 0;
                    }
                    #region 满免促销活动
                    if (g.OrderType == OrderStyle.Usual)
                    {
                        var promotionCarts =
                           g.Carts.Where(t => t.CartType == CartType.Usual).ToList();

                        //获取满免促销满免金额
                        decimal freePrice = _promotionsService.GetFullFreePriceByCartList(promotionCarts);
                        if (freePrice > 0 && freePrice <= order.FactPrice)
                        {
                            order.FullFreePrice = freePrice;
                            order.FactPrice = order.FactPrice - freePrice;
                        }
                    }
                    #endregion

                    #region 满折促销活动
                    if (g.OrderType == OrderStyle.Usual)
                    {
                        var promotionCarts =
                           g.Carts.Where(t => t.CartType == CartType.Usual).ToList();

                        //获取满折促销折扣金额
                        decimal discountPrice = _promotionsService.GetFullDiscountPriceByCartList(promotionCarts);
                        if (discountPrice > 0 && discountPrice <= order.FactPrice)
                        {
                            order.FullDiscountPrice = discountPrice;
                            order.FactPrice = order.FactPrice - discountPrice;
                        }
                    }
                    #endregion

                    #region 判断是否使用了代金券及当前订单真实总价是否大于0

                    decimal myProductCouponTotalPrice = 0;

                    #endregion
                    var sumGetIntegrationValue = 0; //这里计算订单赠送的中民积分总值
                    #region 修改购物车 及 添加 针对单量活动购物车礼品

                    foreach (var cart in g.Carts)
                    {
                        var unitPrice = cart.Product.GetSalePrice();
                        decimal? memberUnitPrice = null;
                        var staffQuantity = cart.Quantity;
                        var discountType = cart.DiscountType;
                        var isPromotion = (cart.DiscountType == DiscountType.Promotion) && (_promotionsService.IsApartPromotions(cart.ProductId));//优惠类型是否为活动
                        // 获取商品的活动价
                        if (cart.CartType == CartType.Usual)
                        {
                            if (isPromotion)
                            {
                                var promotionPrice = _promotionsService.GetProductPromotionPrice(cart.ProductId);
                                if (promotionPrice != null)
                                {
                                    unitPrice = (decimal)promotionPrice;
                                }
                            }
                            else if ((isVip || isStaff) && _promotionsService.IsApartMemberDiscount(cart.ProductId))
                            {
                                var isAgentProduct = _productService.IsAgentProduct(cart.ProductId);//是否是独代酒款
                                if (isAgentProduct)//独代酒款买一送一
                                {
                                    memberUnitPrice = unitPrice / 2;
                                    staffQuantity *= 2;
                                }
                                else
                                {
                                    memberUnitPrice = GetVipPrice(unitPrice);
                                }
                                discountType = isStaff ? DiscountType.Staff : DiscountType.Member;
                            }
                        }

                        if (cart.CartType == CartType.CrossBorder)
                        {
                            unitPrice = cart.Product.GetSalePrice((Int32)ProductPriceColumnAct.CrossBorder);
                        }
                        else if (cart.CartType == CartType.Expect)
                        {
                            unitPrice = cart.Product.GetSalePrice((Int32)ProductPriceColumnAct.Expect);
                        }

                        if (cart.CartType == CartType.PreSale)
                        {
                            unitPrice = _productService.GetSalePriceByProductId(cart.Product.Id, -1);
                        }
                        if (cart.CartType == CartType.CrowdFunding)
                        {
                            unitPrice = cart.Product.GetSalePrice((Int32)ProductPriceColumnAct.CrowdFunding);
                        }
                        int unitIntegralValue = _productService.GetIntegrationValueByProductId(cart.ProductId,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType));  //获取所需中民积分

                        int unitGetIntegralValue = _productService.GetUltimateQuanValueByProductId(cart.ProductId, isVip,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType));  //获取可得中民积分

                        var orderProduct = new OrderProduct()
                        {
                            Id = tempFakeSeed++,
                            CreatedBy = _workContext.CurrentAccount.Id,
                            CreatedTime = DateTime.Now,
                            OrderId = order.Id,
                            ProductId = cart.ProductId,
                            Quantity = staffQuantity,
                            OriginalUnitPrice = unitPrice,
                            MemberUnitPrice = memberUnitPrice,
                            UnitPrice = memberUnitPrice == null ? unitPrice : memberUnitPrice.Value,//最终单价
                            GetIntegrationValue = unitGetIntegralValue,
                            DiscountType = discountType,//优惠类型
                            UnitIntegrationValue = unitIntegralValue,
                            IntegrationValue = cart.Quantity * unitIntegralValue
                        };
                        sumGetIntegrationValue = sumGetIntegrationValue + unitGetIntegralValue * cart.Quantity;
                        orderProduct.Price = orderProduct.Quantity * orderProduct.UnitPrice;//最终价格
                        ValidateProductSaleInfo(cart.Product, cart.Quantity, true, cart.CartType);
                        if (cart.AccountExtend != null)//手机版立即购买不加入购物车
                        {
                            cart.State = CartState.ToOrder;
                            _shopUnitOfWork.Update<ShoppingCart>(cart);
                        }
                        if (cart.CartType == CartType.Usual || cart.CartType == CartType.WineMenu)
                        {
                            IList<GiftModel> gifts = null;
                            IList<GiftCouponModel> coupongifts = null;

                            gifts = _promotionsService.GetGiftByConfirm(new CartModel()
                            {
                                Product = _productService.GetProductCacheById(orderProduct.ProductId),
                                Quantity = orderProduct.Quantity,
                                FareCarts = orderProduct.DiscountType == DiscountType.Promotion ? _promotionsService.UpdateFareCarts(cart.Id) : null,
                                DiscountType = orderProduct.DiscountType
                            }, (pay == null ? "" : pay.Code), IsZXGift);
                            coupongifts = _promotionsService.GetGiftCouponByConfirm(new CartModel()
                            {
                                Product = _productService.GetProductCacheById(orderProduct.ProductId),
                                Quantity = orderProduct.Quantity,
                                DiscountType = orderProduct.DiscountType
                            }, (pay == null ? "" : pay.Code));


                            if (gifts != null && gifts.Count > 0)
                            {
                                foreach (var gift in gifts)
                                {
                                    if (!gift.GiftPromotions.PayCode.IsNullOrEmpty() && gift.GiftPromotions.PayCode != "ZXHD" && gift.GiftPromotions.PayCode != "MSWXHD" && order.FactPrice <= 0)
                                        continue;
                                    decimal? fareProductPrice = null;
                                    if (gift.FarePrice != null)
                                    {
                                        fareProductPrice = Convert.ToDecimal(gift.ProductModel.Price);
                                    }
                                    orderProduct.Gifts.Add(new OrderProductGifts()
                                    {
                                        GiftPromotionsId = gift.GiftPromotionsId,
                                        ProductId = gift.ProductId,
                                        Quantity = gift.Quantity,
                                        FareAddFee = gift.FareAddFee,
                                        FarePrice = gift.FarePrice,
                                        FareQuantity = gift.FareQuantity,
                                        FareProductPrice = fareProductPrice
                                    });
                                    if (!gift.GiftPromotions.PayCode.IsNullOrEmpty() && !CheckIsPayGiftOrder(serialNumber))
                                    {
                                        //添加字符代码
                                        payCodeNew = gift.GiftPromotions.PayCode;
                                        ISinsertPaygift = true;
                                    }
                                }
                            }
                            if (coupongifts != null && coupongifts.Count > 0)
                            {
                                foreach (var coupongift in coupongifts)
                                {
                                    if (!coupongift.GiftPromotions.PayCode.IsNullOrEmpty() && coupongift.GiftPromotions.PayCode != "ZXHD" && coupongift.GiftPromotions.PayCode != "MSWXHD" && order.FactPrice <= 0)
                                        continue;

                                    if (coupongift.CouponName == "AllCouponShow")
                                    {
                                        foreach (var CouponCategoryitem in coupongift.CouponCategoryTCMs)
                                        {
                                            orderProduct.CouponGifts.Add(new OrderProductCouponGifts()
                                            {
                                                GiftPromotionsId = coupongift.GiftPromotionsId,
                                                CouponCategory_Type_Chanel_MappingID = CouponCategoryitem.Id,
                                                CouponChanelCode = CouponCategoryitem.ChanelCode,
                                                Quantity = coupongift.CouponQuantity,
                                                OrderId = order.Id,
                                                cctcm = CouponCategoryitem,
                                                ShowCouponName = "AllCouponShow"
                                            });
                                            if (!coupongift.GiftPromotions.PayCode.IsNullOrEmpty() && !CheckIsPayGiftOrder(serialNumber))
                                            {
                                                //添加字符代码
                                                payCodeNew = coupongift.GiftPromotions.PayCode;
                                                ISinsertPaygift = true;
                                            }
                                        }
                                    }
                                    else
                                    {

                                        int[] aint = new int[coupongift.CouponCategoryTCMs.Count];
                                        for (int i = 0; i < coupongift.CouponCategoryTCMs.Count; i++)
                                        {
                                            aint[i] = i;
                                        }

                                        int[] bint = Tools.CommonTools.GenerateNumber(aint, coupongift.CouponQuantity);
                                        foreach (var iint in bint)
                                        {
                                            orderProduct.CouponGifts.Add(new OrderProductCouponGifts()
                                            {
                                                GiftPromotionsId = coupongift.GiftPromotionsId,
                                                CouponCategory_Type_Chanel_MappingID = coupongift.CouponCategoryTCMs[iint].Id,
                                                CouponChanelCode = coupongift.CouponCategoryTCMs[iint].ChanelCode,
                                                OrderId = order.Id,
                                                Quantity = 1,
                                                cctcm = coupongift.CouponCategoryTCMs[iint],
                                                ShowCouponName = coupongift.CouponCategoryTCMs[0].CouponCategory.Name + "等体验券随机"
                                            });
                                            if (!coupongift.GiftPromotions.PayCode.IsNullOrEmpty() && !CheckIsPayGiftOrder(serialNumber))
                                            {
                                                //添加字符代码
                                                payCodeNew = coupongift.GiftPromotions.PayCode;
                                                ISinsertPaygift = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (order.OrderType == (int)OrderStyle.Expect)
                        {
                            order.OrderProducts = new List<OrderProduct>() { orderProduct };
                        }
                        _shopUnitOfWork.Insert<OrderProduct>(orderProduct);


                        #region 判断该购物车商品是否使用了代金券及当前订单真实总价是否大于0
                        if (order_ProductId_UseCouponModelList != null) //有用代金券
                        {
                            if (order_ProductId_UseCouponModelList.Keys.Contains(cart.ProductId)) //该商品有使用代金券
                            {
                                var temp_order_ProductId_UseCouponModel = order_ProductId_UseCouponModelList[cart.ProductId];

                                //判断该商品使用的代金券数量是否大于该商品的数量
                                //如果小于等于该商品的数量,则创建关联表并移除，否则，所使用的代金券集合移除该商品的数量
                                if (temp_order_ProductId_UseCouponModel.Count <= cart.Quantity)
                                {
                                    foreach (var tempUseCouponList in temp_order_ProductId_UseCouponModel)
                                    {
                                        var order_Product_Coupon = new Order_Product_Coupon()
                                        {
                                            OrderId = order.Id,
                                            OrderProductId = orderProduct.Id,
                                            ProductId = cart.ProductId,
                                            CouponId = tempUseCouponList.Id,
                                            IsPay = false,
                                            FactOffSetMoney = tempUseCouponList.FactOffSetMoney,
                                            IsInAppUse = tempUseCouponList.IsInAppUse,

                                            CreatedTime = DateTime.Now,
                                            CreatedBy = _workContext.CurrentAccount.Id
                                        };

                                        myProductCouponTotalPrice += tempUseCouponList.FactOffSetMoney;

                                        _shopUnitOfWork.Insert<Order_Product_Coupon>(order_Product_Coupon);
                                    }

                                    order_ProductId_UseCouponModelList.Remove(cart.ProductId);
                                }
                                else
                                {
                                    var tempTop_order_productId_UseCouponModel = temp_order_ProductId_UseCouponModel.Take(cart.Quantity);

                                    foreach (var tempUseCouponList in tempTop_order_productId_UseCouponModel)
                                    {
                                        var order_Product_Coupon = new Order_Product_Coupon()
                                        {
                                            OrderId = order.Id,
                                            OrderProductId = orderProduct.Id,
                                            ProductId = cart.ProductId,
                                            CouponId = tempUseCouponList.Id,
                                            IsPay = false,
                                            FactOffSetMoney = tempUseCouponList.FactOffSetMoney,
                                            IsInAppUse = tempUseCouponList.IsInAppUse,

                                            CreatedTime = DateTime.Now,
                                            CreatedBy = _workContext.CurrentAccount.Id
                                        };

                                        myProductCouponTotalPrice += tempUseCouponList.FactOffSetMoney;

                                        _shopUnitOfWork.Insert<Order_Product_Coupon>(order_Product_Coupon);
                                    }

                                    order_ProductId_UseCouponModelList.Remove(cart.ProductId);
                                    order_ProductId_UseCouponModelList.Add(cart.ProductId, temp_order_ProductId_UseCouponModel.Skip(cart.Quantity).ToList());
                                }
                            }
                        }
                        #endregion


                        if (cart.Product.IsCombination)  //是否是组合装
                        {
                            var combinationRelatedProductList = _productService.GetCombinationRelatedProductsByCombinationProductId(cart.ProductId);

                            foreach (CombinationProduct_Product_Mapping combinationProduct_Product_Mapping in combinationRelatedProductList)
                            {
                                var salePrice = _productService.GetSalePriceByProductId(combinationProduct_Product_Mapping.ProductId);
                                var orderCombinationProduct = new OrderCombinationProduct()
                                {
                                    CreatedBy = _workContext.CurrentAccount.Id,
                                    CreatedTime = DateTime.Now,
                                    OrderProductId = orderProduct.Id,
                                    OrderId = order.Id,
                                    CombinationProductId = cart.ProductId,
                                    ProductId = combinationProduct_Product_Mapping.ProductId,
                                    Quantity = combinationProduct_Product_Mapping.Count * cart.Quantity,
                                    Price = !combinationProduct_Product_Mapping.UnitPrice.HasValue ? salePrice : (decimal)combinationProduct_Product_Mapping.UnitPrice,
                                };

                                _shopUnitOfWork.Insert<OrderCombinationProduct>(orderCombinationProduct);
                            }
                        }
                    }
                    order.GetIntegrationValue = sumGetIntegrationValue;
                    //有使用代金券，则更新order
                    if (myProductCouponTotalPrice > 0)
                    {
                        if (order.FactPrice > myProductCouponTotalPrice)
                        {
                            order.FactPrice = order.FactPrice - myProductCouponTotalPrice;

                            order.ProductCoupon = Convert.ToDouble(myProductCouponTotalPrice);
                        }
                        else
                        {
                            order.ProductCoupon = Convert.ToDouble(order.FactPrice);
                            order.FactPrice = 0;
                        }
                    }


                    if (_workContext.CurrentInternetUser_ZM != null)
                    {
                        #region 如果有使用中民 虚拟币  则  锁定

                        if (useZmCoupon > 0 || useWineCoupon > 0 || useWineWorldCoupon > 0)
                        {
                            order.ZMIntegralValue = 0;
                            order.ZMCoupon = 0;
                            order.WineCoupon = 0;
                            order.WineWorldCoupon = 0;
                            // 抵扣顺序 中民券-红酒券-红酒网红酒券
                            if (useZmCoupon > 0 && order.ZMIntegralValue < order.FactPrice.Convert<int>())
                            {
                                order.ZMCoupon = useZmCoupon >= (order.FactPrice.Convert<int>() - order.ZMIntegralValue) ? (order.FactPrice.Convert<int>() - order.ZMIntegralValue) : useZmCoupon;
                                useZmCoupon -= order.ZMCoupon;
                            }
                            if (useWineCoupon > 0 && (order.ZMIntegralValue + order.ZMCoupon) < order.FactPrice.Convert<int>())
                            {
                                order.WineCoupon = useWineCoupon >= (order.FactPrice.Convert<int>() - order.ZMIntegralValue - order.ZMCoupon) ? (order.FactPrice.Convert<int>() - order.ZMIntegralValue - order.ZMCoupon) : useWineCoupon;
                                useWineCoupon -= order.WineCoupon;
                            }
                            if (useWineWorldCoupon > 0 && (order.ZMIntegralValue + order.ZMCoupon + order.WineCoupon) < order.FactPrice.Convert<int>())
                            {
                                order.WineWorldCoupon = useWineWorldCoupon >= (order.FactPrice.Convert<int>() - order.ZMIntegralValue - order.ZMCoupon - order.WineCoupon) ? (order.FactPrice.Convert<int>() - order.ZMIntegralValue - order.ZMCoupon - order.WineCoupon) : useWineWorldCoupon;
                                useWineWorldCoupon -= order.WineWorldCoupon;
                            }

                        }

                        #endregion 如果有使用中民 虚拟币  则  锁定
                    }
                    order.FactPrice = order.FactPrice - order.ZMCoupon - (int)order.WineCoupon - (int)order.WineWorldCoupon;

                    #endregion 修改购物车 及 添加 针对单量活动购物车礼品


                    #region 期酒套装订单取消 审核环节  海外直购也已下线。注释
                    //修改期酒套装和海外直售订单状态为待确认(需客服确认)
                    //if (order.OrderType == (int)OrderStyle.Expect)
                    //{
                    //    var op = order.OrderProducts.FirstOrDefault();
                    //    if (op != null)
                    //    {
                    //        var expect = _productService.GetExpectProductByPId(op.ProductId);
                    //        if (expect != null)
                    //        {
                    //            if (expect.IsSuitExpect)
                    //            {
                    //                order.State = OrderState.ToConfirm;
                    //            }
                    //        }
                    //    }
                    //}
                    //else if (order.OrderType == (int)OrderStyle.PresellOrder)
                    //{
                    //    order.State = OrderState.ToConfirm;
                    //}
                    #endregion
                    if (!buycode.IsNullOrEmpty())
                    {
                        order.BuyCode = buycode;
                    }
                    #region 下单成功，扣减中民积分
                    if (order.IntegralValue > 0)
                    {
                        string jifenresult2 = EngineContext.Current.Resolve<IWorkflowMessageService>().ShopDeductionZM123JiFen(_workContext.CurrentAccount.UserName, order);
                        if (jifenresult2 != "true")
                        {
                            _logger.Error("订单" + order.SerialNumber + "购物扣减中民积分接口返回报错信息：" + jifenresult2);//接口报错，记录接口报错信息
                        }
                    }
                    #endregion

                    _shopUnitOfWork.Insert<Order>(order);

                    result.Add(order);

                    //跨境电商订单插入订单支付人身份证号
                    if (g.OrderType == OrderStyle.CBP)
                    {
                        var CBPOrderInfo = new CBPOrderInfo();
                        CBPOrderInfo.Id = order.Id;
                        CBPOrderInfo.OrderId = order.Id;
                        CBPOrderInfo.CardNumber = cardNumber;
                        CBPOrderInfo.PayerName = payerName;
                        CBPOrderInfo.CreatedBy = _workContext.CurrentAccount.Id;

                        _shopUnitOfWork.Insert<CBPOrderInfo>(CBPOrderInfo);
                    }

                    orderid = order.Id;

                    #region  判断是否是自提订单，如果是则保存自提相关信息
                    if (address == null && order_OwnTakeModel != null)
                    {
                        var order_OwnTakeWarehouse_Mapping = new Order_OwnTakeWarehouse_Mapping();
                        order_OwnTakeWarehouse_Mapping.OrderId = orderid;
                        order_OwnTakeWarehouse_Mapping.OwnTakeWarehouseId = order_OwnTakeModel.OwnTakeWarehouseId;
                        order_OwnTakeWarehouse_Mapping.OwnTakerName = order_OwnTakeModel.OwnTakerName;
                        order_OwnTakeWarehouse_Mapping.OwnTakerPhone = order_OwnTakeModel.OwnTakerPhone;
                        order_OwnTakeWarehouse_Mapping.OwnTakeTime = order_OwnTakeModel.OwnTakeTime;
                        order_OwnTakeWarehouse_Mapping.SellerShowAddress = tempOwnTakeWarehouse != null ? tempOwnTakeWarehouse.SellerShowAddress : null;

                        _shopUnitOfWork.Insert<Order_OwnTakeWarehouse_Mapping>(order_OwnTakeWarehouse_Mapping);
                    }
                    #endregion

                    #region 发票
                    if (order.IsNeedInvoice)
                    {
                        var invoice = new OrderInvoice();

                        invoice.Id = order.Id;
                        invoice.InvoiceTitleType = titleType;

                        if (titleType == InvoiceTitle.ValueAddedTax)
                        {
                            string[] invoicInfos = titleInfo.Split(',');
                            invoice.Id = order.Id;
                            invoice.OrganizationName = invoicInfos[0];  //单位名称
                            invoice.TaxpayerId = invoicInfos[1];        //纳税人识别码
                            invoice.RegisterAddress = invoicInfos[2];   //注册地址
                            invoice.RegisterTal = invoicInfos[3];       //注册电话
                            invoice.BankOfDeposit = invoicInfos[4];     //开户银行
                            invoice.BankAccount = invoicInfos[5];       //银行账户

                        }
                        else if (titleType == InvoiceTitle.Enterprise)
                        {
                            var invoicInfo = titleInfo.Split(',');
                            invoice.InvoiceTitleInfo = invoicInfo[0];
                            invoice.TaxpayerId = invoicInfo[1];
                            invoice.InvoiceEmail = invoicInfo.GetValueSafe(2);
                        }
                        else
                        {
                            var invoicInfo = titleInfo.Split(',');
                            invoice.InvoiceTitleInfo = invoicInfo[0];
                            invoice.InvoiceEmail = invoicInfo.GetValueSafe(1);
                        }

                        #region 保存发票信息
                        ValueAddTax valueAddTax = EngineContext.Current.Resolve<IValueAddTaxService>().GetValueAddTaxByAccountIdAndOrganizationName(_workContext.CurrentAccount.Id, titleType == InvoiceTitle.ValueAddedTax ? invoice.OrganizationName : invoice.InvoiceTitleInfo, titleType);

                        switch (titleType)
                        {
                            case InvoiceTitle.ValueAddedTax:

                                if (valueAddTax != null)
                                {
                                    valueAddTax.TaxpayerId = invoice.TaxpayerId;
                                    valueAddTax.RegisterAddress = invoice.RegisterAddress;
                                    valueAddTax.RegisterTal = invoice.RegisterTal;
                                    valueAddTax.BankOfDeposit = invoice.BankOfDeposit;
                                    valueAddTax.BankAccount = invoice.BankAccount;
                                    valueAddTax.ModifiedTime = System.DateTime.Now;
                                    valueAddTax.InvoiceType = InvoiceTitle.ValueAddedTax;
                                }
                                else
                                {
                                    valueAddTax = new ValueAddTax();
                                    valueAddTax.OrganizationName = invoice.OrganizationName;
                                    valueAddTax.AccountId = _workContext.CurrentAccount.Id;
                                    valueAddTax.TaxpayerId = invoice.TaxpayerId;
                                    valueAddTax.RegisterAddress = invoice.RegisterAddress;
                                    valueAddTax.RegisterTal = invoice.RegisterTal;
                                    valueAddTax.BankOfDeposit = invoice.BankOfDeposit;
                                    valueAddTax.BankAccount = invoice.BankAccount;
                                    valueAddTax.CommonTaxpayerPic = 0;
                                    valueAddTax.PowerOfAttorneyPic = 0;
                                    valueAddTax.TaxRegCerPic = 0;
                                    valueAddTax.IsAuditSuccess = ValueAddTexState.UnAudit;
                                    valueAddTax.InvoiceType = InvoiceTitle.ValueAddedTax;
                                    _shopUnitOfWork.Insert<ValueAddTax>(valueAddTax);
                                }
                                break;
                            case InvoiceTitle.Personal:
                                if (valueAddTax != null)
                                {
                                    valueAddTax.OrganizationName = invoice.InvoiceTitleInfo;
                                    valueAddTax.InvoiceEmail = invoice.InvoiceEmail;
                                    valueAddTax.InvoiceType = InvoiceTitle.Personal;
                                }
                                else
                                {
                                    valueAddTax = new ValueAddTax();
                                    valueAddTax.OrganizationName = invoice.InvoiceTitleInfo;
                                    valueAddTax.InvoiceEmail = invoice.InvoiceEmail;
                                    valueAddTax.InvoiceType = InvoiceTitle.Personal;

                                    valueAddTax.AccountId = _workContext.CurrentAccount.Id;
                                    valueAddTax.BankOfDeposit = "";
                                    valueAddTax.BankAccount = "";
                                    valueAddTax.RegisterTal = "";
                                    valueAddTax.RegisterAddress = "";
                                    valueAddTax.TaxpayerId = "";
                                    valueAddTax.CommonTaxpayerPic = 0;
                                    valueAddTax.PowerOfAttorneyPic = 0;
                                    valueAddTax.TaxRegCerPic = 0;
                                    valueAddTax.IsAuditSuccess = ValueAddTexState.UnAudit;
                                    _shopUnitOfWork.Insert<ValueAddTax>(valueAddTax);
                                }
                                break;
                            case InvoiceTitle.Enterprise:
                                if (valueAddTax != null)
                                {
                                    valueAddTax.OrganizationName = invoice.InvoiceTitleInfo;
                                    valueAddTax.InvoiceEmail = invoice.InvoiceEmail;
                                    valueAddTax.TaxpayerId = invoice.TaxpayerId;
                                    valueAddTax.InvoiceType = InvoiceTitle.Enterprise;
                                }
                                else
                                {
                                    valueAddTax = new ValueAddTax();
                                    valueAddTax.OrganizationName = invoice.InvoiceTitleInfo;
                                    valueAddTax.TaxpayerId = invoice.TaxpayerId;
                                    valueAddTax.InvoiceEmail = invoice.InvoiceEmail;
                                    valueAddTax.InvoiceType = InvoiceTitle.Enterprise;

                                    valueAddTax.AccountId = _workContext.CurrentAccount.Id;
                                    valueAddTax.BankOfDeposit = "";
                                    valueAddTax.BankAccount = "";
                                    valueAddTax.RegisterTal = "";
                                    valueAddTax.RegisterAddress = "";
                                    valueAddTax.CommonTaxpayerPic = 0;
                                    valueAddTax.PowerOfAttorneyPic = 0;
                                    valueAddTax.TaxRegCerPic = 0;
                                    valueAddTax.IsAuditSuccess = ValueAddTexState.UnAudit;
                                    _shopUnitOfWork.Insert<ValueAddTax>(valueAddTax);
                                }
                                break;

                        }
                        #endregion


                        _shopUnitOfWork.Insert<OrderInvoice>(invoice);
                    }
                    #endregion

                    #region creator：chenpeng 返利相关

                    //判断该订单是否是来自返利网的用户所下，若是则将订单中的商品进行返利计算并记录详情
                    if (!string.IsNullOrEmpty(rebateWebSiteId) && !string.IsNullOrEmpty(rebateWebSite_Uid))
                    {
                        var rebateWebSite = _productService.GetRebateWebSiteById(Convert.ToInt32(rebateWebSiteId)); //根据Id查找是否存在该返利站点

                        if (rebateWebSite != null)  //有对接站点存在
                        {
                            foreach (var cart in g.Carts)
                            {
                                var rebateRate_Product_Mapping = _shopUnitOfWork.Get<RebateRate_Product_Mapping>().Where(t => t.Isvalid && t.RebateWebSiteId == rebateWebSite.Id && t.ProductId == cart.ProductId).FirstOrDefault();

                                if (rebateRate_Product_Mapping != null)  //该商品在该返利站点中有返利
                                {
                                    decimal discountRate = rebateRate_Product_Mapping.RebateRate.RateValue;   //返利站点的折扣率
                                    decimal unitPrice = _productService.GetSalePriceByProductId(cart.ProductId);

                                    if (order_ProductId_UseCouponModelList != null) //有用代金券
                                    {
                                        //判断该商品是否使用代金券，使用了代金券的商品不返利
                                        var currentProduct_UserCouponId = order_ProductId_UseCouponModelList.Keys.Where(t => t == cart.ProductId).FirstOrDefault();

                                        if (currentProduct_UserCouponId != null && currentProduct_UserCouponId != 0) //该商品使用了代金券
                                        {
                                            var useCouponCount = order_ProductId_UseCouponModelList[currentProduct_UserCouponId].Count; //使用了代金券的数量

                                            var canRebateCount = cart.Quantity - useCouponCount; //还可以返利的商品数量

                                            if (canRebateCount > 0)
                                            {
                                                var rebateOrderProduct = new RebateOrderProduct()
                                                {
                                                    CreatedBy = _workContext.CurrentAccount.Id,
                                                    CreatedTime = DateTime.Now,
                                                    OrderId = order.Id,
                                                    ProductId = cart.ProductId,
                                                    Quantity = canRebateCount,
                                                    RebateMoney = canRebateCount * unitPrice * discountRate,
                                                };
                                                _shopUnitOfWork.Insert<RebateOrderProduct>(rebateOrderProduct);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var rebateOrderProduct = new RebateOrderProduct()
                                        {
                                            CreatedBy = _workContext.CurrentAccount.Id,
                                            CreatedTime = DateTime.Now,
                                            OrderId = order.Id,
                                            ProductId = cart.ProductId,
                                            Quantity = cart.Quantity,
                                            RebateMoney = cart.Quantity * unitPrice * discountRate,
                                        };
                                        _shopUnitOfWork.Insert<RebateOrderProduct>(rebateOrderProduct);
                                    }
                                }
                            }
                        }
                    }

                    #endregion creator：chenpeng 返利相关

                    #region 插入针对总量活动的礼品

                    var toTotalgifts = _promotionsService.GetGiftByConfirmList(g.Carts, null, false);
                    if (toTotalgifts != null && toTotalgifts.Count > 0)
                    {
                        foreach (var gift in toTotalgifts)
                        {
                            order.ToTotalGifts.Add(new OrderProductGifts()
                            {
                                GiftPromotionsId = gift.GiftPromotionsId,
                                ProductId = gift.ProductId,
                                Quantity = gift.Quantity,
                            });
                        }
                    }

                    #endregion 插入针对总量活动的礼品

                    #region creator :chenpeng 该订单来自返利网的用户所下，则需要反馈给返利网

                    if (order.RebateOrderProducts.Count > 0)
                    {
                        System.Threading.Tasks.Task.Factory.StartNew(() => { EngineContext.Current.Resolve<IWorkflowMessageService>().FeedBackToRebateWebSite(order); });
                    }

                    #endregion creator :chenpeng 该订单来自返利网的用户所下，付款成功时，则需要反馈给返利网

                    #region 锁定中民券、红酒券等
                    if (order.ZMCoupon > 0 || order.WineCoupon > 0 || order.WineWorldCoupon > 0)
                    {
                        string lockResult = workflowMessageService.LockAll(order.SerialNumber, _workContext.CurrentInternetUser_ZM.UserName, 0, order.ZMCoupon, order.WineCoupon, order.WineWorldCoupon);

                        // 锁定中民积分、券失败，则返回
                        if (lockResult == "6")
                        {
                            return null;
                        }
                    }
                    #endregion

                    _shopUnitOfWork.SaveChanges();
                }
                catch (Exception ex)  // 如果出现异常则释放中民积分
                {
                    if (_workContext.CurrentInternetUser_ZM != null && (useZmCoupon > 0 || useWineCoupon > 0 || useWineWorldCoupon > 0))
                    {
                        EngineContext.Current.Resolve<IWorkflowMessageService>().ReleaseAll(serialNumber);
                    };
                    throw;
                }
                
                if (ISinsertPaygift)
                {
                    if (!CheckIsPayGiftOrder(serialNumber))
                    {
                        InsertPayGiftOrder(new PayGiftOrder() { SerialNumber = serialNumber, PayTypeCode = payCodeNew, });
                    }
                }
                if (!buycode.IsNullOrEmpty())
                {
                    //表明该订单是购买码订单，生成订单，将该购买码的状态设置为被占用，并且设置对应的订单号
                    var ProductCDKey = _buyProductCDKeyService.GetcdkeyBycode(buycode);
                    if (ProductCDKey != null)
                    {
                        ProductCDKey.OrdertId = orderid;
                        ProductCDKey.CodeStatus = (int)CodeBuyStatus.Occupied;
                        ProductCDKey.UseTime = DateTime.Now;
                        _buyProductCDKeyService.UpdateBuyProductCDKey(ProductCDKey);
                    }
                }
            }
            #region 给订单附加集花信息 暂且作废 已注释
            //try
            //{
            //    UpdateOrderJiHua(result.FirstOrDefault(), jihua);
            //}
            //catch (Exception e)
            //{
            //    _logger.Error("提交订单成功，处理集花异常！" + e.Message);
            //}
            #endregion


            return result;
        }

        /// <summary>
        /// 渠道活动/抢购专区/中信特区活动的订单生成方法
        /// </summary>
        /// <param name="address">地址</param>
        /// <param name="productId">抢购商品的ID</param>
        /// <param name="columnActId">渠道活动ID</param>
        /// <param name="proNum">该抢购商品的数量</param>
        /// <param name="accountRemark"></param>
        /// <param name="isNeedInvoice"></param>
        /// <param name="titleType"></param>
        /// <param name="titleInfo"></param>
        /// <param name="pay"></param>
        /// <param name="isMobile"></param>
        /// <param name="rebateWebSiteId"></param>
        /// <param name="rebateWebSite_Uid"></param>
        /// <returns></returns>
        public virtual IList<Order> SubmitColumnOrder(
            Address address,
            int productId,
            int columnActId,
            int proNum,
            string accountRemark,
            out string message,
            bool isNeedInvoice = false,
            InvoiceTitle titleType = InvoiceTitle.Personal,
            string titleInfo = "",
            PaymentType pay = null,
            bool isMobile = false,
            string rebateWebSiteId = null,
            string rebateWebSite_Uid = null,
            int useZmCoupon = 0,
            double useWineCoupon = 0.0,
            double useWineWorldCoupon = 0.0,
            JiHua jihua = null
            )
        {

            if (proNum <= 0)
            {
                message = "购买商品数量有误，请重新输入！";
                return null;
            }

            if (!isMobile && pay == null)
            {
                message = "未选择支付方式！";
                return null;
            }

            var result = new List<Order>();
            Product productAct = _productService.GetProductById(productId);
            ColumnActivity columnAct = _columnService.GetColumnActivityById(columnActId); //获取渠道活动
            ColumnChanel columnChanel = _columnService.GetColumnChanelById(columnAct.ColumnChanelId); //获取渠道栏目
            decimal pricesum = _productService.GetSalePriceByProductId(productId, columnActId) * proNum;
            var workflowMessageService = EngineContext.Current.Resolve<IWorkflowMessageService>();
            int tempFakeSeed = 0;
            var serialNumber = string.Empty;
            #region 校验券

            if (_workContext.CurrentInternetUser_ZM != null && (useZmCoupon > 0 || useWineCoupon > 0 || useWineWorldCoupon > 0))
            {
                string jc = EngineContext.Current.Resolve<IWorkflowMessageService>().GetAll(_workContext.CurrentInternetUser_ZM.UserName);

                double currentCoupon = 0.0;
                double currentWineCoupon = 0.0;
                double currentWineWorldCoupon = 0.0;
                if (jc != string.Empty)
                {

                    currentCoupon = Convert.ToDouble(jc.Split('|')[1].Convert<double>());
                    currentWineCoupon = Convert.ToDouble(jc.Split('|')[2].Convert<double>());
                    currentWineWorldCoupon = Convert.ToDouble(jc.Split('|')[3].Convert<double>());
                }


                if (useZmCoupon > 0 && currentCoupon < useZmCoupon)
                {
                    throw new WineException("中民券不足！");
                }
                if (useWineCoupon > 0 && currentWineCoupon < useWineCoupon)
                {
                    throw new WineException("中民红酒券不足");
                }
                if (useWineWorldCoupon > 0 && ((int)currentWineWorldCoupon + (int)currentWineCoupon) < useWineWorldCoupon)
                {
                    throw new WineException("红酒券不足");
                }
                else
                {
                    if (useWineWorldCoupon > (int)currentWineWorldCoupon)
                    {
                        useWineCoupon = useWineWorldCoupon - (int)currentWineWorldCoupon;
                        useWineWorldCoupon = (int)currentWineWorldCoupon;
                    }
                }
                var sumPrice = Convert.ToDouble(pricesum);
                // 所用券值大于价格
                if (useZmCoupon + useWineCoupon + useWineWorldCoupon >= sumPrice)
                {
                    if (useZmCoupon >= sumPrice)// 中民券 足够支付订单
                    {

                        useZmCoupon = Convert.ToInt32(pricesum);
                        useWineCoupon = 0;
                        useWineWorldCoupon = 0;
                    }
                    else if (useZmCoupon + (int)useWineWorldCoupon >= sumPrice)
                    {
                        useWineWorldCoupon = sumPrice - useZmCoupon;
                        useWineCoupon = 0;
                    }
                    else if (useZmCoupon + useWineCoupon + useWineWorldCoupon >= sumPrice)
                    {
                        useWineCoupon = sumPrice - useWineWorldCoupon - useZmCoupon;
                    }
                }
            }

            #endregion 校验中民券
            string platform = "unknown"; //订单来源的平台
            if (isMobile)
            {
                serialNumber = GetOrderSerialNumber("LM"); //渠道活动的手机版订单前缀
                platform = "Mobile";
            }
            else
            {
                serialNumber = GetOrderSerialNumber("LZ"); //渠道活动的PC版订单前缀
                platform = "PC";
            }
            try
            {

                var addressId = address.Id;
                var addressDetail = address.GetAddress();
                var orderType = (int)OrderStyle.Usual;
                #region 判断该订单是否可以生成，并且判断该渠道等的一些数据
                //Step1 判断该渠道和该活动是否过期或者是否启用或者删除等
                if (!columnChanel.Isvalid || !columnChanel.IsInvoke)
                {
                    message = "该渠道未启用或者已经删除！，非法进入！";
                    return null;
                }
                if (!columnAct.IsInvoke || !columnAct.Isvalid)
                {
                    message = "该渠道下面的活动未启用或者已经删除！，非法进入！";
                    return null;
                }
                if (columnAct.BeginTime > System.DateTime.Now)
                {
                    message = "该活动还未开始，敬请期待！";
                    return null;
                }
                if (columnAct.EndTime < System.DateTime.Now)
                {
                    message = "该活动已经结束，谢谢参与！";
                    return null;
                }
                //Step2 判断该该用户是否属于该渠道下面的用户
                //if (!_columnService.IsUserBlongToColumnByColId(_workContext.CurrentAccount.Id,columnAct.ColumnChanelId))
                //{
                //    message = "该渠道用户限制，您无法进入！";
                //    return null;
                //}
                //判断该商品的限购数量和该用户是否还能够购买
                if (!_columnService.IsUserCanBuyAgain(_workContext.CurrentAccount.Id, productId, columnActId))
                {
                    message = "您已经超过购买限制！";
                    return null;
                }
                //Step4 判断该渠道活动下面的商品是否限量和限购数目等是否正确
                //限购数，渠道活动ID，商品ID
                if (!_columnService.CheckColumnActProductByProNumAndproId(proNum, productId, columnActId))
                {
                    message = "该商品已经抢完，谢谢参与！";
                    return null;
                }
                #endregion
                var order = new Order()
                {
                    AccountId = _workContext.CurrentAccount.Id,
                    AccountRemark = accountRemark,
                    AddressId = addressId,
                    AddressDetail = addressDetail,
                    DeliveryType = 1,//默认为顺丰
                    CreatedBy = _workContext.CurrentAccount.Id,
                    OrderGenerateDate = DateTime.Now,
                    OrderGuid = Guid.NewGuid(),
                    OrderInvalidDate = _orderSettings.OrderInvalidMin == 0 ? DateTime.Now.AddHours(48) : DateTime.Now.AddMinutes(_orderSettings.OrderInvalidMin),
                    SerialNumber = serialNumber,
                    Payment = Payment.PayAll,
                    IsNeedInvoice = isNeedInvoice,
                    FactPrice = pricesum,
                    SumPrice = pricesum,
                    IntegralValue = _productService.GetIntegrationValueByProductId(productId, columnActId) * proNum,
                    GetIntegrationValue = _productService.GetQuanValueByProductId(productId, columnActId) * proNum,
                    State = OrderState.NotPay,
                    PaymentTypeId = pay == null ? 0 : pay.Id,
                    RebateWebSiteId = rebateWebSiteId,
                    RebateWebSite_Uid = rebateWebSite_Uid,
                    RebateWebSiteNotifyFlag = NotifyFlag.NoNotify,
                    OrderType = orderType,
                    ColumnActId = columnActId,
                    CustomerName = address.ReceiptName,
                    MobilePhone = address.MobileNumber,
                    PlatformCode = platform
                    //AgreeProtocolState = g.AgreeProtocolState,
                };
                if (_workContext.CurrentInternetUser_ZM != null)
                {
                    #region 如果有使用中民 虚拟币  则  锁定

                    if (useZmCoupon > 0 || useWineCoupon > 0 || useWineWorldCoupon > 0)
                    {
                        order.ZMIntegralValue = 0;
                        order.ZMCoupon = 0;
                        order.WineCoupon = 0;
                        order.WineWorldCoupon = 0;
                        // 抵扣顺序 中民券-红酒券-红酒网红酒券
                        if (useZmCoupon > 0 && order.ZMIntegralValue < Convert.ToInt32(pricesum))
                        {
                            order.ZMCoupon = useZmCoupon >= (Convert.ToInt32(pricesum) - order.ZMIntegralValue) ? (Convert.ToInt32(pricesum) - order.ZMIntegralValue) : useZmCoupon;
                            useZmCoupon -= order.ZMCoupon;
                        }
                        if (useWineCoupon > 0 && (order.ZMIntegralValue + order.ZMCoupon) < Convert.ToInt32(pricesum))
                        {
                            order.WineCoupon = useWineCoupon >= (Convert.ToInt32(pricesum) - order.ZMIntegralValue - order.ZMCoupon) ? (Convert.ToInt32(pricesum) - order.ZMIntegralValue - order.ZMCoupon) : useWineCoupon;
                            useWineCoupon -= order.WineCoupon;
                        }
                        if (useWineWorldCoupon > 0 && (order.ZMIntegralValue + order.ZMCoupon + order.WineCoupon) < Convert.ToInt32(pricesum))
                        {
                            order.WineWorldCoupon = useWineWorldCoupon >= (Convert.ToInt32(pricesum) - order.ZMIntegralValue - order.ZMCoupon - order.WineCoupon) ? (Convert.ToInt32(pricesum) - order.ZMIntegralValue - order.ZMCoupon - order.WineCoupon) : useWineWorldCoupon;
                            useWineWorldCoupon -= order.WineWorldCoupon;
                        }

                    }

                    #endregion 如果有使用中民 虚拟币  则  锁定
                }
                order.FactPrice = pricesum - order.ZMCoupon - (int)order.WineCoupon - (int)order.WineWorldCoupon;
                #region 下单成功，扣减中民积分
                if (order.IntegralValue > 0)
                {
                    string jifenresult2 = EngineContext.Current.Resolve<IWorkflowMessageService>().ShopDeductionZM123JiFen(_workContext.CurrentAccount.UserName, order);
                    if (jifenresult2 != "true")
                    {
                        _logger.Error("订单" + order.SerialNumber + "购物扣减中民积分接口返回报错信息：" + jifenresult2);//接口报错，记录接口报错信息
                    }
                }
                #endregion
                _shopUnitOfWork.Insert<Order>(order);
                result.Add(order);



                #region 发票
                //if (isNeedInvoice)
                //{
                //    var invoice = new OrderInvoice()
                //    {
                //        Id = order.Id,
                //        InvoiceTitleType = titleType,
                //        InvoiceTitleInfo = titleInfo,
                //    };
                //    _shopUnitOfWork.Insert<OrderInvoice>(invoice);
                //}
                if (isNeedInvoice)
                {
                    var invoice = new OrderInvoice();

                    invoice.Id = order.Id;
                    invoice.InvoiceTitleType = titleType;

                    if (titleType == InvoiceTitle.ValueAddedTax)
                    {
                        string[] invoicInfos = titleInfo.Split(',');
                        invoice.Id = order.Id;
                        invoice.OrganizationName = invoicInfos[0];  //单位名称
                        invoice.TaxpayerId = invoicInfos[1];        //纳税人识别码
                        invoice.RegisterAddress = invoicInfos[2];   //注册地址
                        invoice.RegisterTal = invoicInfos[3];       //注册电话
                        invoice.BankOfDeposit = invoicInfos[4];     //开户银行
                        invoice.BankAccount = invoicInfos[5];       //银行账户
                        #region 修改/保存 增值税发票信息
                        ValueAddTax valueAddTax = EngineContext.Current.Resolve<IValueAddTaxService>().GetValueAddTaxByAccountIdAndOrganizationName(_workContext.CurrentAccount.Id, invoice.OrganizationName);
                        if (valueAddTax != null)
                        {
                            valueAddTax.TaxpayerId = invoice.TaxpayerId;
                            valueAddTax.RegisterAddress = invoice.RegisterAddress;
                            valueAddTax.RegisterTal = invoice.RegisterTal;
                            valueAddTax.BankOfDeposit = invoice.BankOfDeposit;
                            valueAddTax.BankAccount = invoice.BankAccount;
                            valueAddTax.ModifiedTime = System.DateTime.Now;
                        }
                        else
                        {
                            valueAddTax = new ValueAddTax();
                            valueAddTax.OrganizationName = invoice.OrganizationName;
                            valueAddTax.AccountId = _workContext.CurrentAccount.Id;
                            valueAddTax.TaxpayerId = invoice.TaxpayerId;
                            valueAddTax.RegisterAddress = invoice.RegisterAddress;
                            valueAddTax.RegisterTal = invoice.RegisterTal;
                            valueAddTax.BankOfDeposit = invoice.BankOfDeposit;
                            valueAddTax.BankAccount = invoice.BankAccount;
                            valueAddTax.CommonTaxpayerPic = 0;
                            valueAddTax.PowerOfAttorneyPic = 0;
                            valueAddTax.TaxRegCerPic = 0;
                            valueAddTax.IsAuditSuccess = ValueAddTexState.UnAudit;
                            _shopUnitOfWork.Insert<ValueAddTax>(valueAddTax);
                        }
                        #endregion
                    }
                    else
                    {
                        invoice.InvoiceTitleInfo = titleInfo;
                    }
                    _shopUnitOfWork.Insert<OrderInvoice>(invoice);
                }
                #endregion

                decimal unitPrice = _productService.GetSalePriceByProductId(productId, columnActId);
                var orderProduct = new OrderProduct()
                {
                    Id = tempFakeSeed++,
                    CreatedBy = _workContext.CurrentAccount.Id,
                    CreatedTime = DateTime.Now,
                    OrderId = order.Id,
                    ProductId = productId,
                    Quantity = proNum,
                    OriginalUnitPrice = unitPrice,
                    UnitPrice = unitPrice,
                    Price = unitPrice * proNum,
                    UnitIntegrationValue = 0,
                    IntegrationValue = 0,
                    GetIntegrationValue = _productService.GetQuanValueByProductId(productId, columnActId)
                };
                ValidateProductSaleInfo(productAct, proNum);
                _shopUnitOfWork.Insert<OrderProduct>(orderProduct);
                #region 是否是组合装
                if (productAct.IsCombination)  //是否是组合装
                {
                    var combinationRelatedProductList = _productService.GetCombinationRelatedProductsByCombinationProductId(productId);

                    foreach (CombinationProduct_Product_Mapping combinationProduct_Product_Mapping in combinationRelatedProductList)
                    {
                        var salePrice = _productService.GetSalePriceByProductId(combinationProduct_Product_Mapping.ProductId);
                        var orderCombinationProduct = new OrderCombinationProduct()
                        {
                            CreatedBy = _workContext.CurrentAccount.Id,
                            CreatedTime = DateTime.Now,
                            OrderProductId = orderProduct.Id,
                            OrderId = order.Id,
                            CombinationProductId = productId,
                            ProductId = combinationProduct_Product_Mapping.ProductId,
                            Quantity = combinationProduct_Product_Mapping.Count * proNum,
                            Price = !combinationProduct_Product_Mapping.UnitPrice.HasValue ? salePrice : (decimal)combinationProduct_Product_Mapping.UnitPrice,
                        };

                        _shopUnitOfWork.Insert<OrderCombinationProduct>(orderCombinationProduct);
                    }
                }
                #endregion

                #region creator：chenpeng 返利相关
                //判断该订单是否是来自返利网的用户所下，若是则将订单中的商品进行返利计算并记录详情
                if (!string.IsNullOrEmpty(rebateWebSiteId) && !string.IsNullOrEmpty(rebateWebSite_Uid))
                {
                    var rebateWebSite = _productService.GetRebateWebSiteById(Convert.ToInt32(rebateWebSiteId)); //根据Id查找是否存在该返利站点
                    if (rebateWebSite != null)  //有对接站点存在
                    {
                        var rebateRate_Product_Mapping = _shopUnitOfWork.Get<RebateRate_Product_Mapping>().Where(t => t.Isvalid && t.RebateWebSiteId == rebateWebSite.Id && t.ProductId == productId).FirstOrDefault();
                        if (rebateRate_Product_Mapping != null)  //该商品在该返利站点中有返利
                        {
                            decimal discountRate = rebateRate_Product_Mapping.RebateRate.RateValue;   //返利站点的折扣率
                            decimal unitItemPrice = _productService.GetSalePriceByProductId(proNum, columnActId);  //获取渠道价格
                            var rebateOrderProduct = new RebateOrderProduct()
                            {
                                CreatedBy = _workContext.CurrentAccount.Id,
                                CreatedTime = DateTime.Now,
                                OrderId = order.Id,
                                ProductId = productId,
                                Quantity = proNum,
                                RebateMoney = proNum * unitItemPrice * discountRate,
                            };
                            _shopUnitOfWork.Insert<RebateOrderProduct>(rebateOrderProduct);
                        }
                    }
                }
                #endregion creator：chenpeng 返利相关

                //#region 插入渠道购买记录（订单信息）
                //_shopUnitOfWork.Insert<ColumnUser_Buy_Record>(new ColumnUser_Buy_Record
                //{
                //    ColumnActId = columnActId,
                //    CreatedTime = System.DateTime.Now,
                //    ProNum = proNum,
                //    OrderNum = order.SerialNumber,
                //    UserId = _workContext.CurrentAccount.Id,
                //    ProductId = productId,
                //    CreatedBy = _workContext.CurrentAccount.Id
                //});

                //#endregion
                _shopUnitOfWork.SaveChanges();
                #region 锁定中民积分、券等
                if (order.ZMCoupon > 0 || order.WineCoupon > 0 || order.WineWorldCoupon > 0)
                {
                    string lockResult = workflowMessageService.LockAll(order.SerialNumber, _workContext.CurrentInternetUser_ZM.UserName, 0, order.ZMCoupon, order.WineCoupon, order.WineWorldCoupon);

                    // 锁定中民积分、券失败，则返回
                    if (lockResult == "6")
                    {
                        message = "锁定中民积分、券失败";
                        return null;
                    }
                }
                #endregion
                #region creator :chenpeng 该订单来自返利网的用户所下，则需要反馈给返利网
                if (order.RebateOrderProducts.Count > 0)
                {
                    System.Threading.Tasks.Task.Factory.StartNew(() => { EngineContext.Current.Resolve<IWorkflowMessageService>().FeedBackToRebateWebSite(order); });
                }
                #endregion creator :chenpeng 该订单来自返利网的用户所下，付款成功时，则需要反馈给返利网
            }
            catch (Exception ex)  // 如果出现异常则抛出
            {
                throw;
            }
            _shopUnitOfWork.SaveChanges();

            message = "";

            #region 给订单附加集花信息 暂且作废 已注释
            //try
            //{
            //    UpdateOrderJiHua(result.FirstOrDefault(), jihua);
            //}
            //catch (Exception e)
            //{
            //    _logger.Error("提交订单成功，处理集花异常！" + e.Message);
            //}
            #endregion


            return result;
        }

        public virtual IList<Order> SubmitOrder_New(
            IList<ShoppingCart> carts,
            Address address,
            string accountRemark,
            bool isNeedInvoice = false,
            InvoiceTitle titleType = InvoiceTitle.Personal,
            string titleInfo = "",
            ClientType clientType = ClientType.NewAndroidApp,
            bool isUpdateShoppingCart = true,
            JiHua jihua = null,
            string cardNumber = "",
            string payerName = "",
            string buycode = "",
            Order_OwnTakeModel order_OwnTakeModel = null
            )
        {
            if (carts.Select(t => t.Quantity).Sum() <= 0)
            {
                return null;
            }
            int orderid = 0; //订单ID
            var result = new List<Order>();

            bool isStaff = _workContext.CurrentAccount.IsStaff();//是否是内部员工
            bool isVip = _workContext.CurrentAccount.IsVipMember();//是否是会员

            var cartGroups = GroupCarts(carts); //给购物车分组,拆单

            int tempFakeSeed = 0;


            foreach (var g in cartGroups)
            {
                var serialNumber = string.Empty;
                string payCodeNew = "";
                bool ISinsertPaygift = false;
                string platform = "unknown"; //订单来源的平台
                switch (clientType)
                {
                    case ClientType.NewAndroidApp:
                        {
                            platform = "Android";
                        }
                        break;
                    case ClientType.NewIOSApp:
                        {
                            platform = "IOS";
                        }
                        break;
                    case ClientType.App8848:
                        {
                            platform = "App8848";
                        }
                        break;
                }

                serialNumber = GetOrderSerialNumber("XH");//XA
                if (g.OrderType == OrderStyle.CBP)
                {
                    serialNumber = GetOrderSerialNumber("KJ");//KA
                }
                else if (g.OrderType == OrderStyle.PresellOrder)
                {
                    serialNumber = GetOrderSerialNumber("HW");//HA
                }
                else if (g.OrderType == OrderStyle.Expect)
                {
                    serialNumber = GetOrderSerialNumber("QJ");//QA
                }
                else if (g.OrderType == OrderStyle.CFP)
                {
                    serialNumber = GetOrderSerialNumber("ZC");//ZA
                }
                else if (g.OrderType == OrderStyle.Collaborator)
                {
                    serialNumber = GetOrderSerialNumber("HZ");//合作商订单
                }

                try
                {
                    var addressId = address == null ? 0 : address.Id;
                    OwnTakeWarehouse tempOwnTakeWarehouse = null;
                    if (order_OwnTakeModel != null)
                    {
                        tempOwnTakeWarehouse = GetOwnTakeWarehouseById(order_OwnTakeModel.OwnTakeWarehouseId);
                    }
                    var addressDetail = address == null ? (tempOwnTakeWarehouse != null ? tempOwnTakeWarehouse.FullAddress : null) : address.GetAddress();
                    var orderType = (int)g.OrderType;
                    var order = new Order()
                    {
                        AccountId = _workContext.CurrentAccount.Id,
                        AccountRemark = accountRemark,
                        AddressId = addressId,
                        AddressDetail = addressDetail,
                        DeliveryType = 1,//默认为顺丰
                        CreatedBy = _workContext.CurrentAccount.Id,
                        OrderGenerateDate = DateTime.Now,
                        OrderGuid = Guid.NewGuid(),
                        OrderInvalidDate = _orderSettings.OrderInvalidMin == 0 ? DateTime.Now.AddHours(48) : DateTime.Now.AddMinutes(_orderSettings.OrderInvalidMin),
                        SerialNumber = serialNumber,
                        Payment = Payment.PayAll,
                        PaymentTypeId = 0,
                        IsNeedInvoice = (orderType == 4 ? false : isNeedInvoice),// 跨境电商不能开发票
                        FactPrice = g.Price,
                        SumPrice = g.Price,
                        State = OrderState.NotPay,
                        OrderType = orderType,
                        CustomerName = address == null ? order_OwnTakeModel != null ? order_OwnTakeModel.OwnTakerName : string.Empty : address.ReceiptName,
                        MobilePhone = address == null ? order_OwnTakeModel != null ? order_OwnTakeModel.OwnTakerPhone : string.Empty : address.MobileNumber,
                        Protocol = g.Protocol,
                        AgreeProtocolState = g.AgreeProtocolState,
                        IntegralValue = g.IntegrationValue,
                        PlatformCode = platform
                    };
                    order.FactPrice = g.Price;


                    #region 满免促销活动

                    if (g.OrderType == OrderStyle.Usual)
                    {
                        var promotionCarts =
                           g.Carts.Where(t => t.CartType == CartType.Usual).ToList();
                        //获取满免促销满免金额
                        decimal freePrice = _promotionsService.GetFullFreePriceByCartList(promotionCarts);
                        if (freePrice > 0 && freePrice <= order.FactPrice)
                        {
                            order.FullFreePrice = freePrice;
                            order.FactPrice = order.FactPrice - freePrice;
                        }
                    }


                    #endregion

                    var sumGetIntegrationValue = 0;
                    #region 满折促销活动
                    if (g.OrderType == OrderStyle.Usual)
                    {
                        var promotionCarts =
                           g.Carts.Where(t => t.CartType == CartType.Usual).ToList();

                        //获取满折促销折扣金额
                        decimal discountPrice = _promotionsService.GetFullDiscountPriceByCartList(promotionCarts);
                        if (discountPrice > 0 && discountPrice <= order.FactPrice)
                        {
                            order.FullDiscountPrice = discountPrice;
                            order.FactPrice = order.FactPrice - discountPrice;
                        }
                    }
                    #endregion

                    #region 修改购物车

                    foreach (var cart in g.Carts)
                    {
                        var unitPrice = cart.Product.GetSalePrice();
                        decimal? memberUnitPrice = null;
                        var staffQuantity = cart.Quantity;
                        var discountType = cart.DiscountType;
                        var isPromotion = (cart.DiscountType == DiscountType.Promotion) && (_promotionsService.IsApartPromotions(cart.ProductId));//优惠类型是否为活动
                        // 获取商品的活动价
                        if (cart.CartType == CartType.Usual)
                        {
                            if (isPromotion)
                            {
                                var promotionPrice = _promotionsService.GetProductPromotionPrice(cart.ProductId);
                                if (promotionPrice != null)
                                {
                                    unitPrice = (decimal)promotionPrice;
                                }
                            }
                            else if ((isVip || isStaff) && _promotionsService.IsApartMemberDiscount(cart.ProductId))
                            {
                                var isAgentProduct = _productService.IsAgentProduct(cart.ProductId);//是否是独代酒款
                                if (isAgentProduct)//独代酒款买一送一
                                {
                                    memberUnitPrice = unitPrice / 2;
                                    staffQuantity *= 2;
                                }
                                else
                                {
                                    memberUnitPrice = GetVipPrice(unitPrice);
                                }
                                discountType = isStaff ? DiscountType.Staff : DiscountType.Member;
                            }
                        }
                        if (cart.CartType == CartType.CrossBorder)
                        {
                            unitPrice = cart.Product.GetSalePrice((Int32)ProductPriceColumnAct.CrossBorder);
                        }
                        else if (cart.CartType == CartType.Expect)
                        {
                            unitPrice = cart.Product.GetSalePrice((Int32)ProductPriceColumnAct.Expect);
                        }
                        if (cart.CartType == CartType.PreSale)
                        {
                            unitPrice = _productService.GetSalePriceByProductId(cart.Product.Id, -1);
                        }
                        if (cart.CartType == CartType.CrowdFunding)
                        {
                            unitPrice = cart.Product.GetSalePrice((Int32)ProductPriceColumnAct.CrowdFunding);
                        }

                        int unitIntegralValue = _productService.GetIntegrationValueByProductId(cart.ProductId,
                           EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType));  //获取所需中民积分

                        int unitGetIntegralValue = _productService.GetUltimateQuanValueByProductId(cart.ProductId, isVip,
                            EngineContext.Current.Resolve<IShoppingCartService>().GetColumnActIdByCartType(cart.CartType));  //获取可得中民积分

                        var orderProduct = new OrderProduct()
                        {
                            Id = tempFakeSeed++,
                            CreatedBy = _workContext.CurrentAccount.Id,
                            CreatedTime = DateTime.Now,
                            OrderId = order.Id,
                            ProductId = cart.ProductId,
                            Quantity = staffQuantity,
                            OriginalUnitPrice = unitPrice,
                            MemberUnitPrice = memberUnitPrice,
                            UnitPrice = memberUnitPrice == null ? unitPrice : memberUnitPrice.Value,//最终单价
                            GetIntegrationValue = unitGetIntegralValue,
                            DiscountType = discountType,//优惠类型
                            UnitIntegrationValue = unitIntegralValue,
                            IntegrationValue = cart.Quantity * unitIntegralValue
                        };
                        orderProduct.Price = orderProduct.UnitPrice * orderProduct.Quantity;//最终价格
                        //赠送的中民积分总和
                        sumGetIntegrationValue = sumGetIntegrationValue + unitGetIntegralValue * cart.Quantity;
                        ValidateProductSaleInfo(cart.Product, cart.Quantity, true, cart.CartType);

                        if (isUpdateShoppingCart && buycode.IsNullOrEmpty())
                        {
                            cart.State = CartState.ToOrder;
                            _shopUnitOfWork.Update<ShoppingCart>(cart);
                        }

                        if (cart.CartType == CartType.Usual || cart.CartType == CartType.WineMenu)
                        {
                            var gifts = _promotionsService.GetGiftByConfirm(new CartModel()
                            {
                                Product = _productService.GetProductCacheById(orderProduct.ProductId),
                                Quantity = orderProduct.Quantity,
                                FareCarts = orderProduct.DiscountType == DiscountType.Promotion ? _promotionsService.UpdateFareCarts(cart.Id) : null,
                                DiscountType = orderProduct.DiscountType
                            }, null, false);
                            var coupongifts = _promotionsService.GetGiftCouponByConfirm(new CartModel()
                            {
                                Product = _productService.GetProductCacheById(orderProduct.ProductId),
                                Quantity = orderProduct.Quantity,
                                DiscountType = orderProduct.DiscountType
                            }, null);

                            if (gifts != null && gifts.Count > 0)
                            {
                                foreach (var gift in gifts)
                                {
                                    if (!gift.GiftPromotions.PayCode.IsNullOrEmpty() && order.FactPrice <= 0)
                                        continue;
                                    decimal? fareProductPrice = null;
                                    if (gift.FarePrice != null)
                                    {
                                        fareProductPrice = Convert.ToDecimal(gift.ProductModel.Price);
                                    }
                                    orderProduct.Gifts.Add(new OrderProductGifts()
                                    {
                                        GiftPromotionsId = gift.GiftPromotionsId,
                                        ProductId = gift.ProductId,
                                        Quantity = gift.Quantity,
                                        FareAddFee = gift.FareAddFee,
                                        FarePrice = gift.FarePrice,
                                        FareQuantity = gift.FareQuantity,
                                        FareProductPrice = fareProductPrice
                                    });
                                    if (!gift.GiftPromotions.PayCode.IsNullOrEmpty() && !CheckIsPayGiftOrder(serialNumber))
                                    {
                                        //添加字符代码
                                        payCodeNew = gift.GiftPromotions.PayCode;
                                        ISinsertPaygift = true;
                                    }
                                }
                            }
                            if (coupongifts != null && coupongifts.Count > 0)
                            {
                                foreach (var coupongift in coupongifts)
                                {
                                    if (!coupongift.GiftPromotions.PayCode.IsNullOrEmpty() && order.FactPrice <= 0)
                                        continue;

                                    if (coupongift.CouponName == "AllCouponShow")
                                    {
                                        foreach (var CouponCategoryitem in coupongift.CouponCategoryTCMs)
                                        {
                                            orderProduct.CouponGifts.Add(new OrderProductCouponGifts()
                                            {
                                                GiftPromotionsId = coupongift.GiftPromotionsId,
                                                CouponCategory_Type_Chanel_MappingID = CouponCategoryitem.Id,
                                                CouponChanelCode = CouponCategoryitem.ChanelCode,
                                                Quantity = coupongift.CouponQuantity,
                                                OrderId = order.Id,
                                                cctcm = CouponCategoryitem,
                                                ShowCouponName = "AllCouponShow"
                                            });
                                            if (!coupongift.GiftPromotions.PayCode.IsNullOrEmpty() && !CheckIsPayGiftOrder(serialNumber))
                                            {
                                                //添加字符代码
                                                payCodeNew = coupongift.GiftPromotions.PayCode;
                                                ISinsertPaygift = true;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        int[] aint = new int[coupongift.CouponCategoryTCMs.Count];
                                        for (int i = 0; i < coupongift.CouponCategoryTCMs.Count; i++)
                                        {
                                            aint[i] = i;
                                        }

                                        int[] bint = Tools.CommonTools.GenerateNumber(aint, coupongift.CouponQuantity);
                                        foreach (var iint in bint)
                                        {
                                            orderProduct.CouponGifts.Add(new OrderProductCouponGifts()
                                            {
                                                GiftPromotionsId = coupongift.GiftPromotionsId,
                                                CouponCategory_Type_Chanel_MappingID = coupongift.CouponCategoryTCMs[iint].Id,
                                                CouponChanelCode = coupongift.CouponCategoryTCMs[iint].ChanelCode,
                                                OrderId = order.Id,
                                                Quantity = 1,
                                                cctcm = coupongift.CouponCategoryTCMs[iint],
                                                ShowCouponName = coupongift.CouponCategoryTCMs[0].CouponCategory.Name + "等体验券随机"
                                            });
                                            if (!coupongift.GiftPromotions.PayCode.IsNullOrEmpty() && !CheckIsPayGiftOrder(serialNumber))
                                            {
                                                //添加字符代码
                                                payCodeNew = coupongift.GiftPromotions.PayCode;
                                                ISinsertPaygift = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (order.OrderType == (int)OrderStyle.Expect)
                        {
                            order.OrderProducts = new List<OrderProduct>() { orderProduct };
                        }
                        _shopUnitOfWork.Insert<OrderProduct>(orderProduct);

                        if (cart.Product.IsCombination)  //是否是组合装
                        {
                            var combinationRelatedProductList = _productService.GetCombinationRelatedProductsByCombinationProductId(cart.ProductId);

                            foreach (CombinationProduct_Product_Mapping combinationProduct_Product_Mapping in combinationRelatedProductList)
                            {
                                var salePrice = _productService.GetSalePriceByProductId(combinationProduct_Product_Mapping.ProductId);
                                var orderCombinationProduct = new OrderCombinationProduct()
                                {
                                    CreatedBy = _workContext.CurrentAccount.Id,
                                    CreatedTime = DateTime.Now,
                                    OrderProductId = orderProduct.Id,
                                    OrderId = order.Id,
                                    CombinationProductId = cart.ProductId,
                                    ProductId = combinationProduct_Product_Mapping.ProductId,
                                    Quantity = combinationProduct_Product_Mapping.Count * cart.Quantity,
                                    Price = !combinationProduct_Product_Mapping.UnitPrice.HasValue ? salePrice : (decimal)combinationProduct_Product_Mapping.UnitPrice,
                                };

                                _shopUnitOfWork.Insert<OrderCombinationProduct>(orderCombinationProduct);
                            }
                        }
                    }
                    #endregion 修改购物车
                    order.GetIntegrationValue = sumGetIntegrationValue; //赠送的中民积分总和
                    #region 下单成功，扣减中民积分
                    if (order.IntegralValue > 0)
                    {
                        string jifenresult2 = EngineContext.Current.Resolve<IWorkflowMessageService>().ShopDeductionZM123JiFen(_workContext.CurrentAccount.UserName, order);
                        if (jifenresult2 != "true")
                        {
                            _logger.Error("订单" + order.SerialNumber + "购物扣减中民积分接口返回报错信息：" + jifenresult2);//接口报错，记录接口报错信息
                        }
                    }
                    #endregion
                    #region 期酒套装取消审核环节 海外直购也已下线 故：注释
                    //修改期酒套装和海外直售订单状态为待确认(需客服确认)
                    if (order.OrderType == (int)OrderStyle.Expect)
                    {
                        var op = order.OrderProducts.FirstOrDefault();
                        if (op != null)
                        {
                            var expect = _productService.GetExpectProductByPId(op.ProductId);
                            if (expect != null)
                            {
                                if (expect.IsSuitExpect)
                                {
                                    order.State = OrderState.ToConfirm;
                                }
                            }
                        }
                    }
                    else if (order.OrderType == (int)OrderStyle.PresellOrder)
                    {
                        order.State = OrderState.ToConfirm;
                    }
                    #endregion
                    if (!buycode.IsNullOrEmpty())
                    {
                        order.BuyCode = buycode;
                    }
                    _shopUnitOfWork.Insert<Order>(order);

                    result.Add(order);
                    orderid = order.Id;

                    #region 发票
                    if (order.IsNeedInvoice)
                    {
                        var invoice = new OrderInvoice();

                        invoice.Id = order.Id;
                        invoice.InvoiceTitleType = titleType;

                        if (titleType == InvoiceTitle.ValueAddedTax)
                        {
                            string[] invoicInfos = titleInfo.Split(',');
                            invoice.Id = order.Id;
                            invoice.OrganizationName = invoicInfos[0];  //单位名称
                            invoice.TaxpayerId = invoicInfos[1];        //纳税人识别码
                            invoice.RegisterAddress = invoicInfos[2];   //注册地址
                            invoice.RegisterTal = invoicInfos[3];       //注册电话
                            invoice.BankOfDeposit = invoicInfos[4];     //开户银行
                            invoice.BankAccount = invoicInfos[5];       //银行账户

                        }
                        else if (titleType == InvoiceTitle.Enterprise)
                        {
                            var invoicInfo = titleInfo.Split(',');
                            invoice.InvoiceTitleInfo = invoicInfo[0];
                            invoice.TaxpayerId = invoicInfo[1];
                            invoice.InvoiceEmail = invoicInfo.GetValueSafe(2);
                        }
                        else
                        {
                            var invoicInfo = titleInfo.Split(',');
                            invoice.InvoiceTitleInfo = invoicInfo[0];
                            invoice.InvoiceEmail = invoicInfo.GetValueSafe(1);
                        }

                        #region 保存发票信息
                        ValueAddTax valueAddTax = EngineContext.Current.Resolve<IValueAddTaxService>().GetValueAddTaxByAccountIdAndOrganizationName(_workContext.CurrentAccount.Id, titleType == InvoiceTitle.ValueAddedTax ? invoice.OrganizationName : invoice.InvoiceTitleInfo, titleType);

                        switch (titleType)
                        {
                            case InvoiceTitle.ValueAddedTax:

                                if (valueAddTax != null)
                                {
                                    valueAddTax.TaxpayerId = invoice.TaxpayerId;
                                    valueAddTax.RegisterAddress = invoice.RegisterAddress;
                                    valueAddTax.RegisterTal = invoice.RegisterTal;
                                    valueAddTax.BankOfDeposit = invoice.BankOfDeposit;
                                    valueAddTax.BankAccount = invoice.BankAccount;
                                    valueAddTax.ModifiedTime = System.DateTime.Now;
                                    valueAddTax.InvoiceType = InvoiceTitle.ValueAddedTax;
                                }
                                else
                                {
                                    valueAddTax = new ValueAddTax();
                                    valueAddTax.OrganizationName = invoice.OrganizationName;
                                    valueAddTax.AccountId = _workContext.CurrentAccount.Id;
                                    valueAddTax.TaxpayerId = invoice.TaxpayerId;
                                    valueAddTax.RegisterAddress = invoice.RegisterAddress;
                                    valueAddTax.RegisterTal = invoice.RegisterTal;
                                    valueAddTax.BankOfDeposit = invoice.BankOfDeposit;
                                    valueAddTax.BankAccount = invoice.BankAccount;
                                    valueAddTax.CommonTaxpayerPic = 0;
                                    valueAddTax.PowerOfAttorneyPic = 0;
                                    valueAddTax.TaxRegCerPic = 0;
                                    valueAddTax.IsAuditSuccess = ValueAddTexState.UnAudit;
                                    valueAddTax.InvoiceType = InvoiceTitle.ValueAddedTax;
                                    _shopUnitOfWork.Insert<ValueAddTax>(valueAddTax);
                                }
                                break;
                            case InvoiceTitle.Personal:
                                if (valueAddTax != null)
                                {
                                    valueAddTax.OrganizationName = invoice.InvoiceTitleInfo;
                                    valueAddTax.InvoiceEmail = invoice.InvoiceEmail;
                                    valueAddTax.InvoiceType = InvoiceTitle.Personal;
                                }
                                else
                                {
                                    valueAddTax = new ValueAddTax();
                                    valueAddTax.OrganizationName = invoice.InvoiceTitleInfo;
                                    valueAddTax.InvoiceEmail = invoice.InvoiceEmail;
                                    valueAddTax.InvoiceType = InvoiceTitle.Personal;

                                    valueAddTax.AccountId = _workContext.CurrentAccount.Id;
                                    valueAddTax.BankOfDeposit = "";
                                    valueAddTax.BankAccount = "";
                                    valueAddTax.RegisterTal = "";
                                    valueAddTax.RegisterAddress = "";
                                    valueAddTax.TaxpayerId = "";
                                    valueAddTax.CommonTaxpayerPic = 0;
                                    valueAddTax.PowerOfAttorneyPic = 0;
                                    valueAddTax.TaxRegCerPic = 0;
                                    valueAddTax.IsAuditSuccess = ValueAddTexState.UnAudit;
                                    _shopUnitOfWork.Insert<ValueAddTax>(valueAddTax);
                                }
                                break;
                            case InvoiceTitle.Enterprise:
                                if (valueAddTax != null)
                                {
                                    valueAddTax.OrganizationName = invoice.InvoiceTitleInfo;
                                    valueAddTax.InvoiceEmail = invoice.InvoiceEmail;
                                    valueAddTax.TaxpayerId = invoice.TaxpayerId;
                                    valueAddTax.InvoiceType = InvoiceTitle.Enterprise;
                                }
                                else
                                {
                                    valueAddTax = new ValueAddTax();
                                    valueAddTax.OrganizationName = invoice.InvoiceTitleInfo;
                                    valueAddTax.TaxpayerId = invoice.TaxpayerId;
                                    valueAddTax.InvoiceEmail = invoice.InvoiceEmail;
                                    valueAddTax.InvoiceType = InvoiceTitle.Enterprise;

                                    valueAddTax.AccountId = _workContext.CurrentAccount.Id;
                                    valueAddTax.BankOfDeposit = "";
                                    valueAddTax.BankAccount = "";
                                    valueAddTax.RegisterTal = "";
                                    valueAddTax.RegisterAddress = "";
                                    valueAddTax.CommonTaxpayerPic = 0;
                                    valueAddTax.PowerOfAttorneyPic = 0;
                                    valueAddTax.TaxRegCerPic = 0;
                                    valueAddTax.IsAuditSuccess = ValueAddTexState.UnAudit;
                                    _shopUnitOfWork.Insert<ValueAddTax>(valueAddTax);
                                }
                                break;

                        }
                        #endregion


                        _shopUnitOfWork.Insert<OrderInvoice>(invoice);
                    }
                    #endregion


                    //跨境电商订单插入订单支付人身份证号
                    if (g.OrderType == OrderStyle.CBP)
                    {
                        var CBPOrderInfo = new CBPOrderInfo();
                        CBPOrderInfo.Id = order.Id;
                        CBPOrderInfo.OrderId = order.Id;
                        CBPOrderInfo.CardNumber = cardNumber;
                        CBPOrderInfo.PayerName = payerName;
                        CBPOrderInfo.CreatedBy = _workContext.CurrentAccount.Id;

                        _shopUnitOfWork.Insert<CBPOrderInfo>(CBPOrderInfo);
                    }

                    #region 插入针对总量活动的礼品
                    var toTotalgifts = _promotionsService.GetGiftByConfirmList(g.Carts, null, false);
                    if (toTotalgifts != null && toTotalgifts.Count > 0)
                    {
                        foreach (var gift in toTotalgifts)
                        {
                            order.ToTotalGifts.Add(new OrderProductGifts()
                            {
                                GiftPromotionsId = gift.GiftPromotionsId,
                                ProductId = gift.ProductId,
                                Quantity = gift.Quantity,
                            });
                        }
                    }
                    #endregion 插入针对总量活动的礼品

                    #region  判断是否是自提订单，如果是则保存自提相关信息
                    if (address == null && order_OwnTakeModel != null)
                    {
                        var order_OwnTakeWarehouse_Mapping = new Order_OwnTakeWarehouse_Mapping();
                        order_OwnTakeWarehouse_Mapping.OrderId = orderid;
                        order_OwnTakeWarehouse_Mapping.OwnTakeWarehouseId = order_OwnTakeModel.OwnTakeWarehouseId;
                        order_OwnTakeWarehouse_Mapping.OwnTakerName = order_OwnTakeModel.OwnTakerName;
                        order_OwnTakeWarehouse_Mapping.OwnTakerPhone = order_OwnTakeModel.OwnTakerPhone;
                        order_OwnTakeWarehouse_Mapping.OwnTakeTime = order_OwnTakeModel.OwnTakeTime;
                        order_OwnTakeWarehouse_Mapping.SellerShowAddress = tempOwnTakeWarehouse != null ? tempOwnTakeWarehouse.SellerShowAddress : null;

                        _shopUnitOfWork.Insert<Order_OwnTakeWarehouse_Mapping>(order_OwnTakeWarehouse_Mapping);
                    }
                    #endregion
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                _shopUnitOfWork.SaveChanges();

                if (ISinsertPaygift)
                {
                    if (!CheckIsPayGiftOrder(serialNumber))
                    {
                        InsertPayGiftOrder(new PayGiftOrder() { SerialNumber = serialNumber, PayTypeCode = payCodeNew, });
                    }
                }
            }

            if (!buycode.IsNullOrEmpty())
            {
                //表明该订单是购买码订单，生成订单，将该购买码的状态设置为被占用，并且设置对应的订单号
                var ProductCDKey = _buyProductCDKeyService.GetcdkeyBycode(buycode);
                if (ProductCDKey != null)
                {
                    ProductCDKey.OrdertId = orderid;
                    ProductCDKey.CodeStatus = (int)CodeBuyStatus.Occupied;
                    ProductCDKey.UseTime = DateTime.Now;
                    _buyProductCDKeyService.UpdateBuyProductCDKey(ProductCDKey);
                }
            }
            #region 给订单附加集花信息 暂且作废 已注释
            //try
            //{
            //    UpdateOrderJiHua(result.FirstOrDefault(), jihua);
            //}
            //catch (Exception e)
            //{
            //    _logger.Error("提交订单成功，处理集花异常！" + e.Message);
            //}
            #endregion
            return result;
        }

        public bool SubmitPaidOrderFromGiftCard(int accountId,
            List<Web.Model.ShopProject.Orders.OtherCartModel> list,
            Address address,
            string remark,
            OrderStyle orderStyle, string platformCode)
        {
            var cansubmit = true;
            list.ForEach(p =>
            {
                int stockQuantity = GetCombinationProductStockQuantity(p.Product.Id);  //得到库存
                if (stockQuantity < 0)
                    cansubmit = false;
            });
            if (cansubmit)
            {
                var now = DateTime.Now;
                var order = new Order()
                {
                    AccountId = accountId,
                    SerialNumber = GetOrderSerialNumber("GF"),
                    OrderGenerateDate = now,
                    OrderInvalidDate = now.AddDays(1),
                    AddressId = address.Id,
                    AddressDetail = address.GetAddress(),
                    PaymentTypeId = _paymentTypeService.GetPaymentTypeByCode("zfb", ProviderType.ALL).Id,
                    OriginalPrice = 0,
                    State = OrderState.Paid,
                    IntegralValue = 0,
                    VouchersPrice = 0,
                    FactPrice = 0,
                    AccountRemark = remark,
                    DeliveryType = 1,//默认为顺丰
                    IsNeedInvoice = false,
                    Isvalid = true,
                    OrderGuid = Guid.NewGuid(),
                    Payment = Payment.PayAll,
                    Deposit = 0,
                    ZMIntegralValue = 0,
                    IsTransferJiuYe = false,
                    IsDeductionZmJifen = false,
                    ZMCoupon = 0,
                    WineCoupon = 0,
                    WineWorldCoupon = 0,
                    ProductCoupon = 0,
                    OrderType = (int)orderStyle,
                    CustomerName = address.ReceiptName,
                    MobilePhone = address.MobileNumber,
                    PlatformCode = string.IsNullOrEmpty(platformCode) ? "unknown" : platformCode
                };
                _shopUnitOfWork.Insert<Order>(order);
                _shopUnitOfWork.SaveChanges();

                list.ForEach(p =>
                {
                    var op = new OrderProduct();
                    op.OrderId = order.Id;
                    op.ProductId = p.Product.Id;
                    op.Quantity = p.Quantity;
                    op.Price = p.PaidPrice;
                    op.PromotionsIds = null;
                    op.Deposit = 0;
                    op.IsReview = false;
                    op.OriginalUnitPrice = p.Product.GetSalePrice();
                    op.UnitPrice = op.OriginalUnitPrice;
                    _shopUnitOfWork.Insert<OrderProduct>(op);
                });
                _shopUnitOfWork.SaveChanges();

                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 生成邀请值兑换的订单
        /// </summary>
        /// <param name="accountId">用户ID</param>
        /// <param name="list">商品list</param>
        /// <param name="address">地址</param>
        /// <param name="remark">备注</param>
        /// <param name="orderStyle">订单类型</param>
        /// <returns></returns>
        public Order SubmitPaidOrderFromInvitation(int accountId,
            List<Web.Model.ShopProject.Orders.OtherCartModel> list,
            Address address,
            string remark,
            OrderStyle orderStyle)
        {
            var cansubmit = true;
            list.ForEach(p =>
            {
                int stockQuantity = GetCombinationProductStockQuantity(p.Product.Id);  //得到库存
                if (stockQuantity < 0)
                    cansubmit = false;
            });
            if (cansubmit)
            {
                var now = DateTime.Now;
                var order = new Order()
                {
                    AccountId = accountId,
                    SerialNumber = GetOrderSerialNumber("YQ"),
                    OrderGenerateDate = now,
                    OrderInvalidDate = now.AddDays(1),
                    AddressId = address.Id,
                    AddressDetail = address.GetAddress(),
                    PaymentTypeId = _paymentTypeService.GetPaymentTypeByCode("zfb", ProviderType.ALL).Id,
                    OriginalPrice = 0,
                    State = OrderState.Paid,
                    IntegralValue = 0,
                    VouchersPrice = 0,
                    FactPrice = 0,
                    AccountRemark = remark,
                    DeliveryType = 1,//默认为顺丰
                    IsNeedInvoice = false,
                    Isvalid = true,
                    OrderGuid = Guid.NewGuid(),
                    Payment = Payment.PayAll,
                    Deposit = 0,
                    ZMIntegralValue = 0,
                    IsTransferJiuYe = false,
                    IsDeductionZmJifen = false,
                    ZMCoupon = 0,
                    WineCoupon = 0,
                    WineWorldCoupon = 0,
                    ProductCoupon = 0,
                    OrderType = (int)orderStyle,
                    CustomerName = address.ReceiptName,
                    MobilePhone = address.MobileNumber,
                    PlatformCode = "unknown"
                };
                _shopUnitOfWork.Insert<Order>(order);
                _shopUnitOfWork.SaveChanges();
                list.ForEach(p =>
                {
                    var op = new OrderProduct();
                    op.OrderId = order.Id;
                    op.ProductId = p.Product.Id;
                    op.Quantity = p.Quantity;
                    op.Price = 0;
                    op.PromotionsIds = null;
                    op.Deposit = 0;
                    op.IsReview = false;
                    op.OriginalUnitPrice = p.Product.GetSalePrice();
                    op.UnitPrice = 0;
                    _shopUnitOfWork.Insert<OrderProduct>(op);
                });
                _shopUnitOfWork.SaveChanges();
                return order;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 检查商品库存、每人限制数量
        /// </summary>
        /// <param name="product"></param>
        /// <param name="quantity"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool CheckProductHasStockQuantity(Product product, int quantity, out string message, bool isInShoppingCart = false, CartType cartType = CartType.Usual)
        {
            message = string.Empty;
            try
            {
                ValidateProductSaleInfo(product, quantity, isInShoppingCart, cartType);
            }
            catch (WineException we)
            {
                message = we.Message;
                _logger.Error(string.Format("校验商品：{0} 库存时，{1}", product.Name, we.Message));
                return false;
            }
            return true;
        }
        /// <summary>
        /// 根据订单的类型返回购物车的类型（主要用于库存等的检验）
        /// </summary>
        /// <param name="os">订单的类型</param>
        /// <returns>购物车的类型</returns>
        public CartType GetCartTypeByOrderStyle(OrderStyle os)
        {
            switch (os)
            {
                case OrderStyle.Usual:
                    return CartType.Usual;
                case OrderStyle.ExchangedCard:
                    return CartType.Usual;
                case OrderStyle.InvitationOrder:
                    return CartType.Usual;
                case OrderStyle.CBP:
                    return CartType.CrossBorder;
                case OrderStyle.Suit:
                    return CartType.Usual;
                case OrderStyle.PresellOrder:
                    return CartType.PreSale;
                default:
                    return CartType.Usual;
            }
        }
        /// <summary>
        /// 检查商品库存、每人限制数量
        /// </summary>
        /// <param name="product">商品</param>
        /// <param name="quantity">数量</param>
        public void ValidateProductSaleInfo(Product product, int quantity, bool isInShoppingCart = false, CartType cartType = CartType.Usual)
        {
            if (cartType == CartType.Usual || cartType == CartType.WineMenu)
            {
                // 检查之前的订单
                var query = _shopUnitOfWork.Get<OrderProduct>().Where(t => t.ProductId == product.Id && t.Isvalid && t.Order.Isvalid &&
                                                                            (
                                                                             t.Order.OrderType == (int)OrderStyle.Usual ||
                                                                             t.Order.OrderType == (int)OrderStyle.ExchangedCard ||
                                                                             t.Order.OrderType == (int)OrderStyle.InvitationOrder
                                                                            )
                                                                      );
                var orderProduct = query.Where(t => t.Order.State == OrderState.Paid ||
                                                  t.Order.State == OrderState.Shipped ||
                                                  t.Order.State == OrderState.Complete
                                              );
                var preBuyCount = orderProduct.Where(t => t.Order.AccountId == _workContext.CurrentAccount.Id)
                     .Select(t => t.Quantity).DefaultIfEmpty(0).Sum();
                if (!product.IsPublished())
                {
                    throw new WineException("{0}商品无效或未上架！".FormatWith(product.Name));
                }
                if (product.LimitNumberPerPerson != 0 && (preBuyCount + quantity) > product.LimitNumberPerPerson)
                {
                    throw new WineException("{0}商品每人只限购{1}瓶！购买数量已超".FormatWith(product.Name, product.LimitNumberPerPerson));
                }

                var stockQuantity = EngineContext.Current.Resolve<IProductService>().IsPreSale(product) ? product.LimitStockQuantity : product.StockQuantity;

                if (product.IsCombination)  //是组合装商品则判断组合装中商品的最少库存
                {
                    stockQuantity = GetCombinationProductStockQuantity(product.Id, isInShoppingCart);
                }
                if (quantity > stockQuantity)
                {
                    throw new WineException("{0}商品库存不足！".FormatWith(product.Name));
                }
            }
            else if (cartType == CartType.Expect)
            {
                var mapping = _productService.GetExpectProduct_Product_MappingByProductId(product.Id);
                if (mapping == null)
                {
                    throw new WineException("{0}商品无效或未上架！".FormatWith(product.Name));
                }
                else
                {
                    var expect = _productService.GetExpectProductById(mapping.ExpectProductId);
                    if (expect == null || expect.State != ProductState.Published)
                    {
                        throw new WineException("{0}商品无效或未上架！".FormatWith(product.Name));
                    }
                    else if (!expect.IsCanSingleSale)
                    {
                        throw new WineException("{0}商品不允许单售！".FormatWith(product.Name));
                    }
                    else if (expect.AvailableQuantity <= 0 || (expect.AvailableQuantity > 0 && expect.AvailableQuantity < quantity))
                    {
                        throw new WineException("{0}商品可售数量不足！".FormatWith(product.Name));
                    }
                    //else if (expect.IsSuitExpect)
                    //{
                    //    var  expectProducts= _shopUnitOfWork.Get<OrderProduct>();
                    //    expectProducts= expectProducts.Where(t =>  t.ProductId == product.Id && t.Isvalid && t.Order.Isvalid &&
                    //                                               t.Order.OrderType==(int)OrderStyle.Expect &&
                    //                                               (
                    //                                                 t.Order.State == OrderState.ToConfirm ||
                    //                                                 t.Order.State == OrderState.NotPay ||
                    //                                                 t.Order.State == OrderState.PaidNotCompleted ||
                    //                                                 t.Order.State == OrderState.Paid ||
                    //                                                 t.Order.State == OrderState.Shipped ||
                    //                                                 t.Order.State == OrderState.Complete
                    //                                                )
                    //                                         );
                    //    expectProducts = expectProducts.Where(t => t.Order.AccountId == _workContext.CurrentAccount.Id); 
                    //        if (expectProducts.Count()> 0)
                    //        {
                    //             if (isInShoppingCart)
                    //             {
                    //                //加入购物车,提交订单，只要存在就不让生成待确认订单
                    //                throw new WineException("{0}每人只限购1套！购买数量已超".FormatWith(product.Name)); 
                    //             }
                    //            else if(expectProducts.Count()>1)
                    //            {
                    //                 //付款项只能有一个订单
                    //                throw new WineException("{0}每人只限购1套！购买数量已超".FormatWith(product.Name)); 
                    //            }
                    //        }
                    //}
                }
            }
            //else if (cartType == CartType.PreSale)
            //{
            //    var presellStockQuantity = EngineContext.Current.Resolve<IPresellService>().GetPresellProductByProductId(product.Id).SaleStock;

            //    if (quantity > presellStockQuantity)
            //    {
            //        throw new WineException("{0}商品库存不足！".FormatWith(product.Name));
            //    }
            //}
            else if (cartType == CartType.CrossBorder)
            {
                var cBPProduct = _cBPProductService.GetCBPProductByProductId(product.Id);
                {
                    if (cBPProduct == null || cBPProduct.State != ProductState.Published)
                    {
                        throw new WineException("{0}商品无效或未上架！".FormatWith(product.Name));
                    }
                    else if (cBPProduct.AvailableQuantity >= 0 && cBPProduct.AvailableQuantity < quantity)
                    {
                        throw new WineException("{0}商品可售数量不足！".FormatWith(product.Name));
                    }
                }
            }
        }


        /// <summary>
        /// 得到组合装的库存数
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        public int GetCombinationProductStockQuantity(int productId, bool isInShoppingCart = false)
        {
            int stockQuantity = 0;

            #region 通过获取组合装中关联的商品中库存最小的那个商品
            var combinationRelatedProductList = _productService.GetCombinationRelatedProductsByCombinationProductId(productId);

            //获取组合装中关联的商品中，库存最小的那个商品
            var stockQuantityMin_Product = combinationRelatedProductList.OrderBy(t => t.Product.StockQuantity).FirstOrDefault();

            if (stockQuantityMin_Product != null)
            {
                int tempNum = 0;

                if (isInShoppingCart)   //是否在购物车中判断库存，加上在购物车中商品数量
                {
                    var cart = _shopUnitOfWork.Get<ShoppingCart>().Where(t => t.ProductId == stockQuantityMin_Product.ProductId && t.State == CartState.NotToOrder && t.AccountId == _workContext.CurrentAccount.Id).FirstOrDefault();
                    if (cart != null)
                    {
                        tempNum = cart.Quantity;
                    }
                }

                stockQuantity = ((stockQuantityMin_Product.Product.IsCombination ? GetCombinationProductStockQuantity(stockQuantityMin_Product.ProductId) : stockQuantityMin_Product.Product.StockQuantity) - tempNum) / stockQuantityMin_Product.Count;
            }
            #endregion

            //int tempStockQuantity = 0;

            //var combinationRelatedProductModelList = _productService.GetCombinationRelatedProductModelListByCombinationProductId(productId);

            //if (combinationRelatedProductModelList != null && combinationRelatedProductModelList.Count > 0)
            //{
            //    var tempProduct = _productService.GetProductCacheById(combinationRelatedProductModelList[0].ProductId);

            //    int tempNum = 0;

            //    if (isInShoppingCart)   //是否在购物车中判断库存，加上在购物车中商品数量
            //    {
            //        var cart = _shopUnitOfWork.Get<ShoppingCart>().Where(t => t.ProductId == tempProduct.Id && t.State == CartState.NotToOrder && t.AccountId == _workContext.CurrentAccount.Id).FirstOrDefault();
            //        if (cart != null)
            //        {
            //            tempNum = cart.Quantity;
            //        }
            //    }

            //    stockQuantity = ((tempProduct.IsCombination ? GetCombinationProductStockQuantity(tempProduct.Id) : tempProduct.StockQuantity) - tempNum) / combinationRelatedProductModelList[0].Count;



            //    for (int i = 1; i < combinationRelatedProductModelList.Count; i++)
            //    {
            //        tempProduct = _productService.GetProductCacheById(combinationRelatedProductModelList[i].ProductId);

            //        tempNum = 0;

            //        if (isInShoppingCart)  //是否在购物车中判断库存，加上在购物车中商品数量
            //        {
            //            var cart = _shopUnitOfWork.Get<ShoppingCart>().Where(t => t.ProductId == tempProduct.Id && t.State == CartState.NotToOrder && t.AccountId == _workContext.CurrentAccount.Id).FirstOrDefault();
            //            if (cart != null)
            //            {
            //                tempNum = cart.Quantity;
            //            }
            //        }

            //        tempStockQuantity = ((tempProduct.IsCombination ? GetCombinationProductStockQuantity(tempProduct.Id) : (tempProduct.StockQuantity)) - tempNum) / combinationRelatedProductModelList[i].Count;



            //        if (stockQuantity > tempStockQuantity)
            //        {
            //            stockQuantity = tempStockQuantity;
            //        }
            //    }
            //}
            return stockQuantity;
        }

        /// <summary>
        /// 获得流水号(获得支付号)
        /// </summary>
        /// <returns></returns>
        public string GetOrderId(string preWord)
        {
            preWord = preWord + CommonTools.GetSerialNoHeadMin();
            lock (lockObj)
            {
                return _shopUnitOfWork.SqlQuery<string>("EXEC [proc_GetSerNo] @MaintainCate",
                 new System.Data.SqlClient.SqlParameter("MaintainCate", preWord)).SingleOrDefault<string>();
            }
        }

        /// <summary>
        /// 获得流水号(订单号)
        /// </summary>
        /// <returns></returns>
        public string GetOrderSerialNumber(string preWord)
        {
            preWord = preWord + CommonTools.GetSerialNoHead();
            lock (lockObj)
            {
                return _shopUnitOfWork.SqlQuery<string>("EXEC [proc_GetSerNo] @MaintainCate",
                 new System.Data.SqlClient.SqlParameter("MaintainCate", preWord)).SingleOrDefault<string>();
            }
        }

        public void SendCouponByOrderIdChannelCode(int orderId, string code)
        {
            SqlParameter[] paras = new SqlParameter[]
                {
                    new SqlParameter("orderId", orderId),
                    new SqlParameter("channelCode", code)
                };
            _shopUnitOfWork.ExecuteSqlCommand(
                "EXEC [GetCouponByOIdAndChannelCode] @orderId,@channelCode", parameters: paras);

        }

        /// <summary>
        /// Updates the order
        /// </summary>
        /// <param name="order">The order</param>
        public virtual void UpdateOrder(Shop.Data.Domain.Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            _shopUnitOfWork.Update<Order>(order);
            _shopUnitOfWork.SaveChanges();
            //event notification
            _eventPublisher.EntityUpdated(order);
        }

        /// <summary>
        /// 批量更新order,或全部更新或全部不更新
        /// </summary>
        /// <param name="orders"></param>
        public virtual void UpdateOrderList(IList<Shop.Data.Domain.Order> orders)
        {
            if (orders == null)
                throw new ArgumentNullException("order");
            if (orders.Count == 0)
                throw new ArgumentNullException("order");
            foreach (var o in orders)
            {
                _shopUnitOfWork.Update<Order>(o);
            }
            _shopUnitOfWork.SaveChanges();
        }

        /// <summary>
        /// 得到某订单中的组合装商品中的商品列表
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="combinationProductId"></param>
        /// <returns></returns>
        public virtual IList<OrderCombinationProduct> GetOrderCombinationProductsByOrderProductId(int orderProductId)
        {
            var query = from rp in _shopUnitOfWork.Get<OrderCombinationProduct>()
                        where rp.OrderProductId == orderProductId && rp.Isvalid
                        select rp;
            var orderCombinationProducts = query == null ? null : query.ToList();

            return orderCombinationProducts;
        }

        private CartType GetCartTypeByOrder(Order order)
        {
            switch (order.OrderType)
            {
                case 1:
                case 2:
                case 3:
                case 5:
                    return CartType.Usual;
                case 4: return CartType.CrossBorder;
                case 6: return CartType.PreSale;
                case 7: return CartType.Expect;
                default: return CartType.Usual;
            }
        }



        /// <summary>
        /// 判断该订单中的商品是否还有库存
        /// </summary>
        /// <param name="order"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool IsOrderProductsHasStockQuantity(Order order, out string message)
        {
            message = string.Empty;
            try
            {
                foreach (OrderProduct orderProduct in order.OrderProducts)
                {
                    ValidateProductSaleInfo(orderProduct.Product, orderProduct.Quantity, false, GetCartTypeByOrder(order));
                }
            }
            catch (WineException we)
            {
                message = string.Format("订单{0}中的", order.SerialNumber) + we.Message;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 获取该订单所能得到的中民积分（特权券）
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public int GetIntegrationValueByOrder(Order order)
        {
            var vipGradeType = _accountService.GetVipGradeType(order.AccountId);
            decimal tempMultiple = 0;

            switch (vipGradeType)
            {
                case VipGradeType.GeneralMember: { tempMultiple = VipGradeTypeName.GeneralMemberMultiple; } break;
                case VipGradeType.VipMember: { tempMultiple = VipGradeTypeName.VipMemberMultiple; } break;
                case VipGradeType.SilverMember: { tempMultiple = VipGradeTypeName.SilverMemberMultiple; } break;
                case VipGradeType.GoldMember: { tempMultiple = VipGradeTypeName.GoldMemberMultiple; } break;
                default: break;
            }

            int tempGetIntegrationValue = 0;

            foreach (OrderProduct orderProduct in order.OrderProducts)
            {

                var tempQuanValue = orderProduct.GetIntegrationValue * orderProduct.Quantity; //这里的渠道ID是否需要待验证
                tempGetIntegrationValue += tempQuanValue;
            }

            return tempGetIntegrationValue;
        }

        /// <summary>
        /// 支付成功
        /// </summary>
        /// <param name="order"></param>
        /// <param name="useWXCoupon">使用微信立减金 金额</param>
        public void PaySucess(IList<Order> orders, string tradeNo = "", string payNumber = "", bool fullVirtualMoney = false,int useWXCoupon=0)
        {
            if (!orders.HasItems())
                _logger.Error("支付成功订单错误，查询出订单为null, 交易流水号:{0}，paynumber:{1}".FormatWith(tradeNo,payNumber));

            // 全部用中民积分支付
            if (fullVirtualMoney)
            {
                var sumMoney = orders.Select(t => t.FactPrice).Sum();
                if (sumMoney != 0)
                {
                    _logger.Error("处理订单支付成功异常: 全部用中民积分支付但是 订单factprice>0！");
                    return;
                }
            }
            // int payState = _confirmBankPay.CheckBankPay(orders.ToList(), payNumber);//银行验证支付是否成功

            if (orders.Count > 0)
            {
                int minId = orders.Select(t => t.Id).Min();
                string key = "OrderPaySuccessLock_{0}".FormatWith(minId);
                try
                {
                    DictionaryBaseKeyLockEngine.Invoke(key, () =>
                    {
                        DelPaySuccess(orders, tradeNo, payNumber, 0, useWXCoupon);
                        // DelPaySuccess(orders, tradeNo, payNumber, payState);//银行验证支付是否成功
                        return string.Empty;
                    });
                }
                catch (Exception e)
                {
                    _logger.Error("处理订单支付成功异常！", e);
                }
            }


        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="orders"></param>
        /// <param name="tradeNo"></param>
        /// <param name="payNumber"></param>
        /// <param name="payState"></param>
        /// <param name="useWXCoupon">微信支付优惠（立减金）</param>
        private void DelPaySuccess(IList<Order> orders, string tradeNo = "", string payNumber = "", int payState = 0,int useWXCoupon = 0)
        {
            //int tempGetIntegrationValueSum = 0;
            //int tempIntegrationValueSum = 0;
            var workflowMessageService = EngineContext.Current.Resolve<IWorkflowMessageService>();
            AccountExtend account;
            if (orders.Count > 0)
            {
                account = _accountService.GetAccountExtendById(orders[0].AccountId);
            }
            else
            {
                return;
            }
            int index = 0; //循环项
            int canuseWXCoupon = useWXCoupon; //每个订单分配的立减金

            foreach (var order in orders.OrderBy(x=>x.FactPrice))
            {
                
                var lastOrder = GetOrderById(order.Id);// 重新获取最新订单信息（订单有可能被更改）
                if (lastOrder.State == OrderState.PaidNotCompleted || lastOrder.State == OrderState.Paid || lastOrder.State == OrderState.Complete || lastOrder.State == OrderState.PaidExceptionOrder || lastOrder.State == OrderState.RevokeOrder || lastOrder.State == OrderState.Shipped)
                {
                    return;
                }
                #region 处理订单逻辑
                //在此判断是否是 跨境电商 订单
                if (order.OrderType == (int)OrderStyle.CBP)
                {
                    //1、减 可销售数量
                    foreach (var p in order.OrderProducts)
                    {
                        var tempCBPProduct = _cBPProductService.GetCBPProductByProductId(p.ProductId);

                        if (tempCBPProduct != null)
                        {
                            tempCBPProduct.AvailableQuantity -= p.Quantity;

                            _cBPProductService.UpdateCBPProduct(tempCBPProduct);
                        }
                    }
                }
                else if (order.OrderType == (int)OrderStyle.Expect)
                {

                    foreach (var p in order.OrderProducts)
                    {
                        var expect = _productService.GetExpectProductByPId(p.ProductId);
                        if (expect != null)
                        {
                            string message = string.Empty;
                            //使用行级锁更新库存
                            if (!_productService.SubtractExpectProudctAvailableQuantity(expect.Id, p.Quantity, out message))
                            {
                                _logger.Error("更新期酒库出错！订单号：{0},返回信息：{1}，期酒id：{2}".FormatWith(order.SerialNumber, message, expect.Id));
                            };
                        }

                    }

                }
                else if (order.OrderType == (int)OrderStyle.PresellOrder)
                {
                    foreach (var p in order.OrderProducts)
                    {
                        var preproduct = _presellService.GetPresellProductByProductId(p.ProductId);
                        if (preproduct != null)
                        {
                            _presellService.SubtractOrderProductStock(p.ProductId, p.Quantity);
                        }
                    }
                }
                else if (order.OrderType == (int)OrderStyle.CFP)
                {
                    foreach (var p in order.OrderProducts)
                    {
                        var crowdFundingPlain = _crowdFundingService.GetCFPProductByProductId(p.ProductId);
                        var crowdFund = _crowdFundingService.GetCrowdFundingByCFPId(crowdFundingPlain.Id);
                        if (crowdFund == null)
                        {
                            var crowdFunding = new CrowdFunding()
                            {
                                CrowdFundingPlainId = crowdFundingPlain.Id,
                                PresellProductId = crowdFundingPlain.PresellProduct.Id,
                                StartDate = DateTime.Now,
                                EndDate = p.Quantity >= crowdFundingPlain.EndNum ? DateTime.Now : DateTime.Now.AddDays(crowdFundingPlain.Duration),
                                FundingNum = p.Quantity,
                                State = p.Quantity >= crowdFundingPlain.EndNum ? CrowdFundingState.EndFunding : CrowdFundingState.Underway,
                                CreatedBy = _workContext.CurrentAccount.Id
                            };
                            _shopUnitOfWork.Insert<CrowdFunding>(crowdFunding);

                            var crowdFundingOrderMap = new CrowdFundingOrderMap()
                            {
                                CrowdFundingId = crowdFunding.Id,
                                OrderId = order.Id,
                                CreatedBy = _workContext.CurrentAccount.Id
                            };
                            _shopUnitOfWork.Insert<CrowdFundingOrderMap>(crowdFundingOrderMap);

                            _shopUnitOfWork.SaveChanges();
                        }
                        else
                        {
                            crowdFund.FundingNum += p.Quantity;
                            if ((crowdFund.FundingNum + p.Quantity) >= crowdFundingPlain.EndNum)
                            {
                                crowdFund.EndDate = DateTime.Now;
                                crowdFund.State = CrowdFundingState.EndFunding;
                            }
                            _shopUnitOfWork.Update<CrowdFunding>(crowdFund);

                            var crowdFundingOrderMap = new CrowdFundingOrderMap()
                            {
                                CrowdFundingId = crowdFund.Id,
                                OrderId = order.Id,
                                CreatedBy = _workContext.CurrentAccount.Id
                            };
                            _shopUnitOfWork.Insert<CrowdFundingOrderMap>(crowdFundingOrderMap);
                            _shopUnitOfWork.SaveChanges();

                        }


                    }
                }
                else
                {
                    foreach (var p in order.OrderProducts)
                    {
                        if (p.Product.IsCombination) //是组合装商品,减少各自商品的库存
                        {
                            var orderCombinationProductList = GetOrderCombinationProductsByOrderProductId(p.Id);

                            for (int i = 0; i < orderCombinationProductList.Count; i++)
                            {
                                if (_productService.GetProductById(orderCombinationProductList[i].ProductId).LimitStockQuantity > 0)
                                {
                                    orderCombinationProductList[i].Product.LimitStockQuantity = orderCombinationProductList[i].Product.LimitStockQuantity - orderCombinationProductList[i].Quantity;
                                }
                                else
                                {
                                    orderCombinationProductList[i].Product.StockQuantity = orderCombinationProductList[i].Product.StockQuantity - orderCombinationProductList[i].Quantity;
                                }

                                _shopUnitOfWork.Update<OrderCombinationProduct>(orderCombinationProductList[i]);
                            }
                        }
                        else
                        {
                            if (EngineContext.Current.Resolve<IProductService>().IsPreSale(p.Product))
                            {
                                p.Product.LimitStockQuantity = p.Product.LimitStockQuantity - p.Quantity;
                            }
                            else
                            {
                                p.Product.StockQuantity = p.Product.StockQuantity - p.Quantity;
                                /*
                                if (p.Product.StockQuantity == 0||p.Order.OrderType==(int)OrderStyle.Usual)
                                {
                                    try
                                    {
                                        #region 从阿里云上面删除数据
                                        string message = "";
                                        bool isdeletepro = _luceneService.DeleteOtherChannelsProduct(p.Product, out message);
                                        if (!isdeletepro)
                                        {
                                            Tools.IOTools.LogText("PublishProduct Delete error text is:" + message);
                                        }
                                        #endregion
                                    }
                                    catch (Exception ex)
                                    {
                                        Tools.IOTools.LogText("从阿里云上面删除数据报错，程序报错信息为：" + ex.Message);
                                    }

                                    p.Product.State = ProductState.UnPublished;
                                    _productService.UpdateProduct(p.Product);
                                }*/
                            }
                        }

                        foreach (var gift in p.Gifts) // 减赠品库存
                        {
                            var product = _productService.GetProductById(gift.ProductId);
                            product.StockQuantity = product.StockQuantity - gift.Quantity;
                            _shopUnitOfWork.Update<Product>(product);
                        }

                        #region 加荣誉(注释掉 不加 荣誉未上线)
                        //foreach (var ph in p.Product.Product_HonorCategory_Mapping)
                        //{
                        //    var accountHonorCategory = _shopUnitOfWork.Get<AccountHonorCategoryMapping>().
                        //          Where(t => t.AccountId == account.Id && t.HonorCategoryId == ph.HonorCategoryId).FirstOrDefault();
                        //    if (accountHonorCategory == null)
                        //    {
                        //        _shopUnitOfWork.Insert<AccountHonorCategoryMapping>(new AccountHonorCategoryMapping()
                        //        {
                        //            AccountId = account.Id,
                        //            HonorValue = ph.HonorValue * p.Quantity,
                        //            HonorCategoryId = ph.HonorCategoryId,
                        //        });
                        //    }
                        //    else
                        //    {
                        //        accountHonorCategory.HonorValue += ph.HonorValue * p.Quantity;
                        //        _shopUnitOfWork.Update(accountHonorCategory);
                        //    }
                        //}
                        #endregion
                    }
                }
                //if (order.OrderType == (int)OrderStyle.CBP || order.OrderType == (int)OrderStyle.Usual)
                //{
                //    //order.GetIntegrationValue = (int)order.FactPrice; //这里实际支付多少钱就送多少中民积分，只限于现货和跨境电商
                //    order.GetIntegrationValue = GetIntegrationValueByOrder(order);  //这里获取订单里面商品的中民积分数量
                //}
                //else
                //{
                //    order.GetIntegrationValue = 0;
                //}
                //GetIntegrationValueByOrder(order);  
                #region 购买成功根据订单赠送中民返利网中民积分或者抵扣相应的中民积分
                if (order.GetIntegrationValue > 0)
                {
                    int iResult = 0;
                    string jifenresult1 = EngineContext.Current.Resolve<IWorkflowMessageService>().ChargingZM123JiFen(account.UserName, order, out iResult);
                    if (jifenresult1 != "true")
                    {

                        _logger.Error("中民积分接口报错用户名：" + account.UserName + ";订单号：" + order.SerialNumber + "返回报错代码：" + iResult + "信息：" + jifenresult1);//接口报错，记录接口报错信息
                    }
                    else
                    {
                        order.IsGetZMJiFen = true; //表示中民积分赠送成功
                    }
                }
                #endregion


                ////加减中民积分
                //tempGetIntegrationValueSum += order.GetIntegrationValue;
                //tempIntegrationValueSum += order.IntegralValue;


                //期酒订单(两次付款)
                if (order.OrderType == (int)OrderStyle.Expect)
                {

                    if (order.State == OrderState.NotPay)
                    {
                        order.State = OrderState.PaidNotCompleted;
                    }
                    else
                    {
                        //执行了多次  PaySuccess有问题
                        //if (order.State == OrderState.PaidNotCompleted)
                        //{
                        //    order.State = OrderState.Paid;
                        //}
                    }

                }
                else if (order.OrderType == (int)OrderStyle.CFP)
                {
                    if (order.State == OrderState.NotPay)
                    {
                        order.State = OrderState.PaidNotConfirm;
                    }
                }
                else
                {
                    order.State = OrderState.Paid;
                }
                //order.CheckPayState = payState;//TODO：银行支付状态
                order.PayDate = DateTime.Now;
                order.TradeNO = tradeNo;
                order.IsDeductionZmJifen = false;
                if (!payNumber.IsNullOrEmpty())
                {
                    order.PayNumber = payNumber;
                }

                #region 更新支付号支付信息

                if (tradeNo != string.Empty && payNumber != string.Empty)
                {
                    try
                    {
                        PayNumber model = GetPayNumberModelByPayNumber(payNumber);
                        if (model != null)
                        {
                            model.TradeNO = tradeNo;
                            UpdatePayNumber(model);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("更新支付宝/银行 流水号失败,方法报错，支付号：{0},流水号：{1},异常：{2}".FormatWith(payNumber, tradeNo, ex.Message));
                    }
                }

                #endregion 更新支付号支付信息

                if (order.ZMCoupon == 0 && order.WineCoupon == 0 && order.WineWorldCoupon == 0)
                {
                    order.IsDeductionZmJifen = true;
                }
                else if ((order.ZMCoupon > 0 || order.WineCoupon > 0 || order.WineWorldCoupon > 0) && !order.IsDeductionZmJifen)
                {
                    if (EngineContext.Current.Resolve<IWorkflowMessageService>().NotifyAll(order.Id))
                    {
                        order.IsDeductionZmJifen = true;
                    }
                }

                if (order.OrderType != (int)OrderStyle.CBP && order.OrderType != (int)OrderStyle.Expect && order.OrderType != (int)OrderStyle.PresellOrder)  // 非跨境电商订单
                {
                    #region 付款成功，记录使用的代金券，并更新订单—商品—代金券 关联记录

                    var tempUnInuseCouponCount = order.Order_Product_Coupons.Where(p => p.Coupon.Order_Product_Coupons.Where(t => t.OrderId != p.OrderId && p.IsPay).Count() > 0).Count();  //该订单中的不可用代金券

                    if (tempUnInuseCouponCount > 0) //存在代金券不是可以使用状态,说明已经失效,则该订单为异常订单
                    {
                        order.State = OrderState.PaidExceptionOrder;
                    }
                    else
                    {

                        // 核销微信卡券
                        System.Threading.Tasks.Task.Factory.StartNew(() => { EngineContext.Current.Resolve<IWorkflowMessageService>().ConsumeCouponByCoupons(order.Order_Product_Coupons); });

                        foreach (Order_Product_Coupon order_Product_Coupon in order.Order_Product_Coupons)
                        {
                            order_Product_Coupon.Coupon.State = CouponState.Purchased;
                            order_Product_Coupon.IsPay = true;
                            _shopUnitOfWork.Update<Order_Product_Coupon>(order_Product_Coupon);
                        }
                    }

                    #endregion 付款成功，记录使用的代金券，并更新代金券
                }

                #region 微信支付立减金
                if (useWXCoupon > 0)
                {
                    if (orders.Count <= (index + 1))
                    {
                        //最后一个订单的立减金 金额为剩下的
                        order.ReduceCost = (decimal)(canuseWXCoupon / 100.00);
                    }
                    else
                    {
                        if ((order.FactPrice * 100) > (useWXCoupon / orders.Count))
                        {
                            //订单均摊立减金 金额
                            order.ReduceCost = (decimal)(((int)(useWXCoupon / orders.Count)) / 100.00);
                            canuseWXCoupon = canuseWXCoupon - (int)(useWXCoupon / orders.Count);
                        }
                        else
                        {
                            //当订单实际金额比立减金平摊金额小时，订单消费的立减金 金额为订单实际金额（也就是该订单发票金额为0）
                            order.ReduceCost = order.FactPrice;
                            canuseWXCoupon = canuseWXCoupon - (int)(order.FactPrice * 100);
                        }
                    }
                }
                index++; 
                #endregion          

                order.Isvalid = true;
                _shopUnitOfWork.Update<Order>(order);

                #region 付款成功，电子提货码

                if (order.OrderType == (int)OrderStyle.ExchangedCard)
                {
                    var isExisted = false;
                    var giftCard = _productService.GenExchangeGiftCard(new List<Order>() { order }, out isExisted);
                    if (giftCard != null)
                    {
                        if (!isExisted)
                        {
                            var now = DateTime.Now;
                            EngineContext.Current.Resolve<ISystemMessageService>().InsertNewAccountSystemMessage(
                                "通知",
                                string.Format("恭喜，您收到“电子提货码”一枚，激活码是“{0}”，您可以（分享给好友）在“会员中心”下“<a style=\"color: #d10000;\" href=\"/account/giftcard\">我的电子提货码</a>”使用",
                                giftCard.UniqueCode.ToUpper()),
                                now,
                                now.AddYears(1),
                                order.AccountId);
                            var mobileSess = HttpContext.Current.Session["ValidatorMobile"];
                            if (mobileSess != null)
                            {
                                var mobile = mobileSess.ToString().Trim();
                                EngineContext.Current.Resolve<IWorkflowMessageService>().SendExchangeGiftCardCodeSuccess(
                                    order.AccountExtend,
                                    giftCard.UniqueCode.ToUpper(),
                                    mobile
                                    );
                            }
                        }
                        var orderIdOrGiftCardSerNum = order.Id.ToString();
                        //电子提货码 预扣库存
                        //SaledNotShipOrder(order);
                    }
                }

                #endregion 付款成功，电子提货码
                #region 集花系统作废 已注释
                /***********************集花系统作废 注释
                if (order.OrderType != (int)OrderStyle.CBP && order.OrderType != (int)OrderStyle.Expect && order.OrderType != (int)OrderStyle.PresellOrder)  // 非跨境电商订单
                {
                    #region 修改用户集花
                    try
                    {
                        UpdateUserJiHuaValue(order);
                    }
                    catch (Exception e)
                    {
                        _logger.Error("订单支付成功，处理用户集花异常！" + e.Message);
                    }
                    #endregion
                }
                 *************************/
                #endregion
                if (!order.BuyCode.IsNullOrEmpty())
                {
                    //表明该订单是购买码购买
                    var buycode = _buyProductCDKeyService.GetcdkeyByOrderId(order.Id);
                    buycode.CodeStatus = (int)CodeBuyStatus.Used;
                    _buyProductCDKeyService.UpdateBuyProductCDKey(buycode);
                }


                //判断该订单是否是自提订单
                #region
                if (order != null && order.State == OrderState.Paid && order.AddressId == 0)
                {
                    var order_OwnTakeWarehouse_Mapping = GetOrder_OwnTakeWarehouse_MappingByOrderId(order.Id);
                    if (order_OwnTakeWarehouse_Mapping == null)
                    {
                        continue;
                    }
                    //生成自提码，然后发送短信到自提人手机上
                    string ownTakeSmsCode = Tools.CommonTools.CreateNumberCode();
                    order_OwnTakeWarehouse_Mapping.OwnTakeSmsCode = ownTakeSmsCode;

                    _shopUnitOfWork.Update<Order_OwnTakeWarehouse_Mapping>(order_OwnTakeWarehouse_Mapping);

                    //发送自提码短信
                    int resultCode = workflowMessageService.SendOwnTakeSmsCodeWhenOwnTakeOrderShiped(order);

                    //判断是否是App端下的订单，若是，则个人消息中心记录和App推送一下
                    if (!string.IsNullOrEmpty(order.PlatformCode) && ("ios" == order.PlatformCode.ToLower() || "android" == order.PlatformCode.ToLower()))
                    {
                        //地址Id为0,则为自提订单,则发送自提提货码给自提人手机号上，并在个人消息中记录
                        var stationMessage = new StationMessage()
                        {
                            SendType = SendType.Personal,
                            Summary = "您的订单" + order.SerialNumber + "已付款,提货码已发送到了自提人的手机上,请注意查收.请于" + order_OwnTakeWarehouse_Mapping.OwnTakeTime + "时间上门提货,地址：" + GetOwnTakeWarehouseById(order_OwnTakeWarehouse_Mapping.OwnTakeWarehouseId).FullAddress + ".如有疑问,请拨打品酒师热线：4001000529.",
                            Content = "提货码已发送到了自提人的手机上,请注意查收.请于" + order_OwnTakeWarehouse_Mapping.OwnTakeTime + "时间上门提货.如有疑问,请拨打品酒师热线：4001000529.",
                            StarTime = DateTime.Now,
                            MessageType = MessageType.System,
                            EndTime = DateTime.Now.AddDays(3)
                        };
                        stationMessage.IsOutLinkurl = true;
                        stationMessage.AppAutoPush = true;
                        stationMessage.LinkUrl = "https://app.wine-world.com/account/paid";
                        stationMessage.DirectType = DirectType.Common;

                        stationMessage.Title = "您的订单" + order.SerialNumber + "已付款";

                        if (stationMessage.SendType == SendType.Personal && order.AccountId > 0)
                        {
                            stationMessage.AccountId = order.AccountId;
                        }
                        //新增站内消息
                        _stationMessageService.InsertStationMessage(stationMessage);

                        //进行App端通知消息推送
                        if (account != null)
                        {
                            //调用友盟推送接口给指定用户推送消息通知
                            SendAppCustomizedcast(
                               stationMessage.Id,
                               account.UserName,
                               stationMessage.Title,
                               stationMessage.Title,
                               stationMessage.Content,
                               stationMessage.DirectType.ToString().ToLower(),
                               stationMessage.LinkUrl,
                               "待收货订单",
                               order.PlatformCode
                              );
                        }
                    }
                }
                #endregion

                #endregion
            }
            #region 赠送中民积分或者减掉相应的中民积分  已注释
            //if (orders.Count() > 0 && tempIntegrationValueSum != 0 )
            //{                
            //    account.AccountIntegral -= tempIntegrationValueSum;
            //    _shopUnitOfWork.Update<AccountExtend>(account);
            //}           

            #endregion
            _shopUnitOfWork.SaveChanges();

            #region 关于推荐码，检测推荐人赠送一个推荐集花
            try
            {
                if (!EngineContext.Current.Resolve<IExtensionService>().IsExitBuyLog(account.UserName))
                {
                    AccountExtend otherAccount = _accountService.GetAccountExtendByRecommendCode(account.OtherRecommendCode);
                    if (otherAccount != null)
                    {
                        string orderstrs = "";
                        foreach (var iotem in orders)
                        {
                            orderstrs = orderstrs + iotem.SerialNumber;
                        }
                        EngineContext.Current.Resolve<IExtensionService>().InsertBuySuccessLog(account.UserName, orderstrs, otherAccount.UserName);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error("订单支付成功，处理用户 推荐集花 异常！" + e.Message);
            }
            #endregion
            try
            {
                //string channelCode = System.Configuration.ConfigurationManager.AppSettings["OrderGetCoupon"].ToString();

                foreach (var order in orders)
                {
                    if (order.OrderType != (int)OrderStyle.CBP && order.OrderType != (int)OrderStyle.Expect && order.OrderType != (int)OrderStyle.PresellOrder)  // 非跨境电商订单
                    {
                        foreach (var p in order.OrderProducts)
                        {
                            #region 查看存在的代金券，添加代金券
                            if (p.CouponGifts != null)
                            {
                                if (account.IsRegistered())
                                {
                                    //获取账号
                                    string usernamecoupon = "";
                                    usernamecoupon = account.UserName;
                                    foreach (var icoupon in p.CouponGifts)
                                    {
                                        for (int i = 0; i < icoupon.Quantity; i++)
                                        {
                                            //生成代金券，并绑定用户，然后设置代金券的状态和种类等等
                                            string returnCouponCode = "";
                                            bool result = _couponService.StayIssueCouponByChanelCode(icoupon.cctcm.ChanelCode, usernamecoupon, "来源于（支付）买赠活动：" + icoupon.GiftPromotions.Promotions.Name, out returnCouponCode);

                                            //将发放的代金券信息插入到OrderCoupons表中
                                            if (result)
                                            {
                                                _promotionsService.InsertCouponByOrderForGift(new OrderCoupons() { serialNumber = order.SerialNumber, SerialCode = returnCouponCode, CouponState = OrderCouponCategory.NoGive });

                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                p.CouponGifts = getopcg(p.Id);
                                //获取账号
                                string usernamecoupon = "";
                                usernamecoupon = account.UserName;
                                foreach (var icoupon in p.CouponGifts)
                                {
                                    for (int i = 0; i < icoupon.Quantity; i++)
                                    {
                                        //生成代金券，并绑定用户，然后设置代金券的状态和种类等等
                                        string returnCouponCode = "";
                                        bool result = _couponService.StayIssueCouponByChanelCode(icoupon.cctcm.ChanelCode, usernamecoupon, "来源于（支付）买赠活动：" + icoupon.GiftPromotions.Promotions.Name, out returnCouponCode);
                                        //将发放的代金券信息插入到OrderCoupons表中
                                        if (result)
                                        {
                                            _promotionsService.InsertCouponByOrderForGift(new OrderCoupons() { serialNumber = order.SerialNumber, SerialCode = returnCouponCode, CouponState = OrderCouponCategory.NoGive });
                                        }
                                    }
                                }
                            }
                            #endregion
                        }
                        #region 判断是否使用代金券，含有代金券将会进行中民接口的更新
                        foreach (Order_Product_Coupon order_Product_Coupon in order.Order_Product_Coupons)
                        {
                            //将代金券进行状态的回调，传送到中民接口进行状态更新
                            bool iszmState = _couponService.SetAlpacaCouponStateByCouponCode(order_Product_Coupon.Coupon.Id.ToString());
                        }
                        #endregion

                    }
                    #region creator :chenpeng 该订单来自返利网的用户所下，付款成功时，则需要反馈给返利网
                    if (order.RebateOrderProducts.Count > 0)
                    {
                        System.Threading.Tasks.Task.Factory.StartNew(() => { EngineContext.Current.Resolve<IWorkflowMessageService>().FeedBackToRebateWebSite(order); });
                        System.Threading.Tasks.Task.Factory.StartNew(() => { EngineContext.Current.Resolve<IWorkflowMessageService>().SendRebatePaySuccess(order); });

                    }
                    #endregion creator :chenpeng 该订单来自返利网的用户所下，付款成功时，则需要反馈给返利网
                    if (order.OrderType != (int)OrderStyle.ExchangedCard)
                    {
                        System.Threading.Tasks.Task.Factory.StartNew(() => { EngineContext.Current.Resolve<IWorkflowMessageService>().SendEmailWhenPaied(order); });
                        System.Threading.Tasks.Task.Factory.StartNew(() => { EngineContext.Current.Resolve<IWorkflowMessageService>().SendMobileMessageWhenPaied(order); });
                    }
                    //if (order.OrderType != (int)OrderStyle.CBP && order.OrderType != (int)OrderStyle.PresellOrder)
                    //{
                    //    EngineContext.Current.Resolve<IWorkflowMessageService>().TransferOrderJiuYeAsnyc(order, false);
                    //}

                    if (order.OrderType == (int)OrderStyle.CBP)//跨境电商订单下单
                    {
                        EngineContext.Current.Resolve<IWorkflowMessageService>().TransferCbpOrderAsnyc(order);
                    }

                    //支付成功赠送中民券,已取消
                    //if (order.FactPrice > 0 && order.ZMCouponPayGive == null)
                    //{
                    //    System.Threading.Tasks.Task.Factory.StartNew(() =>
                    //    {
                    //        decimal zmCouponGive = order.FactPrice * 0.01M;// 支付成功赠送中民劵金额
                    //        ZMCouponParmModel parmModel = new ZMCouponParmModel
                    //        {
                    //            couponType = ZMCCouponType.BuyGive,
                    //            OrderNumber = order.SerialNumber,
                    //            UserName = order.AccountExtend.UserName,
                    //            FactPrice = order.FactPrice,
                    //            ZMCoupon = zmCouponGive,
                    //            Memo = "红酒世界订单支付成功送中民券",
                    //            FromType = ZMCFromType.Give,
                    //        };
                    //        bool isSuccess = EngineContext.Current.Resolve<IWorkflowMessageService>().GiveZMCoupon(parmModel);
                    //        if (isSuccess)
                    //        {
                    //            order.ZMCouponPayGive = zmCouponGive;
                    //            UpdateOrder(order);
                    //        }
                    //    });
                    //}               

                    //如果期酒订单状态改为付款未完结，更新用户消费金额及会员等级(现货/跨境订单 需要订单完成才计入用户消费金额里)
                    if (order.OrderType == (int)OrderStyle.Expect && order.State == OrderState.PaidNotCompleted)
                    {
                        _accountService.UpdateAccountGrade(order.AccountId);

                    }

                    try
                    {
                        ////普通订单付款成功赠送代金券
                        //if (order.OrderType == (int)OrderStyle.Usual)
                        //{
                        //    System.Threading.Tasks.Task.Factory.StartNew(() => { SendCouponByOrderIdChannelCode(order.Id, channelCode); });
                        //}
                    }
                    catch (Exception e)
                    {
                        _logger.Error("订单支付成功,赠送代金券异常！", e);
                    }
                }
                #region 2017年9月9号到 10月10号 对所有第一次下单的用户送券
                //if (System.DateTime.Now > new DateTime(2017, 9, 9) && System.DateTime.Now < new DateTime(2017, 10, 11))
                //{
                //    try
                //    {
                //        int recordOrder = GetAccountOrderCountRecord(account.Id);
                //        if (recordOrder == 0)
                //        {
                //            string resultCode, resultMessage, returnCouponCode;
                //            _couponService.ActivateCouponByChanelCode("fbf4733e-5ee6-4d2a-8c88-f6f12e4080cd", account.UserName, "首单用户赠代金券代金券", out resultCode, out resultMessage, out returnCouponCode, couponNum: 6);
                //        }
                //    }
                //    catch (Exception e)
                //    {
                //        _logger.Error("首单用户赠送代金券异常！", e);
                //    }
                //}
                #endregion
            }
            catch (Exception e)
            {
                _logger.Error(string.Format("出错信息如下:{0}----内部异常：{1}", e.Message, e.InnerException != null ? e.InnerException.Message : string.Empty), e.InnerException != null ? e.InnerException : e);
            }
        }

        #region 友盟推送消息通知接口
        /// <summary>
        /// 推送app消息
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="alias"></param>
        /// <param name="ticker"></param>
        /// <param name="title"></param>
        /// <param name="type"></param>
        /// <param name="url"></param>
        private void SendAppCustomizedcast(int messageId, string alias, string ticker, string title, string text, string type, string url, string name, string ios_or_android)
        {
            if (ios_or_android != null && "android" == (ios_or_android.ToLower()))
            {
                //android
                SendAndroidCustomizedcast(messageId, alias, ticker, title, text, type, url, name);
            }
            else if (ios_or_android != null && "ios" == (ios_or_android.ToLower()))
            {
                //ios
                SendIOSCustomizedcast(messageId, alias, ticker, title, text, type, url, name);
            }
        }

        /// <summary>
        /// 推送Android App消息通知
        /// </summary>
        private void SendAndroidCustomizedcast(int messageId, string alias, string ticker, string title, string text, string type, string url, string name)
        {
            CustomizedcastMessage message = new CustomizedcastMessage();
            message.setMessageId(messageId);
            message.setAlias(alias);
            message.setTicker(ticker);
            message.setTitle(title);
            message.setText(text);
            message.setType(type);
            message.setUrl(url);
            message.setName(name);
            System.Threading.Tasks.Task.Factory.StartNew(() => { new UmengNotificationHelper().sendAndroidCustomizedcast(message); });
        }

        /// <summary>
        /// 推送IOS App消息通知
        /// </summary>
        private void SendIOSCustomizedcast(int messageId, string alias, string ticker, string title, string text, string type, string url, string name)
        {
            CustomizedcastMessage message = new CustomizedcastMessage();
            message.setMessageId(messageId);
            message.setAlias(alias);
            message.setTicker(ticker);
            message.setTitle(title);
            message.setText(text);
            message.setType(type);
            message.setUrl(url);
            message.setName(name);
            System.Threading.Tasks.Task.Factory.StartNew(() => { new UmengNotificationHelper().sendIOSCustomizedcast(message); });
        }
        #endregion

        /// <summary>
        /// 获得用户 30分钟之前的所有历史有效订单总数（支付金额大于0的有效单）
        /// </summary>
        /// <param name="accountid"></param>
        /// <returns></returns>
        public int GetAccountOrderCountRecord(int accountid)
        {
            string sql = @"select count(1) from dbo.[order] where [state] in(3,4,5,9,11) and  accountid=@accountid 
 and factprice> 0 and paydate<DATEADD(mi,-30,GETDATE())";
            SqlParameter parameter = new System.Data.SqlClient.SqlParameter("accountid", accountid);
            return _shopUnitOfWork.Context.Database.SqlQuery<int>(sql, parameter).FirstOrDefault();
        }
        #region 集花
        public void UpdateOrderJiHua(Order order, JiHua jihua)
        {
            if (order == null)
                return;

            string userName = _shopUnitOfWork.Get<AccountExtend>().Where(t => t.Id == order.AccountId).FirstOrDefault().UserName;

            int ytId = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["YTgongzai"]);
            int jbcId = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["Jbiaoce"]);
            int jbtId = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["Jbiaotie"]);

            var ytstock = _shopUnitOfWork.GetById<Product>(ytId).StockQuantity; // 1173 羊驼公仔的商品ID  获取羊驼公仔库存
            var jbcstock = _shopUnitOfWork.GetById<Product>(jbcId).StockQuantity; // 1174 酒标册的商品ID  获取酒标册库存
            var jbtstock = _shopUnitOfWork.GetById<Product>(jbtId).StockQuantity;// 1175 酒标贴的商品ID  获取酒标贴库存


            #region 羊驼公仔

            //int factGiftNumYT = jihua.ExchangeYtNum;// 实际配送数 与库存有关
            //int newfactJHYT = jihua.NewYTValue; // 赠送完后  羊驼集花数

            // 订单中有羊驼集花 或者 以往欠羊驼公仔
            if (jihua.CollectYTCount - jihua.CurrYTValue > 0 || jihua.ExchangeYtNum > 0)
            {
                if (ytstock < jihua.ExchangeYtNum)// 库存不足时
                {
                    jihua.ExchangeYtNum = 0;
                }

                var yt = _shopUnitOfWork.Get<OrderJiHuaGift>().Where(t => t.OrderId == order.Id && t.CollectType == CollectType.CollectYT && t.Isvalid).FirstOrDefault();
                if (yt == null)
                {
                    OrderJiHuaGift model = new OrderJiHuaGift()
                    {
                        CollectType = CollectType.CollectYT,
                        OrderId = order.Id,
                        Quantity = jihua.ExchangeYtNum,
                        OrderGiftNum = jihua.CollectYTCount - jihua.CurrYTValue,
                        IsPaid = false,
                        Isvalid = true
                    };

                    _shopUnitOfWork.Insert<OrderJiHuaGift>(model);
                }
                else
                {
                    yt.Quantity = jihua.ExchangeYtNum;
                    _shopUnitOfWork.Update<OrderJiHuaGift>(yt);
                }
            }

            #endregion

            #region 酒标册

            // 订单中有酒标册集花
            if (jihua.CollectJBCCount - jihua.CurrJBCValue > 0 || jihua.ExchangeJbcNum > 0)
            {
                if (jbcstock < jihua.NeedJBCnum || jbtstock < jihua.ExchangeJbcNum)// 库存不足时
                {
                    jihua.ExchangeJbcNum = 0;
                }

                var jbc = _shopUnitOfWork.Get<OrderJiHuaGift>().Where(t => t.OrderId == order.Id && t.CollectType == CollectType.CollectJBC && t.Isvalid).FirstOrDefault();
                if (jbc == null)
                {
                    OrderJiHuaGift model = new OrderJiHuaGift()
                    {
                        CollectType = CollectType.CollectJBC,
                        OrderId = order.Id,
                        Quantity = jihua.ExchangeJbcNum,
                        OrderGiftNum = jihua.CollectJBCCount - jihua.CurrJBCValue,
                        IsPaid = false
                    };
                    _shopUnitOfWork.Insert<OrderJiHuaGift>(model);
                }
                else
                {
                    jbc.Quantity = jihua.ExchangeJbcNum;
                    _shopUnitOfWork.Update<OrderJiHuaGift>(jbc);
                }

                //// 集花值有变动
                //if (newfactJHJBC != jihua.CurrJBCValue)
                //{
                //    UserCollectValue JBCValue = _shopUnitOfWork.Get<UserCollectValue>().Where(t => t.UserName == userName && t.CollectType == CollectType.CollectJBC && t.Isvalid).FirstOrDefault();
                //    // 修改集花值
                //    if (JBCValue == null)
                //    {
                //        UserCollectValue jbcvaluemodel = new UserCollectValue()
                //        {
                //            CollectType = CollectType.CollectJBC,
                //            UserName = userName,
                //            Value = newfactJHJBC
                //        };
                //        _shopUnitOfWork.Insert<UserCollectValue>(jbcvaluemodel);
                //    }
                //    else
                //    {
                //        JBCValue.Value = newfactJHJBC;
                //        _shopUnitOfWork.Update<UserCollectValue>(JBCValue);
                //    }
                //}
            }
            #endregion

            #region 酒标贴

            // 订单中有酒标贴集花 或者 以往欠 酒标贴
            if (jihua.CollectJBTCount - jihua.CurrJBTValue > 0 || jihua.ExchangeJbtNum > 0)
            {
                if (jbtstock < jihua.NeedJBTnum || jbcstock < jihua.ExchangeJbtNum)// 库存不足时
                {
                    jihua.ExchangeJbtNum = 0;
                }
                var jbt = _shopUnitOfWork.Get<OrderJiHuaGift>().Where(t => t.OrderId == order.Id && t.CollectType == CollectType.CollectJBT && t.Isvalid).FirstOrDefault();
                if (jbt == null)
                {
                    OrderJiHuaGift model = new OrderJiHuaGift()
                    {
                        CollectType = CollectType.CollectJBT,
                        OrderId = order.Id,
                        Quantity = jihua.ExchangeJbtNum,
                        OrderGiftNum = jihua.CollectJBTCount - jihua.CurrJBTValue,
                        IsPaid = false
                    };

                    _shopUnitOfWork.Insert<OrderJiHuaGift>(model);
                }
                else
                {
                    jbt.Quantity = jihua.ExchangeJbtNum;
                    _shopUnitOfWork.Update<OrderJiHuaGift>(jbt);
                }


                //// 集花值有变动
                //if (newfactJHJBT != jihua.CurrJBTValue)
                //{
                //    UserCollectValue JBTValue = _shopUnitOfWork.Get<UserCollectValue>().Where(t => t.UserName == userName && t.CollectType == CollectType.CollectJBT && t.Isvalid).FirstOrDefault();
                //    // 修改集花值
                //    if (JBTValue == null)
                //    {
                //        UserCollectValue jbtvaluemodel = new UserCollectValue()
                //        {
                //            CollectType = CollectType.CollectJBT,
                //            UserName = userName,
                //            Value = newfactJHJBT
                //        };
                //        _shopUnitOfWork.Insert<UserCollectValue>(jbtvaluemodel);
                //    }
                //    else
                //    {
                //        JBTValue.Value = newfactJHJBT;
                //        _shopUnitOfWork.Update<UserCollectValue>(JBTValue);
                //    }
                //}
            }

            #endregion

            _shopUnitOfWork.SaveChanges();
        }

        public void UpdateOrderJiHua(OrderJiHuaGift model)
        {
            _shopUnitOfWork.Update<OrderJiHuaGift>(model);
            _shopUnitOfWork.SaveChanges();
        }
        public IList<OrderJiHuaGift> GetOrderJiHuaGiftListByOrderId(int orderId)
        {
            return _shopUnitOfWork.Get<OrderJiHuaGift>().Where(t => t.OrderId == orderId).ToList();
        }

        /// <summary>
        /// 订单支付成功，修改用户集花数量
        /// </summary>
        /// <param name="order"></param>
        private void UpdateUserJiHuaValue(Order order)
        {
            if (order == null)
                return;

            string userName = _shopUnitOfWork.Get<AccountExtend>().Where(t => t.Id == order.AccountId).FirstOrDefault().UserName;
            JiHua jihua = new JiHua();
            GetJiHua(order, jihua);
            var lst = _shopUnitOfWork.Get<OrderJiHuaGift>().Where(t => t.OrderId == order.Id && !t.IsPaid && t.Isvalid);
            foreach (OrderJiHuaGift g in lst)
            {
                switch (g.CollectType)
                {
                    case CollectType.CollectYT:
                        UserCollectValue YTValue = _shopUnitOfWork.Get<UserCollectValue>().Where(t => t.UserName == userName && t.CollectType == CollectType.CollectYT && t.Isvalid).FirstOrDefault();
                        if (YTValue == null)
                        {
                            UserCollectValue ytvaluemodel = new UserCollectValue()
                            {
                                CollectType = CollectType.CollectYT,
                                UserName = userName,
                                Value = jihua.CollectYTCount - g.Quantity * 6
                            };
                            _shopUnitOfWork.Insert<UserCollectValue>(ytvaluemodel);
                        }
                        else
                        {
                            YTValue.Value = jihua.CollectYTCount - g.Quantity * 6;
                            YTValue.ModifiedTime = System.DateTime.Now;
                            _shopUnitOfWork.Update<UserCollectValue>(YTValue);
                        }
                        g.IsPaid = true;
                        break;
                    case CollectType.CollectJBC:
                        UserCollectValue JBCValue = _shopUnitOfWork.Get<UserCollectValue>().Where(t => t.UserName == userName && t.CollectType == CollectType.CollectJBC && t.Isvalid).FirstOrDefault();
                        if (JBCValue == null)
                        {
                            UserCollectValue jbcvaluemodel = new UserCollectValue()
                            {
                                CollectType = CollectType.CollectJBC,
                                UserName = userName,
                                Value = jihua.CollectJBCCount - g.Quantity * 36
                            };
                            _shopUnitOfWork.Insert<UserCollectValue>(jbcvaluemodel);
                        }
                        else
                        {
                            JBCValue.Value = jihua.CollectJBCCount - g.Quantity * 36;
                            JBCValue.ModifiedTime = System.DateTime.Now;
                            _shopUnitOfWork.Update<UserCollectValue>(JBCValue);
                        }
                        g.IsPaid = true;
                        break;
                    case CollectType.CollectJBT:
                        UserCollectValue JBTValue = _shopUnitOfWork.Get<UserCollectValue>().Where(t => t.UserName == userName && t.CollectType == CollectType.CollectJBT && t.Isvalid).FirstOrDefault();
                        if (JBTValue == null)
                        {
                            UserCollectValue jbtvaluemodel = new UserCollectValue()
                            {
                                CollectType = CollectType.CollectJBT,
                                UserName = userName,
                                Value = jihua.CollectJBTCount - g.Quantity * 12
                            };
                            _shopUnitOfWork.Insert<UserCollectValue>(jbtvaluemodel);
                        }
                        else
                        {
                            JBTValue.Value = jihua.CollectJBTCount - g.Quantity * 12;
                            JBTValue.ModifiedTime = System.DateTime.Now;
                            _shopUnitOfWork.Update<UserCollectValue>(JBTValue);
                        }
                        g.IsPaid = true;
                        break;
                }
            }
            _shopUnitOfWork.SaveChanges();
        }

        public void GetJiHua(Order order, JiHua model)
        {
            string userName = _shopUnitOfWork.Get<AccountExtend>(t => t.Id == order.AccountId).FirstOrDefault().UserName;
            var userJiHua = _shopUnitOfWork.Get<UserCollectValue>().Where(t => t.UserName == userName);

            var CollectYTmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectYT).FirstOrDefault();
            model.CurrYTValue = CollectYTmodel == null ? 0 : CollectYTmodel.Value;
            model.CollectYTCount = model.CurrYTValue;

            var CollectJBCmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectJBC).FirstOrDefault();
            model.CurrJBCValue = CollectJBCmodel == null ? 0 : CollectJBCmodel.Value;
            model.CollectJBCCount = model.CurrJBCValue;

            var CollectJBTmodel = userJiHua.Where(t => t.CollectType == Shop.Data.Domain.CollectType.CollectJBT).FirstOrDefault();
            model.CurrJBTValue = CollectJBTmodel == null ? 0 : CollectJBTmodel.Value;
            model.CollectJBTCount = model.CurrJBTValue;

            string type = string.Empty;
            foreach (var orderproduct in order.OrderProducts)
            {
                type = orderproduct.Product.CollectType;
                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }
                var types = type.Split(',');
                foreach (string t in types)
                {
                    var tt = t.Split('|');
                    if (tt[0] == "1")
                    {
                        model.CollectYTCount += Convert.ToInt32(tt[1]) * orderproduct.Quantity;
                    }
                    else if (tt[0] == "2")
                    {
                        model.CollectJBCCount += Convert.ToInt32(tt[1]) * orderproduct.Quantity;
                    }
                    else if (tt[0] == "3")
                    {
                        model.CollectJBTCount += Convert.ToInt32(tt[1]) * orderproduct.Quantity;
                    }
                }
            }
            // 羊驼公仔
            model.NeedYTnum = model.CollectYTCount / 6;
            //model.NewYTValue = model.CollectYTCount % 6;

            // 酒标册
            model.NeedJBCnum = model.CollectJBCCount / 36;
            //model.NewJBCValue = model.CollectJBCCount % 36;

            // 酒标贴
            model.NeedJBTnum = model.CollectJBTCount / 12;
            //model.NewJBTValue = model.CollectJBTCount % 12;

        }

        /// <summary>
        ///  新版 集花兑换方法 供视图调用
        /// </summary>
        /// <param name="model"></param>
        public void GetJiHua(OrderModel model, JiHua jihua)
        {
            var userJiHua = EngineContext.Current.Resolve<IUserCollectValueService>().GetValueByUserName(_workContext.CurrentAccount.UserName);

            var CollectYTmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectYT).FirstOrDefault();
            jihua.CurrYTValue = CollectYTmodel == null ? 0 : CollectYTmodel.Value;
            jihua.CollectYTCount = jihua.CurrYTValue;

            var CollectJBCmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectJBC).FirstOrDefault();
            jihua.CurrJBCValue = CollectJBCmodel == null ? 0 : CollectJBCmodel.Value;
            jihua.CollectJBCCount = jihua.CurrJBCValue;

            var CollectJBTmodel = userJiHua.Where(t => t.CollectType == Shop.Data.Domain.CollectType.CollectJBT).FirstOrDefault();
            jihua.CurrJBTValue = CollectJBTmodel == null ? 0 : CollectJBTmodel.Value;
            jihua.CollectJBTCount = jihua.CurrJBTValue;

            string type = string.Empty;
            foreach (var orderproduct in model.OrderProducts)
            {
                type = orderproduct.ProductModel.CollectType;
                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }
                var types = type.Split(',');
                foreach (string t in types)
                {
                    var tt = t.Split('|');
                    if (tt[0] == "1")
                    {
                        jihua.CollectYTCount += Convert.ToInt32(tt[1]) * orderproduct.Quantity;
                    }
                    else if (tt[0] == "2")
                    {
                        jihua.CollectJBCCount += Convert.ToInt32(tt[1]) * orderproduct.Quantity;
                    }
                    else if (tt[0] == "3")
                    {
                        jihua.CollectJBTCount += Convert.ToInt32(tt[1]) * orderproduct.Quantity;
                    }
                }
            }
            //总共可兑换 羊驼公仔 数
            jihua.NeedYTnum = jihua.CollectYTCount / 6;

            // 总共可兑换 酒标册数
            jihua.NeedJBCnum = jihua.CollectJBCCount / 36;

            //总共可兑换  酒标贴数
            jihua.NeedJBTnum = jihua.CollectJBTCount / 12;

            var jihuagifts = _shopUnitOfWork.Get<OrderJiHuaGift>().Where(t => t.OrderId == model.Id && t.Isvalid);
            foreach (var jihuagift in jihuagifts)
            {
                switch (jihuagift.CollectType)
                {
                    case CollectType.CollectYT:
                        jihua.ExchangeYtNum = jihuagift.Quantity;
                        break;
                    case CollectType.CollectJBC:
                        jihua.ExchangeJbcNum = jihuagift.Quantity;
                        break;
                    case CollectType.CollectJBT:
                        jihua.ExchangeJbtNum = jihuagift.Quantity;
                        break;
                }
            }

        }

        /// <summary>
        /// 供视图调用
        /// </summary>
        /// <param name="model"></param>
        public void GetJiHua(CartListModel model, JiHua jihua)
        {

            var userJiHua = EngineContext.Current.Resolve<IUserCollectValueService>().GetValueByUserName(_workContext.CurrentAccount.UserName);

            var CollectYTmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectYT).FirstOrDefault();
            jihua.CurrYTValue = CollectYTmodel == null ? 0 : CollectYTmodel.Value;
            jihua.CollectYTCount = jihua.CurrYTValue;

            var CollectJBCmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectJBC).FirstOrDefault();
            jihua.CurrJBCValue = CollectJBCmodel == null ? 0 : CollectJBCmodel.Value;
            jihua.CollectJBCCount = jihua.CurrJBCValue;

            var CollectJBTmodel = userJiHua.Where(t => t.CollectType == Shop.Data.Domain.CollectType.CollectJBT).FirstOrDefault();
            jihua.CurrJBTValue = CollectJBTmodel == null ? 0 : CollectJBTmodel.Value;
            jihua.CollectJBTCount = jihua.CurrJBTValue;

            string type = string.Empty;
            foreach (var cart in model.CartDetailList)
            {
                type = cart.Product.CollectType;

                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }
                var types = type.Split(',');
                foreach (string t in types)
                {
                    var tt = t.Split('|');
                    if (tt[0] == "1")
                    {
                        jihua.CollectYTCount += Convert.ToInt32(tt[1]) * cart.Quantity;
                    }
                    else if (tt[0] == "2")
                    {
                        jihua.CollectJBCCount += Convert.ToInt32(tt[1]) * cart.Quantity;
                    }
                    else if (tt[0] == "3")
                    {
                        jihua.CollectJBTCount += Convert.ToInt32(tt[1]) * cart.Quantity;
                    }
                }
            }
            // 羊驼
            jihua.NeedYTnum = jihua.CollectYTCount / 6;
            //jihua.NewYTValue = jihua.CollectYTCount % 6;

            // 酒标册
            jihua.NeedJBCnum = jihua.CollectJBCCount / 36;
            //jihua.NewJBCValue = jihua.CollectJBCCount % 36;

            // 酒标贴
            jihua.NeedJBTnum = jihua.CollectJBTCount / 12;
            //jihua.NewJBTValue = jihua.CollectJBTCount % 12;
        }

        public void GetJiHua(IList<CartModel> cartList, JiHua jihua)
        {
            var userJiHua = EngineContext.Current.Resolve<IUserCollectValueService>().GetValueByUserName(_workContext.CurrentAccount.UserName);

            var CollectYTmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectYT).FirstOrDefault();
            jihua.CurrYTValue = CollectYTmodel == null ? 0 : CollectYTmodel.Value;
            jihua.CollectYTCount = jihua.CurrYTValue;

            var CollectJBCmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectJBC).FirstOrDefault();
            jihua.CurrJBCValue = CollectJBCmodel == null ? 0 : CollectJBCmodel.Value;
            jihua.CollectJBCCount = jihua.CurrJBCValue;

            var CollectJBTmodel = userJiHua.Where(t => t.CollectType == Shop.Data.Domain.CollectType.CollectJBT).FirstOrDefault();
            jihua.CurrJBTValue = CollectJBTmodel == null ? 0 : CollectJBTmodel.Value;
            jihua.CollectJBTCount = jihua.CurrJBTValue;

            string type = string.Empty;
            foreach (var cart in cartList)
            {
                type = cart.Product.CollectType;

                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }
                var types = type.Split(',');
                foreach (string t in types)
                {
                    var tt = t.Split('|');
                    if (tt[0] == "1")
                    {
                        jihua.CollectYTCount += Convert.ToInt32(tt[1]) * cart.Quantity;
                    }
                    else if (tt[0] == "2")
                    {
                        jihua.CollectJBCCount += Convert.ToInt32(tt[1]) * cart.Quantity;
                    }
                    else if (tt[0] == "3")
                    {
                        jihua.CollectJBTCount += Convert.ToInt32(tt[1]) * cart.Quantity;
                    }
                }
            }
            // 羊驼
            jihua.NeedYTnum = jihua.CollectYTCount / 6;
            //jihua.NewYTValue = jihua.CollectYTCount % 6;

            // 酒标册
            jihua.NeedJBCnum = jihua.CollectJBCCount / 36;
            //jihua.NewJBCValue = jihua.CollectJBCCount % 36;

            // 酒标贴
            jihua.NeedJBTnum = jihua.CollectJBTCount / 12;
            //jihua.NewJBTValue = jihua.CollectJBTCount % 12;
        }

        /// <summary>
        /// 新版 集花兑换方法
        /// </summary>
        /// <param name="model"></param>
        public void GetJiHua(IList<ShoppingCart> model, JiHua jihua)
        {
            var userJiHua = EngineContext.Current.Resolve<IUserCollectValueService>().GetValueByUserName(_workContext.CurrentAccount.UserName);

            var CollectYTmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectYT).FirstOrDefault();
            jihua.CurrYTValue = CollectYTmodel == null ? 0 : CollectYTmodel.Value;
            jihua.CollectYTCount = jihua.CurrYTValue;

            var CollectJBCmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectJBC).FirstOrDefault();
            jihua.CurrJBCValue = CollectJBCmodel == null ? 0 : CollectJBCmodel.Value;
            jihua.CollectJBCCount = jihua.CurrJBCValue;

            var CollectJBTmodel = userJiHua.Where(t => t.CollectType == Shop.Data.Domain.CollectType.CollectJBT).FirstOrDefault();
            jihua.CurrJBTValue = CollectJBTmodel == null ? 0 : CollectJBTmodel.Value;
            jihua.CollectJBTCount = jihua.CurrJBTValue;

            string type = string.Empty;
            foreach (var cart in model)
            {
                type = cart.Product.CollectType;

                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }
                var types = type.Split(',');
                foreach (string t in types)
                {
                    var tt = t.Split('|');
                    if (tt[0] == "1")
                    {
                        jihua.CollectYTCount += Convert.ToInt32(tt[1]) * cart.Quantity;
                    }
                    else if (tt[0] == "2")
                    {
                        jihua.CollectJBCCount += Convert.ToInt32(tt[1]) * cart.Quantity;
                    }
                    else if (tt[0] == "3")
                    {
                        jihua.CollectJBTCount += Convert.ToInt32(tt[1]) * cart.Quantity;
                    }
                }
            }
            // 羊驼
            jihua.NeedYTnum = jihua.CollectYTCount / 6;
            //jihua.NewYTValue = jihua.CollectYTCount % 6;

            // 酒标册
            jihua.NeedJBCnum = jihua.CollectJBCCount / 36;
            //jihua.NewJBCValue = jihua.CollectJBCCount % 36;

            // 酒标贴
            jihua.NeedJBTnum = jihua.CollectJBTCount / 12;
            //jihua.NewJBTValue = jihua.CollectJBTCount % 12;
        }

        /// <summary>
        /// 新版 集花兑换方法
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="num"></param>
        /// <param name="jihua"></param>
        public void GetJiHua(int productId, int num, JiHua jihua)
        {
            var userJiHua = EngineContext.Current.Resolve<IUserCollectValueService>().GetValueByUserName(_workContext.CurrentAccount.UserName);

            var CollectYTmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectYT).FirstOrDefault();
            jihua.CurrYTValue = CollectYTmodel == null ? 0 : CollectYTmodel.Value;
            jihua.CollectYTCount = jihua.CurrYTValue;

            var CollectJBCmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectJBC).FirstOrDefault();
            jihua.CurrJBCValue = CollectJBCmodel == null ? 0 : CollectJBCmodel.Value;
            jihua.CollectJBCCount = jihua.CurrJBCValue;

            var CollectJBTmodel = userJiHua.Where(t => t.CollectType == Shop.Data.Domain.CollectType.CollectJBT).FirstOrDefault();
            jihua.CurrJBTValue = CollectJBTmodel == null ? 0 : CollectJBTmodel.Value;
            jihua.CollectJBTCount = jihua.CurrJBTValue;

            string type = string.Empty;
            var product = EngineContext.Current.Resolve<IProductService>().GetProductCacheById(productId);

            type = product.CollectType;

            if (!string.IsNullOrEmpty(type))
            {
                var types = type.Split(',');
                foreach (string t in types)
                {
                    var tt = t.Split('|');
                    if (tt[0] == "1")
                    {
                        jihua.CollectYTCount += Convert.ToInt32(tt[1]) * num;
                    }
                    else if (tt[0] == "2")
                    {
                        jihua.CollectJBCCount += Convert.ToInt32(tt[1]) * num;
                    }
                    else if (tt[0] == "3")
                    {
                        jihua.CollectJBTCount += Convert.ToInt32(tt[1]) * num;
                    }
                }
            }
            // 羊驼
            jihua.NeedYTnum = jihua.CollectYTCount / 6;
            //jihua.NewYTValue = jihua.CollectYTCount % 6;

            // 酒标册
            jihua.NeedJBCnum = jihua.CollectJBCCount / 36;
            //jihua.NewJBCValue = jihua.CollectJBCCount % 36;

            // 酒标贴
            jihua.NeedJBTnum = jihua.CollectJBTCount / 12;
            //jihua.NewJBTValue = jihua.CollectJBTCount % 12;
        }

        /// <summary>
        ///  根据加密后的 订单号 校验用户的集花礼品数 ( 当前用户)
        /// </summary>
        /// <param name="model"></param>
        public void GetJiHua(string serialNumbers, JiHua jihua)
        {
            var userJiHua = EngineContext.Current.Resolve<IUserCollectValueService>().GetValueByUserName(_workContext.CurrentAccount.UserName);

            var CollectYTmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectYT).FirstOrDefault();
            var CollectJBCmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectJBC).FirstOrDefault();
            var CollectJBTmodel = userJiHua.Where(t => t.CollectType == CollectType.CollectJBT).FirstOrDefault();
            jihua.CurrYTValue = CollectYTmodel == null ? 0 : CollectYTmodel.Value;
            jihua.CurrJBCValue = CollectJBCmodel == null ? 0 : CollectJBCmodel.Value;
            jihua.CurrJBTValue = CollectJBTmodel == null ? 0 : CollectJBTmodel.Value;
            jihua.CollectYTCount = jihua.CurrYTValue;
            jihua.CollectJBCCount = jihua.CurrJBCValue;
            jihua.CollectJBTCount = jihua.CurrJBTValue;

            foreach (var number in serialNumbers.Split(','))
            {
                var order = GetOrderByNumber(AESHelper.AESDecrypt(number, _workContext.CurrentAccount.Passwordsalt));

                string type = string.Empty;
                foreach (var orderproduct in order.OrderProducts)
                {
                    type = orderproduct.Product.CollectType;
                    if (string.IsNullOrEmpty(type))
                    {
                        continue;
                    }
                    var types = type.Split(',');
                    foreach (string t in types)
                    {
                        var tt = t.Split('|');
                        if (tt[0] == "1")
                        {
                            jihua.CollectYTCount += Convert.ToInt32(tt[1]) * orderproduct.Quantity;
                        }
                        else if (tt[0] == "2")
                        {
                            jihua.CollectJBCCount += Convert.ToInt32(tt[1]) * orderproduct.Quantity;
                        }
                        else if (tt[0] == "3")
                        {
                            jihua.CollectJBTCount += Convert.ToInt32(tt[1]) * orderproduct.Quantity;
                        }
                    }
                }
                var jihuagifts = _shopUnitOfWork.Get<OrderJiHuaGift>().Where(t => t.OrderId == order.Id && t.Isvalid);
                foreach (var jihuagift in jihuagifts)
                {
                    switch (jihuagift.CollectType)
                    {
                        case CollectType.CollectYT:
                            jihua.ExchangeYtNum += jihuagift.Quantity;
                            break;
                        case CollectType.CollectJBC:
                            jihua.ExchangeJbcNum += jihuagift.Quantity;
                            break;
                        case CollectType.CollectJBT:
                            jihua.ExchangeJbtNum += jihuagift.Quantity;
                            break;
                    }
                }
            }
            //总共可兑换 羊驼公仔 数
            jihua.NeedYTnum = jihua.CollectYTCount / 6;

            // 总共可兑换 酒标册数
            jihua.NeedJBCnum = jihua.CollectJBCCount / 36;

            //总共可兑换  酒标贴数
            jihua.NeedJBTnum = jihua.CollectJBTCount / 12;
        }
        #endregion


        /// <summary>
        /// 根据订单的商品ID获取代金券赠品
        /// </summary>
        /// <param name="opID">OrderProductorID</param>
        /// <returns>代金券集合</returns>
        public ICollection<OrderProductCouponGifts> getopcg(int opID)
        {
            var query = _shopUnitOfWork.Get<OrderProductCouponGifts>().Where(t => t.OrderProductId == opID);
            return query.ToList();
        }


        /***********
        public bool GiveBackStock(int orderId)
        {
            //返还预留库存
            Shop.Ref.Levinelite.OpenOrder levinelite = new Shop.Ref.Levinelite.OpenOrder();
            Order order = _shopUnitOfWork.GetById<Order>(orderId);
            //以下三者条件满足才返库存
            //扣库存完成
            //订单是支付完成状态
            //是换购订单
            if (order.StockChangeStatus != 1 || order.State != OrderState.Paid || order.OrderType != 2)
            {
                return false;
            }
            var serialNumber = order.SerialNumber;
            var errorStr = string.Empty;
            var requestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TransData>
  <Head>
    <Desc>返还库存</Desc>
    <Channel>7</Channel>
    <SerialNumber>{0}</SerialNumber>
    <UserId>{1}</UserId>
  </Head>
</TransData>".Replace("\r\n", " ").FormatWith(serialNumber, HongJiuCustomerId);
            try
            {
                XmlDocument xml = new XmlDocument();
                var responseXml = levinelite.GiveBackStock(requestXml);
                xml.LoadXml(responseXml);
                var result = xml.SelectSingleNode("TransData/Body/Result").InnerText;
                if (result != "1")
                {
                    errorStr = "电子提货码 返还库存失败！OrderId或SerialNumber号：{0}，发送信息： {2}， 返回信息{1}".FormatWith(serialNumber, responseXml, requestXml);
                }
            }
            catch (Exception ex)
            {
                errorStr = "电子提货码 返还库存失败！OrderId或SerialNumber号：{0},异常信息：{1}, 发送信息：{2}, 错误信息：{3}".FormatWith(serialNumber, ex.Message, requestXml, ex.ToString());
            }
            if (!string.IsNullOrEmpty(errorStr))
            {
                _logger.Error(errorStr);
                return false;
            }
            else
            {
                //1预扣库存成功
                //2预扣库存成功后的返还库存成功
                order.StockChangeStatus = 2;
                _shopUnitOfWork.Update<Order>(order);
                _shopUnitOfWork.SaveChanges();
                return true;
            }
        private bool SaledNotShipOrder(Order order)
        {
            //以下三者条件满足才扣库存
            //没有扣库存
            //订单是支付完成状态
            //是换购订单
            if (order.StockChangeStatus != 0 || order.State != OrderState.Paid || order.OrderType != 2)
            {
                return false;
            }
            var strProducts = new List<string>();
            var strNumbers = new List<string>();
            var strTypes = string.Empty;
            order.OrderProducts.Each(x =>
            {
                if (x.Product.IsCombination) //是组合装商品
                {
                    var OrderCombinationProducts = GetOrderCombinationProductsByOrderProductId(x.Id);

                    OrderCombinationProducts.Each(p =>
                    {
                        strProducts.Add(p.Product.JiuYeProductId.ToString());
                        strNumbers.Add(p.Quantity.ToString());
                        strTypes += "0,";
                    });
                }
                else
                {
                    strProducts.Add(x.Product.JiuYeProductId.ToString());
                    strNumbers.Add(x.Quantity.ToString());
                    strTypes += "0,";
                }

                x.Gifts.Each(g =>
                {
                    strProducts.Add(g.Product.JiuYeProductId.ToString());
                    strNumbers.Add(g.Quantity.ToString());
                    strTypes += "1,";
                });
            });

            order.ToTotalGifts = GetToTotalGiftsByOrderId(order.Id);
            if (order.ToTotalGifts != null && order.ToTotalGifts.Count > 0)
            {
                order.ToTotalGifts.Each(g =>
                {
                    strProducts.Add(g.Product.JiuYeProductId.ToString());
                    strNumbers.Add(g.Quantity.ToString());
                    strTypes += "1,";
                });
            }
            Shop.Ref.Levinelite.OpenOrder levinelite = new Shop.Ref.Levinelite.OpenOrder();
            var errorStr = string.Empty;
            var serialNumber = order.SerialNumber;
            var requestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TransData>
  <Head>
    <Desc>预扣库存</Desc>
    <Channel>7</Channel>
    <SerialNumber>{0}</SerialNumber>
    <UserId>{1}</UserId>
  </Head>
  <Body>
    <Product>
      <PId>{2}</PId>
      <Number>{3}</Number>
    </Product>
  </Body>
</TransData>".Replace("\r\n", " ").FormatWith(serialNumber,
             HongJiuCustomerId,
             string.Join(",", strProducts),
             string.Join(",", strNumbers));
            try
            {
                XmlDocument xml = new XmlDocument();
                var responseXml = levinelite.SaledNotShipOrder(requestXml);
                xml.LoadXml(responseXml);
                var result = xml.SelectSingleNode("TransData/Body/Result").InnerText;
                if (result != "1")
                {
                    errorStr = "电子提货码 预扣库存失败！OrderId或SerialNumber号：{0}，发送信息： {2}， 返回信息{1}".FormatWith(serialNumber, responseXml, requestXml);
                }
            }
            catch (Exception ex)
            {
                errorStr = "电子提货码 预扣库存失败！OrderId或SerialNumber号：{0},异常信息：{1}, 发送信息：{2}, 错误信息：{3}".FormatWith(serialNumber, ex.Message, requestXml, ex.ToString());
            }
            if (!string.IsNullOrEmpty(errorStr))
            {
                _logger.Error(errorStr);
                return false;
            }
            else
            {
                //1预扣库存成功
                //2预扣库存成功后的返还库存成功
                order.StockChangeStatus = 1;
                _shopUnitOfWork.Update<Order>(order);
                _shopUnitOfWork.SaveChanges();
                return true;
            }
        }
        
         public bool SaledNotShipOrder(int orderId)
        {
            Order order = _shopUnitOfWork.GetById<Order>(orderId);
            return SaledNotShipOrder(order);
        }
         *************/

        /// <summary>
        /// 合并付款
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="payment"></param>
        public void MergerPayOrder(string serialNumber, PaymentType paymentType, out string payNumber, out decimal payMoney, out string subject, string platform = null)
        {
            if (paymentType == null)
                throw new Exception("未选择付款方式！");
            payMoney = 0;
            subject = string.Empty;
            payNumber = GetOrderSerialNumber("ZF"); //GetOrderId("ZF");
            PayNumber payNumberModel = new PayNumber
            {
                PayNo = payNumber
            };
            InsertPayNumber(payNumberModel);
            foreach (var number in serialNumber.Split(','))
            {
                var order = GetOrderByNumber(AESHelper.AESDecrypt(number, _workContext.CurrentAccount.Passwordsalt));
                order.PayNumber = payNumber;
                /// 未支付
                if (order != null && order.AccountId == _workContext.CurrentAccount.Id
                    && order.State == OrderState.NotPay)
                {
                    order.PaymentTypeId = (paymentType.Code == "WXZF" && platform == "APP") ? 42 : paymentType.Id;// app 且是微信支付则取APP的微信支付
                    order.Payment = Payment.PayAll;
                    payMoney += order.FactPrice;
                    subject += string.Format("{0}({1})...,",
                        order.OrderProducts.First().Product.Name,
                        order.OrderProducts.First().Quantity);
                }

                Order_PayNumber_Mapping map = new Order_PayNumber_Mapping()
                {
                    Isvalid = true,
                    ModifiedTime = System.DateTime.Now,
                    OrderId = order.Id,
                    PaymentTypeId = (paymentType.Code == "WXZF" && platform == "APP") ? 42 : paymentType.Id,// app 且是微信支付则取APP的微信支付
                    PayNumberId = payNumberModel.Id,
                    CreatedTime = System.DateTime.Now
                };
                _shopUnitOfWork.Insert<Order_PayNumber_Mapping>(map);
                _shopUnitOfWork.Update<Order>(order);
            }
            subject = subject.TrimEnd(',').Length > 50 ? subject.Substring(0, 50) : subject;
            _shopUnitOfWork.SaveChanges();
        }

        public void CheckOrderAndUpdateByPayGift(Order order, PaymentType paymentType)
        {
            if (paymentType == null)
                throw new Exception("未选择付款方式！");
            bool ISchange = false;
            string payTypeOne = "";
            /// 未支付
            if (order != null && order.AccountId == _workContext.CurrentAccount.Id
                && order.State == OrderState.NotPay)
            {
                //先去判断是否存在支付的方式
                foreach (var item in order.OrderProducts)
                {
                    //vip折扣价和活动不同享
                    if (item.DiscountType == DiscountType.Promotion && Tools.IOTools.IsPaycodeInPaycodes(paymentType.Code, _promotionsService.GetPayGiftTypeByPayChange(item)))
                    {
                        //如果存在该支付方式的优惠活动
                        IList<GiftModel> giftdata = _promotionsService.GetPayGiftByPayChange(item);
                        foreach (var gd in giftdata)
                        {
                            payTypeOne = _promotionsService.GetPayGiftTypeByPayChange(item).ToUpper();
                            ISchange = true;
                            item.Gifts.Add(new OrderProductGifts()
                            {
                                GiftPromotionsId = gd.GiftPromotionsId,
                                ProductId = gd.ProductId,
                                Quantity = gd.Quantity,
                            });
                        }
                        IList<GiftCouponModel> gifcoupon = _promotionsService.GetPayGiftCouponByPayChange(item);
                        foreach (var gc in gifcoupon)
                        {
                            payTypeOne = _promotionsService.GetPayGiftTypeByPayChange(item).ToUpper();
                            ISchange = true;
                            item.CouponGifts.Add(new OrderProductCouponGifts()
                            {
                                Quantity = gc.CouponQuantity,
                                ShowCouponName = gc.CouponName,
                                GiftPromotionsId = gc.GiftPromotionsId,
                                cctcm = gc.CouponCategoryTCMs.FirstOrDefault(),
                                CouponCategory_Type_Chanel_MappingID = gc.CouponCategoryTCMs.FirstOrDefault().Id,
                                CouponChanelCode = gc.CouponCategoryTCMs.FirstOrDefault().ChanelCode
                            });
                        }
                    }
                }
                _shopUnitOfWork.Update<Order>(order);
            }
            _shopUnitOfWork.SaveChanges();
            if (ISchange)
            {
                if (!CheckIsPayGiftOrder(order.SerialNumber))
                {
                    //表中插入数据，该订单已经和支付方式绑定！
                    InsertPayGiftOrder(new PayGiftOrder() { SerialNumber = order.SerialNumber, PayTypeCode = payTypeOne });
                }
            }
        }

        /// <summary>
        /// 获得用户支付金额
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        ///

        public decimal GetPayMoney(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            return order.FactPrice;
        }

        /// <summary>
        /// 根据订单状态 获取当天的订单数
        /// </summary>
        /// <param name="orderState"></param>
        /// <returns></returns>
        public virtual int GetTodayOrderCountByStatus(OrderState orderState)
        {
            var query = _shopUnitOfWork.Get<Order>();
            query = query.Where(o => o.Isvalid && o.CreatedTime > System.DateTime.Today && o.State == orderState);

            return query.Count();
        }

        public IList<Order> GetTopOrdersByAccountId(int accountId, int top)
        {
            return _shopUnitOfWork.Get<Order>()
                .Where(p => p.AccountId == accountId && p.Isvalid)
                .OrderByDescending(p => p.OrderGenerateDate).Take(top).ToList();
        }

        public IList<Order> GetListPageByState(int accountId, int pageIndex, int pageSize, OrderState state)
        {
            return _shopUnitOfWork.Get<Order>()
                .Where(p => p.AccountId == accountId && p.Isvalid && p.State == state)
                .OrderByDescending(p => p.OrderGenerateDate).Skip(pageSize * (pageIndex - 1)).Take(pageSize).ToList();
        }

        public IList<Order> GetListPageByState(int accountId, int pageIndex, int pageSize, List<OrderState> state)
        {
            return _shopUnitOfWork.Get<Order>()
                .Where(p => p.AccountId == accountId && p.Isvalid && state.Contains(p.State))
                .OrderByDescending(p => p.OrderGenerateDate).Skip(pageSize * (pageIndex - 1)).Take(pageSize).ToList();
        }
        public IList<OrderedProduct> GetProductOrderedList(int accountId, int pageIndex, int pageSize)
        {
            //获取某用户下支付订单，的产品Id，和产品购买次数
            string sql = string.Format("select p.Id ,count(a.Id) as num,a.OrderType as [Type] from( " +
                        "select o.Id,o.OrderType from [Order] o where o.AccountId={0} and o.state in(3,4,5)) as a " +
                        "inner join OrderProduct op on a.Id=op.OrderId " +
                        "inner join Product p on op.ProductId=p.Id " +
                        "group by  p.Id,a.OrderType order by num desc;", accountId);
            return _shopUnitOfWork.Context.Database.SqlQuery<OrderedProduct>(sql)
                .Skip(pageSize * (pageIndex - 1)).Take(pageSize).ToList();
        }
        public IList<OrderedProduct> GetProductOrderedList(int accountId, int pageIndex, int pageSize, out int total)
        {
            //获取某用户下支付订单，的产品Id，和产品购买次数
            string sql = string.Format("select p.Id ,count(a.Id) as num,a.OrderType as [Type] from( " +
                        "select o.Id,o.OrderType from [Order] o where o.AccountId={0} and o.state in(3,4,5)) as a " +
                        "inner join OrderProduct op on a.Id=op.OrderId " +
                        "inner join Product p on op.ProductId=p.Id " +
                        "group by  p.Id,a.OrderType order by num desc;", accountId);
            var result = _shopUnitOfWork.Context.Database.SqlQuery<OrderedProduct>(sql).ToList();
            total = result.Count();
            return result.Skip(pageSize * (pageIndex - 1)).Take(pageSize).ToList();


        }
        /// <summary>
        /// 获取当前销售最多的商品
        /// </summary>
        /// <param name="top">前几商品</param>
        /// <param name="channel">对应的渠道  -1|全部渠道 1|现货和期酒 其余为跨境订单</param>
        /// <returns></returns>
        public List<string> GetTopHotSaleWine(int top, int channel)
        {
            List<string> list = new List<string>();
            string sql = "select p.Name from ( " +
                                " select o.Id from [Order] o where o.Isvalid=1 and o.state in(3,4,5) ";
            if (channel != 0)
            {                
                if (channel == 1)
                {
                    sql += " and o.OrderType in(1,7) ";

                }
                else
                {
                    sql += " and o.OrderType =4 ";
                }
            }
            else 
            {
                sql += " and o.OrderType in(1,4,7) ";
            }
            sql += " ) a inner join [orderProduct] op on a.Id = op.OrderId inner join [Product] p on p.id=op.ProductId  group by p.Id,p.Name order  by sum(op.Quantity) desc";
            var result = _shopUnitOfWork.Context.Database.SqlQuery<string>(sql).Where(p => !p.Contains("麦卡斯") && !p.Contains("羊驼"));
            foreach (var item in result)
            {
                var count = 0;
                if (channel == 0)
                {
                    var count1 = _luceneService.RetrievalProductAllInfoByAliYun(searchValue: item, type: ProductAliYunType.Normal).TotalCount;
                    var count2 = _luceneService.RetrievalProductAllInfoByAliYun(searchValue: item, type: ProductAliYunType.Expect).TotalCount;
                    var count3 = _luceneService.RetrievalProductAllInfoByAliYun(searchValue: item, type: ProductAliYunType.CBP).TotalCount;
                    count = count1 + count2 + count3;
                }
                else if (channel == 1)
                {
                    var count1 = _luceneService.RetrievalProductAllInfoByAliYun(searchValue: item, type: ProductAliYunType.Normal).TotalCount;
                    var count2 = _luceneService.RetrievalProductAllInfoByAliYun(searchValue: item, type: ProductAliYunType.Expect).TotalCount;
                    count = count1 + count2;
                }
                else
                {
                    count = _luceneService.RetrievalProductAllInfoByAliYun(searchValue: item, type: ProductAliYunType.CBP).TotalCount;

                }
                if (count > 0) { list.Add(item); }
                if (list.Count >= top) { break; }
            }
            return list;
        }
        

        /// <summary>
        /// 获取所有订单（待付定金 即将失效的除外）
        /// </summary>
        /// <param name="accountId"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public IPagedList<Order> GetOrderNotNeedPayListPage(int accountId, int pageIndex, int pageSize)
        {
            var query = _shopUnitOfWork.Get<Order>()
                .Where(p => p.AccountId == accountId && p.Isvalid && p.State != OrderState.NotPay).OrderByDescending(p => p.OrderGenerateDate);
            return new PagedList<Order>(query, pageIndex - 1, pageSize);
        }

        public int GetCountByState(int accountId, OrderState state)
        {
            return _shopUnitOfWork.Get<Order>()
                .Where(p => p.AccountId == accountId && p.Isvalid && p.State == state).Count();
        }

        public int GetCountByState(int accountId, List<OrderState> state)
        {
            return _shopUnitOfWork.Get<Order>()
                .Where(p => p.AccountId == accountId && p.Isvalid && state.Contains(p.State)).Count();
        }

        public int GetAllCount(int accountId)
        {
            return _shopUnitOfWork.Get<Order>()
                .Where(p => p.AccountId == accountId && p.Isvalid).Count();
        }

        /// <summary>
        /// 获得除即将失效的需付款的订单外 其他订单的数量
        /// </summary>
        /// <param name="accountId"></param>
        /// <returns></returns>
        public int GetAllCountOrderNotNeedPay(int accountId)
        {
            IList<Order> lst = _shopUnitOfWork.Get<Order>().Where(p => p.AccountId == accountId && p.Isvalid).ToList();
            for (int i = 0; i < lst.Count(); i++)
            {
                if (lst[i].State == OrderState.NotPay && lst[i].Deposit > 0 && lst[i].OrderInvalidDate > System.DateTime.Now)
                {
                    lst.Remove(lst[i]);
                }
            }
            return lst.Count();
        }

        public int GetInvalidCount(int accountId)
        {
            return _shopUnitOfWork.Get<Order>()
                .Where(p => p.State == OrderState.NotPay && p.AccountId == accountId && p.OrderInvalidDate < System.DateTime.Now && p.Isvalid).Count();
        }

        /// <summary>
        /// 交易报表
        /// </summary>
        /// <param name="startdate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public IList<OrderView> GetOrderView(DateTime startdate, DateTime endDate)
        {
            var query = from p in _shopUnitOfWork.Get<Order>()
                        where p.PayDate > startdate && p.PayDate < endDate
                              && (p.State == OrderState.Paid || p.State == OrderState.Shipped || p.State == OrderState.Complete || p.State == OrderState.PaidNotCompleted)
                        let dt = DbFunctions.TruncateTime(p.PayDate)
                        group p by dt into g
                        select new
                        {
                            Date = g.Key,
                            FactPrice = g.Sum(x => x.FactPrice),
                            ZMIntegralValue = g.Sum(x => x.ZMIntegralValue),
                            ZMCoupon = g.Sum(x => x.ZMCoupon),
                            WineCoupon = g.Sum(x => x.WineCoupon),
                            WineWorldCoupon = g.Sum(x => x.WineWorldCoupon),
                            ProductCoupon = g.Sum(x => x.ProductCoupon),
                            Price = g.Sum(x => x.FactPrice + x.ZMIntegralValue + x.ZMCoupon + (int)x.WineCoupon + (int)x.WineWorldCoupon + (int)x.ProductCoupon)
                        };
            return query.ToList().Select(x =>
            {
                return new OrderView()
                {
                    Date = x.Date.Value,
                    FactPrice = x.FactPrice,
                    ZMIntegralValue = x.ZMIntegralValue,
                    ZMCoupon = x.ZMCoupon,
                    WineCoupon = (int)x.WineCoupon,
                    WineWorldCoupon = (int)x.WineWorldCoupon,
                    ProductCoupon = (int)x.ProductCoupon,
                    Price = x.Price
                };
            }).ToList();
        }

        /// <summary>
        /// 根据地址获取 该地址的订单
        /// </summary>
        /// <param name="addressId"></param>
        /// <returns></returns>
        public IList<Order> GetOrdersByAddressId(int addressId)
        {
            return _shopUnitOfWork.Get<Order>().Where(p => p.Isvalid && p.AddressId == addressId).ToList();
        }

        #endregion Orders

        #region OrdrProductGift

        /// <summary>
        /// 获得赠品信息
        /// </summary>
        /// <param name="orderProductId"></param>
        /// <returns></returns>
        public IList<OrderProductGifts> GetOrderProductGiftsByOrderProductId(int orderProductId)
        {
            if (orderProductId == 0)
                throw new ArgumentException("orderProductId ==0");
            return _shopUnitOfWork.Get<OrderProductGifts>().Where(t => t.OrderProductId == orderProductId).ToList();
        }

        /// <summary>
        /// 获得赠品信息（订单的所有礼品：单量活动跟总量活动）
        /// </summary>
        /// <param name="orderProductId"></param>
        /// <returns></returns>
        public IList<OrderProductGifts> GetOrderProductGiftsByOrderId(int orderId)
        {
            if (orderId == 0)
                throw new ArgumentException("orderid =0");
            List<OrderProductGifts> result = _shopUnitOfWork.Get<OrderProductGifts>().Where(t => t.OrderProductId != null && t.OrderProduct.OrderId == orderId).ToList();// 针对单量的礼品

            List<OrderProductGifts> result1 = _shopUnitOfWork.Get<OrderProductGifts>().Where(t => t.OrderId == orderId).ToList();

            result.AddRange(result1);

            return result;
        }

        /// <summary>
        /// 获得赠品信息（总量活动类型的礼品）
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public IList<OrderProductGifts> GetToTotalGiftsByOrderId(int orderId)
        {
            if (orderId == 0)
                throw new ArgumentException("orderId ==0");
            List<OrderProductGifts> result = _shopUnitOfWork.Get<OrderProductGifts>().Where(t => t.OrderId == orderId && t.OrderProductId == null).ToList();// 针对总量的礼品

            return result;
        }

        #endregion OrdrProductGift

        #region PayNumber

        public void InsertPayNumber(PayNumber model)
        {
            _shopUnitOfWork.Insert<PayNumber>(model);
            _shopUnitOfWork.SaveChanges();
        }

        public PayNumber GetPayNumberModelByPayNumber(string payNumber)
        {
            PayNumber result = new PayNumber();
            result = _shopUnitOfWork.Get<PayNumber>().Where(t => t.PayNo == payNumber).FirstOrDefault();

            return result;
        }

        public PayNumber GetPayNumberById(int id)
        {
            return _shopUnitOfWork.Get<PayNumber>().Where(t => t.Id == id).FirstOrDefault();
        }

        public void UpdatePayNumber(PayNumber model)
        {
            _shopUnitOfWork.Update<PayNumber>(model);
            _shopUnitOfWork.SaveChanges();
        }

        /// <summary>
        /// 获取订单号的 最新支付号
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public string GetPayNumberByOrderId(int orderId)
        {
            Order_PayNumber_Mapping orderpaynumbermap =
            _shopUnitOfWork.Get<Order_PayNumber_Mapping>().Where(t => t.OrderId == orderId).OrderByDescending(t => t.CreatedTime).FirstOrDefault();
            if (orderpaynumbermap != null)
            {
                return _shopUnitOfWork.GetById<PayNumber>(orderpaynumbermap.PayNumberId).PayNo;
            }
            else
            {
                return string.Empty;
            }
        }

        public List<Order_PayNumber_Mapping> GetPayNumberMapByOrderId(int orderId)
        {
            List<Order_PayNumber_Mapping> orderpaynumbermap =
                       _shopUnitOfWork.Get<Order_PayNumber_Mapping>().Where(t => t.OrderId == orderId).OrderByDescending(t => t.CreatedTime).ToList();
            return orderpaynumbermap;
        }

        #endregion PayNumber

        #region OrderProduct

        /// <summary>
        /// 获取订单商品列表（未传送到酒业系统的已付款订单）
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        public IQueryable<OrderProduct> GetOrderProductListPayNotSendJiuYe(int productId)
        {
            return _shopUnitOfWork.Get<OrderProduct>().Where(t => t.ProductId == productId && t.Order.State == OrderState.Paid && !t.Order.IsTransferJiuYe);
        }

        #endregion OrderProduct

        public void ModOrderState(int orderId, OrderState orderState)
        {
            var order = _shopUnitOfWork.GetById<Order>(orderId);
            order.State = orderState;
            if (order.OrderType == (int)OrderStyle.CFP && orderState == OrderState.Paid)
            {
                foreach (var p in order.OrderProducts)
                {
                    var crowdFundingPlain = _crowdFundingService.GetCFPProductByProductId(p.ProductId);
                    var crowdFund = _crowdFundingService.GetCrowdFundingByCFPId(crowdFundingPlain.Id);
                    if (crowdFund == null)
                    {
                        var crowdFunding = new CrowdFunding()
                        {
                            CrowdFundingPlainId = crowdFundingPlain.Id,
                            PresellProductId = crowdFundingPlain.PresellProduct.Id,
                            StartDate = DateTime.Now,
                            EndDate = p.Quantity >= crowdFundingPlain.EndNum ? DateTime.Now : DateTime.Now.AddDays(10),
                            FundingNum = p.Quantity,
                            State = p.Quantity >= crowdFundingPlain.EndNum ? CrowdFundingState.EndFunding : CrowdFundingState.Underway,
                            CreatedBy = _workContext.CurrentAccount.Id
                        };
                        _shopUnitOfWork.Insert<CrowdFunding>(crowdFunding);

                        var crowdFundingOrderMap = new CrowdFundingOrderMap()
                        {
                            CrowdFundingId = crowdFunding.Id,
                            OrderId = order.Id,
                            CreatedBy = _workContext.CurrentAccount.Id
                        };
                        _shopUnitOfWork.Insert<CrowdFundingOrderMap>(crowdFundingOrderMap);

                        _shopUnitOfWork.SaveChanges();
                    }
                    else
                    {
                        crowdFund.FundingNum += p.Quantity;
                        if ((crowdFund.FundingNum + p.Quantity) >= crowdFundingPlain.EndNum)
                        {
                            crowdFund.EndDate = DateTime.Now;
                            crowdFund.State = CrowdFundingState.EndFunding;
                        }
                        _shopUnitOfWork.Update<CrowdFunding>(crowdFund);

                        var crowdFundingOrderMap = new CrowdFundingOrderMap()
                        {
                            CrowdFundingId = crowdFund.Id,
                            OrderId = order.Id,
                            CreatedBy = _workContext.CurrentAccount.Id
                        };
                        _shopUnitOfWork.Insert<CrowdFundingOrderMap>(crowdFundingOrderMap);
                        _shopUnitOfWork.SaveChanges();

                    }


                }
            }

            _shopUnitOfWork.SaveChanges();
        }
        /// <summary>
        /// 修改订单的收货
        /// </summary>
        /// <param name="orderId">订单ID</param>
        /// <param name="CustomerName">收货人姓名</param>
        /// <param name="MobilePhone">收货人手机号（电话号码）</param>
        /// <param name="strAddress">收货人地址</param>
        /// <param name="ReceiveEmail">通讯邮箱</param>
        public void EditOrderReceive(int orderId, string CustomerName, string MobilePhone, string strAddress, string ReceiveEmail)
        {
            var order = _shopUnitOfWork.GetById<Order>(orderId);
            order.CustomerName = CustomerName;
            order.MobilePhone = MobilePhone;
            order.AddressDetail = strAddress;
            _shopUnitOfWork.SaveChanges();
            if (order.OrderType == (int)OrderStyle.Expect)
            {
                //更新通讯邮箱
                AccountExtend account = _accountService.GetAccountExtendById(order.AccountId);
                account.ReceiveEmail = ReceiveEmail;
                _accountService.UpdateAccount(account);
            }
        }

        public void ModOrderProductPrice(int orderProductId, decimal unitPrice, string reason)
        {
            var orderProduct = _shopUnitOfWork.GetById<OrderProduct>(orderProductId);
            var order = _shopUnitOfWork.GetById<Order>(orderProduct.OrderId);
            if (order.State == OrderState.NotPay)
            {

                if (order.AdminRemark != null)
                {
                    order.AdminRemark += "修改该订单[" + orderProduct.Product.Name + "] 将价格" + orderProduct.UnitPrice + "改为" + unitPrice + "  " + "原因：" + reason + "\r";
                }
                else
                {
                    order.AdminRemark = "修改该订单[" + orderProduct.Product.Name + "] 将价格" + orderProduct.UnitPrice + "改为" + unitPrice + "  " + "原因：" + reason + "\r";
                }
                orderProduct.UnitPrice = unitPrice;
                orderProduct.Price = unitPrice * orderProduct.Quantity;
                order.FactPrice = order.OrderProducts.Sum(p => p.UnitPrice * p.Quantity);

                var action = "修改该订单[" + orderProduct.Product.Name + "]价格为：" + unitPrice;
                var orderId = orderProduct.OrderId;

                //新增
                _shopUnitOfWork.Update<Order>(order);
                //新增

                AddOrderModifyLog(reason, action, orderId, false);

                _shopUnitOfWork.SaveChanges();

                //新增
                _eventPublisher.EntityUpdated(order);
            }
        }
        public void ModOrderProductNum(int orderProductId, int num, string reason)
        {
            var orderProduct = _shopUnitOfWork.GetById<OrderProduct>(orderProductId);
            var order = _shopUnitOfWork.GetById<Order>(orderProduct.OrderId);
            if (order.State == OrderState.NotPay)
            {
                if (order.AdminRemark != null)
                {
                    order.AdminRemark += "修改该订单[" + orderProduct.Product.Name + "] 将数量：" + orderProduct.Quantity + "改为" + num + "  " + "原因：" + reason + "\r";
                }
                else
                {
                    order.AdminRemark = "修改该订单[" + orderProduct.Product.Name + "] 将数量：" + orderProduct.Quantity + "改为" + num + "  " + "原因：" + reason + "\r";
                }
                orderProduct.Quantity = num;
                orderProduct.Price = num * orderProduct.UnitPrice;
                order.FactPrice = order.OrderProducts.Sum(p => p.UnitPrice * p.Quantity);

                var action = "修改该订单[" + orderProduct.Product.Name + "]数量为：" + num;
                var orderId = orderProduct.OrderId;

                //新增
                _shopUnitOfWork.Update<Order>(order);
                //新增


                AddOrderModifyLog(reason, action, orderId, false);

                _shopUnitOfWork.SaveChanges();

                //新增
                _eventPublisher.EntityUpdated(order);
            }
        }
        public void AddOrderModifyLog(string reason, string action, int orderId, bool saveChanges = true)
        {
            var log = new OrderModifyLog()
            {
                OrderId = orderId,
                Action = action,
                Note = reason,
                Isvalid = true,
                CreatedBy = _workContext.CurrentAccount.Id,
                CreatedTime = DateTime.Now,
                ModifiedTime = DateTime.Now
            };
            _shopUnitOfWork.Insert<OrderModifyLog>(log);
            if (saveChanges) _shopUnitOfWork.SaveChanges();
        }

        public IList<OrderModifyLog> GetAllOrderModifyLog(int orderId)
        {
            return _shopUnitOfWork.Get<OrderModifyLog>().Where(p => p.Isvalid && p.OrderId == orderId).OrderBy(p => p.CreatedTime).ToList();
        }

        public void UpdateOrderAccount(int gustId, int accountId)
        {
            var orders = _shopUnitOfWork.Get<Order>().Where(p => p.AccountId == gustId);
            foreach (var item in orders)
            {
                item.AccountId = accountId;
            }
            _shopUnitOfWork.SaveChanges();
        }

        public void InsertPayGiftOrder(PayGiftOrder payGiftOrder)
        {
            if (payGiftOrder == null)
                throw new ArgumentNullException("payGiftOrder");

            _shopUnitOfWork.Insert<PayGiftOrder>(payGiftOrder);
            _shopUnitOfWork.SaveChanges();
            //event notification
            _eventPublisher.EntityInserted(payGiftOrder);
        }

        /// <summary>
        /// 获取订单是否存在唯一的支付方式
        /// </summary>
        /// <returns></returns>
        public bool CheckIsPayGiftOrder(string serialNumber)
        {
            if (serialNumber.IsNullOrEmpty())
                return false;

            var query = from o in _shopUnitOfWork.Get<PayGiftOrder>()
                        where o.SerialNumber == serialNumber
                        select o;
            var order = query.FirstOrDefault();

            if (order != null)
            {
                return true;
            }
            return false;
        }

        public PayGiftOrder GetPayGiftOrderBySN(string serialNumber)
        {
            if (serialNumber.IsNullOrEmpty())
                return null;

            var query = from o in _shopUnitOfWork.Get<PayGiftOrder>()
                        where o.SerialNumber == serialNumber
                        select o;
            var order = query.FirstOrDefault();
            return order;
        }


        /// <summary>
        /// 根据订单状态 返回订单状态描述。解决[ForMember(dest => dest.StrState, mo => mo.MapFrom(t => t.State.GetLocalizedEnum(localizationService, workContext)))]偶尔异常
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public string GetOrderStateStrByOrderState(OrderState state)
        {
            string result = "未知";
            switch (state)
            {
                case OrderState.Cancelled:
                    result = "已取消";
                    break;
                case OrderState.Complete:
                    result = "已完成";
                    break;
                case OrderState.Invalid:
                    result = "已失效";
                    break;
                case OrderState.ToConfirm:
                    result = "待确认";
                    break;
                case OrderState.NotPay:
                    result = "未支付";
                    break;
                case OrderState.PaidNotCompleted:
                    result = "付款未完结";
                    break;
                case OrderState.Paid:
                    result = "待发货";
                    break;
                case OrderState.PaidExceptionOrder:
                    result = "已支付异常订单";
                    break;
                case OrderState.RevokeOrder:
                    result = "已撤单";
                    break;
                case OrderState.Shipped:
                    result = "已发货";
                    break;
                case OrderState.PaidNotConfirm:
                    result = "付款待确认";
                    break;
                case OrderState.paidConfirmed:
                    result = "付款已确认";
                    break;
            }
            return result;
        }

        /// <summary>
        /// 根据订单号及产品Id判断是否属于跨境电商订单
        /// </summary>
        /// <param name="orderNumber"></param>
        /// <returns></returns>
        public bool IsCBPOrderByProductId(string orderNumber, int productId = 0)
        {

            var query = from o in _shopUnitOfWork.Get<Order>()
                        where o.OrderType == 4 && o.SerialNumber == orderNumber
                        select o;
            var order = query.Select(s => s.OrderProducts).SingleOrDefault();
            if (order != null)
            {
                return true;
            }


            return false;
        }

        #endregion Methods

        #region 关于在优惠活动赠送代金券的问题的方法
        public bool insertgiftcoupon(Order order)
        {
            string payCodeNew = "";
            bool ISinsertPaygift = false;
            foreach (var orderProduct in order.OrderProducts)
            {
                if (orderProduct.CouponGifts.Count > 0)
                {
                    continue;
                }
                IList<GiftCouponModel> coupongifts = null;
                coupongifts = _promotionsService.GetGiftCouponByConfirm(new CartModel()
                {
                    Product = _productService.GetProductCacheById(orderProduct.ProductId),
                    Quantity = orderProduct.Quantity,
                }, order.PaymentType.Code);
                if (coupongifts != null && coupongifts.Count > 0)
                {
                    foreach (var coupongift in coupongifts)
                    {
                        if (coupongift.CouponName == "AllCouponShow")
                        {
                            foreach (var CouponCategoryitem in coupongift.CouponCategoryTCMs)
                            {
                                orderProduct.CouponGifts.Add(new OrderProductCouponGifts()
                                {
                                    GiftPromotionsId = coupongift.GiftPromotionsId,
                                    CouponCategory_Type_Chanel_MappingID = CouponCategoryitem.Id,
                                    CouponChanelCode = CouponCategoryitem.ChanelCode,
                                    Quantity = coupongift.CouponQuantity,
                                    OrderId = order.Id,
                                    cctcm = CouponCategoryitem,
                                    ShowCouponName = "AllCouponShow"
                                });
                                if (!coupongift.GiftPromotions.PayCode.IsNullOrEmpty() && !CheckIsPayGiftOrder(order.SerialNumber))
                                {
                                    //添加字符代码
                                    payCodeNew = coupongift.GiftPromotions.PayCode;
                                    ISinsertPaygift = true;
                                }
                            }
                        }
                        else
                        {

                            int[] aint = new int[coupongift.CouponCategoryTCMs.Count];
                            for (int i = 0; i < coupongift.CouponCategoryTCMs.Count; i++)
                            {
                                aint[i] = i;
                            }

                            int[] bint = Tools.CommonTools.GenerateNumber(aint, coupongift.CouponQuantity);
                            foreach (var iint in bint)
                            {
                                orderProduct.CouponGifts.Add(new OrderProductCouponGifts()
                                {
                                    GiftPromotionsId = coupongift.GiftPromotionsId,
                                    CouponCategory_Type_Chanel_MappingID = coupongift.CouponCategoryTCMs[iint].Id,
                                    CouponChanelCode = coupongift.CouponCategoryTCMs[iint].ChanelCode,
                                    OrderId = order.Id,
                                    Quantity = 1,
                                    cctcm = coupongift.CouponCategoryTCMs[iint],
                                    ShowCouponName = coupongift.CouponCategoryTCMs[0].CouponCategory.Name + "等体验券随机"
                                });
                                if (!coupongift.GiftPromotions.PayCode.IsNullOrEmpty() && !CheckIsPayGiftOrder(order.SerialNumber))
                                {
                                    //添加字符代码
                                    payCodeNew = coupongift.GiftPromotions.PayCode;
                                    ISinsertPaygift = true;
                                }
                            }
                        }
                    }
                }
                _shopUnitOfWork.Update<OrderProduct>(orderProduct);
            }
            _shopUnitOfWork.SaveChanges();
            if (ISinsertPaygift)
            {
                if (!CheckIsPayGiftOrder(order.SerialNumber))
                {
                    if (payCodeNew != "ZXHD" && payCodeNew != "MSWXHD") //写死的支付代码
                    {
                        InsertPayGiftOrder(new PayGiftOrder() { SerialNumber = order.SerialNumber, PayTypeCode = payCodeNew, });
                    }
                }
            }
            return true;
        }
        #endregion

        #region 订购单下载管理

        public OrderFormPrintStatus GetOrderFormPrintStatusByOrderId(int orderId)
        {
            var query = _shopUnitOfWork.Get<OrderFormPrintStatus>().Where(x => x.Isvalid && x.OrderId == orderId).FirstOrDefault();
            return query;
        }




        public void UpdateOrderFormPrintStatus(OrderFormPrintStatus orderForm)
        {
            _shopUnitOfWork.Update<OrderFormPrintStatus>(orderForm);
            _shopUnitOfWork.SaveChanges();
        }

        public void InsertOrderFormPrintStatus(OrderFormPrintStatus orderForm)
        {
            _shopUnitOfWork.Insert<OrderFormPrintStatus>(orderForm);
            _shopUnitOfWork.SaveChanges();
        }

        #endregion

        #region OrderMoneySource
        /// <summary>
        /// 获取订单所使用的虚拟币来源
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public List<OrderMoneySource> GetOrderMoneySourceByOrderId(int orderId)
        {
            return _shopUnitOfWork.Get<OrderMoneySource>().Where(t => t.OrderId == orderId).OrderByDescending(t => t.CreatedTime).ToList();
        }
        public void DeleteOrderMoneySourceByOrderId(int orderId)
        {
            var datas = _shopUnitOfWork.Get<OrderMoneySource>().Where(t => t.OrderId == orderId);
            _shopUnitOfWork.DeleteRange<OrderMoneySource>(datas);
            _shopUnitOfWork.SaveChanges();
        }
        public void AddOrderMoneySource(OrderMoneySource model)
        {
            _shopUnitOfWork.Insert<OrderMoneySource>(model);
            _shopUnitOfWork.SaveChanges();
        }
        public void AddRangeMoneySource(List<OrderMoneySource> models)
        {
            _shopUnitOfWork.InsertRange<OrderMoneySource>(models);
            _shopUnitOfWork.SaveChanges();
        }
        /// <summary>
        /// 获取所有的待 记录中民积分明细来源的订单
        /// </summary>
        /// <returns></returns>
        public DataTable GetOrdersForGetOrderMoneyDetail()
        {
            string sql = @"select o.id,o.serialnumber from dbo.[order] o
where o.[state] in(3,4,5,9,11) and o.IntegralValue>0 
and not exists(select orderid from dbo.ordermoneysource where orderid= o.id)";
            return ExeSqlReturnDT(sql, null);

        }
        #endregion

        private AnalysisOrderResult AnalysisOrder(IEnumerable<Order> orders)
        {
            if (orders == null || orders.Count() == 0)
            {
                return AnalysisOrderResult.NoData;
            }
            int count = orders.Count();
            var usualCount = orders.Where(p => p.OrderType == (int)OrderStyle.Usual).Count();
            if (usualCount == count)
            {
                return AnalysisOrderResult.OnlyUsual;
            }
            var expectCount = orders.Where(p => p.OrderType == (int)OrderStyle.Expect).Count();
            if (expectCount == count)
            {
                return AnalysisOrderResult.OnlyExpect;
            }
            var presaleCount = orders.Where(p => p.OrderType == (int)OrderStyle.PresellOrder).Count();
            if (presaleCount == count)
            {
                return AnalysisOrderResult.OnlyPreSale;
            }
            var crossCount = orders.Where(p => p.OrderType == (int)OrderStyle.CBP).Count();
            if (crossCount == count)
            {
                return AnalysisOrderResult.OnlyCross;
            }
            if (usualCount + expectCount == count)
            {
                return AnalysisOrderResult.UsualExpect;
            }
            if (usualCount + presaleCount == count)
            {
                return AnalysisOrderResult.UsualPre;
            }
            if (usualCount + crossCount == count)
            {
                return AnalysisOrderResult.UsualCross;
            }
            if (expectCount + presaleCount == count)
            {
                return AnalysisOrderResult.ExpectPre;
            }
            if (expectCount + crossCount == count)
            {
                return AnalysisOrderResult.ExpectCross;
            }

            if (presaleCount + crossCount == count)
            {
                return AnalysisOrderResult.PreCross;
            }

            if (usualCount + expectCount + presaleCount == count)
            {
                return AnalysisOrderResult.UsualExpectPre;
            }
            if (usualCount + expectCount + crossCount == count)
            {
                return AnalysisOrderResult.UsualExpectCross;
            }

            if (usualCount + presaleCount + crossCount == count)
            {
                return AnalysisOrderResult.UsualPreCross;
            }

            if (expectCount + presaleCount + crossCount == count)
            {
                return AnalysisOrderResult.ExpectPreCross;
            }

            if (expectCount + presaleCount + crossCount == count)
            {
                return AnalysisOrderResult.UsualExpectPreCross;
            }
            return AnalysisOrderResult.Others;
        }

        public string GetOrderMessage(IList<Order> orders)
        {
            AnalysisOrderResult result = AnalysisOrder(orders);
            string message = string.Empty;
            switch (result)
            {
                case AnalysisOrderResult.NoData:
                case AnalysisOrderResult.Others:
                case AnalysisOrderResult.OnlyUsual:
                case AnalysisOrderResult.OnlyCross:
                case AnalysisOrderResult.UsualCross:
                    message = "您的订单已付款成功，我们将尽快给您安排发货。";
                    break;
                case AnalysisOrderResult.OnlyExpect:
                    message = "您的订单已付款成功，期酒到货后我们将尽快为您安排发货。";
                    break;
                case AnalysisOrderResult.OnlyPreSale:
                    message = "您的订单已付款成功，海外直购酒款到货后我们将尽快为您安排发货。";
                    break;
                case AnalysisOrderResult.ExpectCross:
                case AnalysisOrderResult.UsualExpect:
                case AnalysisOrderResult.UsualExpectCross:
                    message = "您的订单已付款成功，库存商品我们尽快为您安排发货；订单中的期酒到货后我们尽快安排发货。";
                    break;
                case AnalysisOrderResult.UsualPre:
                case AnalysisOrderResult.PreCross:
                case AnalysisOrderResult.UsualPreCross:
                    message = "您的订单已付款成功，库存商品我们尽快为您安排发货；订单中的海外直购酒款到货后我们尽快安排发货。";
                    break;
                case AnalysisOrderResult.UsualExpectPre:
                case AnalysisOrderResult.ExpectPreCross:
                case AnalysisOrderResult.UsualExpectPreCross:
                    message = "您的订单已付款成功，库存商品我们尽快为您安排发货；海外直购酒款和期酒到货后我们尽快安排发货。";
                    break;
                case AnalysisOrderResult.ExpectPre:
                    message = "您的订单已付款成功，海外直购酒款和期酒到货后我们尽快安排发货。";
                    break;
                default:
                    message = "您的订单已付款成功，我们将尽快给您安排发货。";
                    break;
            }
            return message;
        }

        #region 京东商城订单处理


        #region 订单SOP出库
        /// <summary>
        /// 订单SOP出库
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public JDM_SopOutstorageResult OrderOutstorage(JDM_SopOutstorage model, out string message)
        {
            #region 字段
            /// <summary>
            /// 采用OAuth授权方式的token
            /// </summary>
            string access_token = ConfigurationManager.AppSettings["access_token"].ToString();
            /// <summary>
            /// 应用的app_key
            /// </summary>
            string app_key = ConfigurationManager.AppSettings["app_key"].ToString();
            /// <summary>
            /// API协议版本，可选值:2.0
            /// </summary>
            string v = ConfigurationManager.AppSettings["v"].ToString();
            /// <summary>
            /// API接口名称
            /// </summary>
            string method = string.Empty;
            /// <summary>
            /// 应用的appSecret
            /// </summary>
            string app_secret = ConfigurationManager.AppSettings["app_secret"].ToString();
            /// <summary>
            /// 获取时间戳
            /// </summary>
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            /// <summary>
            /// 签名
            /// </summary>
            string sign = string.Empty;
            /// <summary>
            /// https调用入口地址
            /// </summary>
            string server_url = ConfigurationManager.AppSettings["server_url"].ToString();
            #endregion
            method = "360buy.order.sop.outstorage";
            //help doc link:http://jos.jd.com/api/detail.htm?apiName=360buy.order.sop.outstorage&id=411
            StringBuilder strMd5 = new StringBuilder();
            strMd5.Append(app_secret);
            strMd5.Append("360buy_param_json");
            strMd5.Append(StrTools.JSONListToString(model).Trim());
            //strMd5.Append("}");
            strMd5.Append("access_token" + access_token);
            strMd5.Append("app_key" + app_key);
            strMd5.Append("method" + method);
            strMd5.Append("timestamp" + timestamp);
            strMd5.Append(app_secret);
            StringBuilder strParameters = new StringBuilder();
            strParameters.Append(server_url + "?");
            strParameters.Append("v=" + v + "&");
            strParameters.Append("method=" + method + "&");
            strParameters.Append("app_key=" + app_key + "&");
            strParameters.Append("access_token=" + access_token + "&");
            strParameters.Append("360buy_param_json=");
            strParameters.Append(StrTools.JSONListToString(model).Trim());
            strParameters.Append("&");
            strParameters.Append("timestamp=" + timestamp + "&");
            strParameters.Append("sign=" + CommonTools.SVmd5(strMd5.ToString()));
            string result = StrTools.GetHtmlFromGet(strParameters.ToString(), Encoding.UTF8);
            try
            {
                //解析JSON
                JObject jo = (JObject)JsonConvert.DeserializeObject(result);
                if (jo["order_sop_outstorage_response"] != null)
                {
                    string jsonstr = jo["order_sop_outstorage_response"].ToString();
                    List<JDM_SopOutstorageResult> canslist = new List<JDM_SopOutstorageResult>();
                    canslist = StrTools.JSONStringToList<JDM_SopOutstorageResult>(jsonstr);
                    message = "";
                    return canslist.FirstOrDefault();
                }
                else
                {
                    if (result.Contains("10300009"))
                    {
                        JDM_SopOutstorageResult resmodel = new JDM_SopOutstorageResult();
                        resmodel.code = "10300009"; //表示 运单没有在青龙系统生成
                        message = "表示运单没有在青龙系统生成，联系技术人员查看";
                        return resmodel;
                    }
                    else
                    {
                        Tools.IOTools.LogText("订单SOP出库接口JDAPI出错。接口调用的json为:" + strParameters.ToString() + "。程序默认返回为空！");
                        Tools.IOTools.LogText("订单SOP出库接口JDAPI出错。接口返回的json为:" + result + "。程序默认返回为空！");
                        message = "接口返回报错，请联系技术人员解决，返回信息为：" + result;
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                Tools.IOTools.LogText("订单SOP出库接口JDAPI出错。接口调用的json为:" + strParameters.ToString() + "。程序默认返回为空！");
                Tools.IOTools.LogText("订单SOP出库接口JDAPI出错。接口返回的json为:" + result + "。程序出错信息：" + e.Message);
                message = "接口程序报错，请联系技术人员解决，返回信息为：" + result + "；程序出错信息：" + e.Message;
                return null;
            }
        }
        #endregion

        #region 检索商家物流公司信息（只可获取商家后台已设置的物流公司信息）
        /// <summary>
        /// 检索商家物流公司信息（只可获取商家后台已设置的物流公司信息）
        /// </summary>
        /// <param name="fields">字段</param>
        /// <returns>商家后台配置的能用的物流公司</returns>
        public List<JDM_CompanyExpressCanUse> getDeliveryCompanyCanUse(string fields)
        {
            #region 字段
            /// <summary>
            /// 采用OAuth授权方式的token
            /// </summary>
            string access_token = ConfigurationManager.AppSettings["access_token"].ToString();
            /// <summary>
            /// 应用的app_key
            /// </summary>
            string app_key = ConfigurationManager.AppSettings["app_key"].ToString();
            /// <summary>
            /// API协议版本，可选值:2.0
            /// </summary>
            string v = ConfigurationManager.AppSettings["v"].ToString();
            /// <summary>
            /// API接口名称
            /// </summary>
            string method = string.Empty;
            /// <summary>
            /// 应用的appSecret
            /// </summary>
            string app_secret = ConfigurationManager.AppSettings["app_secret"].ToString();
            /// <summary>
            /// 获取时间戳
            /// </summary>
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            /// <summary>
            /// 签名
            /// </summary>
            string sign = string.Empty;
            /// <summary>
            /// https调用入口地址
            /// </summary>
            string server_url = ConfigurationManager.AppSettings["server_url"].ToString();
            #endregion
            method = "360buy.delivery.logistics.get";
            //help doc link:http://jos.jd.com/api/detail.htm?apiName=360buy.delivery.logistics.get&id=394
            StringBuilder strMd5 = new StringBuilder();
            strMd5.Append(app_secret);
            strMd5.Append("360buy_param_json{");
            strMd5.Append("\"optional_fields\":\"" + fields + "\"");
            strMd5.Append("}");
            strMd5.Append("access_token" + access_token);
            strMd5.Append("app_key" + app_key);
            strMd5.Append("method" + method);
            strMd5.Append("timestamp" + timestamp);
            strMd5.Append(app_secret);
            StringBuilder strParameters = new StringBuilder();
            strParameters.Append(server_url + "?");
            strParameters.Append("v=" + v + "&");
            strParameters.Append("method=" + method + "&");
            strParameters.Append("app_key=" + app_key + "&");
            strParameters.Append("access_token=" + access_token + "&");
            strParameters.Append("360buy_param_json={");
            strParameters.Append("\"optional_fields\":\"" + fields + "\"");
            strParameters.Append("}&");
            strParameters.Append("timestamp=" + timestamp + "&");
            strParameters.Append("sign=" + CommonTools.SVmd5(strMd5.ToString()));
            string result = StrTools.GetHtmlFromGet(strParameters.ToString(), Encoding.UTF8);
            List<JDM_CompanyExpressCanUse> canslist = new List<JDM_CompanyExpressCanUse>();
            try
            {
                //解析JSON
                JObject jo = (JObject)JsonConvert.DeserializeObject(result);
                if (jo["delivery_logistics_get_response"] != null)
                {
                    string jsonstr = jo["delivery_logistics_get_response"]["logistics_companies"]["logistics_list"].ToString();
                    canslist = StrTools.JSONStringToList<JDM_CompanyExpressCanUse>(jsonstr);
                    return canslist;
                }
                else
                {
                    Tools.IOTools.LogText("获取商家的在用物流公司接口JDAPI出错。接口调用的json为:" + strParameters.ToString() + "。程序默认返回为空！");
                    Tools.IOTools.LogText("获取商家的在用物流公司接口JDAPI出错。接口返回的json为:" + result + "。程序默认返回为空！");
                    return canslist;
                }
            }
            catch (Exception e)
            {
                Tools.IOTools.LogText("获取商家的在用物流公司接口JDAPI出错。接口调用的json为:" + strParameters.ToString() + "。程序默认返回为空！");
                Tools.IOTools.LogText("获取商家的在用物流公司接口JDAPI出错。接口返回的json为:" + result + "。程序出错信息：" + e.Message);
                return canslist;
            }
        }
        #endregion

        #endregion

        /// <summary>
        /// 订单来源统计分析
        /// </summary>
        /// <param name="searchModel">查询</param>
        /// <param name="isDetail">是否显示用户细节</param>
        /// <returns></returns>
        public IEnumerable<OrderStatisticsModel> OrderReferStatistics(OrderStatisticsSearchModel searchModel, bool isDetail, out int totalCount)
        {
            #region 拼接sql
            string sqlFormat = "SELECT ReferrerDomain,id,RegsterTime,ReferrerUrl INTO #ReferrerAccount FROM dbo.AccountExtend A WHERE {0};"
                               + "SELECT SUM(O.ZMCoupon+O.FactPrice+O.WineCoupon+O.WineWorldCoupon+O.ZMIntegralValue) AS OrderPrice,COUNT(O.Id) AS OrderNum,"
                               + "A.ReferrerDomain,COUNT(DISTINCT A.id) AS AccountNum,COUNT(DISTINCT O.AccountId) AS ResumeNum {2} INTO #Result FROM #ReferrerAccount A LEFT JOIN"
                               + " dbo.[Order] O ON A.id=O.AccountId  {1} GROUP BY A.ReferrerDomain {3};SET @totalCount=(SELECT COUNT(*) FROM #Result);"
                               + "SELECT * FROM(SELECT ROW_NUMBER() OVER ({5}) AS RowNumber,* FROM #Result)P {4};"
                               + "DROP TABLE #ReferrerAccount,#Result;";
            StringBuilder builder = new StringBuilder();
            StringBuilder accountBuilder = new StringBuilder(" A.ReferrerDomain IS NOT NULL ");
            string isDetailSql = "";
            string endSql = "";
            string orderSql = "";
            string pageSql = "";
            switch (searchModel.AccountType)
            {
                case AccountStatisticsType.Vip:
                    accountBuilder.Append(" AND A.VipGradeId=2 ");
                    break;
                case AccountStatisticsType.General:
                    accountBuilder.Append(" AND A.VipGradeId=1 ");
                    break;
                default:
                    break;
            }
            if (searchModel.RegStartTime != null)
            {
                accountBuilder.AppendFormat(" AND A.RegsterTime>='{0}' ", searchModel.RegStartTime.Value);
            }
            if (searchModel.RegEndTime != null)
            {
                accountBuilder.AppendFormat(" AND A.RegsterTime<='{0}' ", searchModel.RegEndTime.Value);
            }
            if (searchModel.ReferrerDomain != null)
            {
                accountBuilder.AppendFormat(" AND A.ReferrerDomain LIKE '%{0}%' ", searchModel.ReferrerDomain);
            }
            if (searchModel.StartTime != null)
            {
                builder.AppendFormat(" AND O.OrderGenerateDate>='{0}' ", searchModel.StartTime.Value);
            }
            if (searchModel.EndTime != null)
            {
                builder.AppendFormat(" AND O.OrderGenerateDate<='{0}' ", searchModel.EndTime.Value);
            }
            if (searchModel.OrderState != null && searchModel.OrderState != OrderAllState.NoCondition)
            {
                if (searchModel.OrderState == OrderAllState.ValidOrder)
                {
                    builder.AppendFormat(" AND O.state IN({0},{1},{2},{3}) ", (int)OrderAllState.Paid, (int)OrderAllState.Shipped, (int)OrderAllState.Complete, (int)OrderAllState.PaidNotCompleted);
                }
                else
                {
                    builder.AppendFormat(" AND O.state={0} ", (int)searchModel.OrderState.Value);
                }
            }
            if (searchModel.OrderType == null || (int)searchModel.OrderType != -2)
            {
                if (searchModel.OrderType != null)
                {
                    builder.AppendFormat(" AND O.OrderType={0} ", (int)searchModel.OrderType.Value);
                }
                builder.AppendFormat(" AND O.SerialNumber NOT LIKE 'GF%' ");//不包括礼品卡订单
            }
            else
            {
                builder.AppendFormat(" AND O.SerialNumber LIKE 'GF%' ");//礼品卡订单
            }
            if (isDetail)
            {
                isDetailSql = ",A.id AS AccountId,A.RegsterTime,A.ReferrerUrl ";
                endSql = " ,A.id,A.RegsterTime,A.ReferrerUrl ";
                orderSql = " ORDER BY RegsterTime DESC";
            }
            else
            {
                orderSql = " ORDER BY OrderPrice DESC";
            }
            if (searchModel.IsValildReferrer)
            {
                endSql += " HAVING COUNT(O.Id)>0";
            }
            pageSql = " WHERE RowNumber BETWEEN " + ((searchModel.CurrentPage - 1) * searchModel.PageSize + 1) + " AND " + searchModel.PageSize * searchModel.CurrentPage;
            string execSql = string.Format(sqlFormat, accountBuilder.ToString(), builder.ToString(), isDetailSql, endSql, pageSql, orderSql);
            #endregion

            SqlParameter totalPara = new SqlParameter();
            totalPara.ParameterName = "totalCount";
            totalPara.Value = 0;
            totalPara.Direction = ParameterDirection.Output;
            var result = _shopUnitOfWork.Context.Database.SqlQuery<OrderStatisticsModel>(execSql, totalPara).ToList();
            totalCount = (int)totalPara.Value;

            result.Each(i =>
            {
                if (i.OrderPrice == null)
                {
                    i.OrderPrice = 0;
                    i.ResumeNum = 0;
                }
                if (i.ResumeNum > 0 && i.OrderNum > 0)
                {
                    i.OrderPrice = Math.Round(i.OrderPrice.Value, 2);
                    i.PerOrderNum = i.OrderNum / i.ResumeNum.Value;
                    i.PerOrderPrice = Math.Round((i.OrderPrice.Value / i.ResumeNum.Value), 2);
                    i.PerOrderNumPrice = Math.Round(i.OrderPrice.Value / i.OrderNum, 2);
                    i.PerAONumPrice = Math.Round(i.OrderPrice.Value / (i.OrderNum * i.ResumeNum.Value), 2);
                }
            });
            return result;
        }

        /// <summary>
        /// 获得给定价格的VIP价
        /// </summary>
        /// <param name="price"></param>
        /// <returns></returns>
        public decimal GetVipPrice(decimal price)
        {
            return price * _workContext.CurrentAccount.VipDiscountValue();
        }

        /// <param name="isAgent">是否独代商品</param>
        /// <returns></returns>
        public string GetMemberDisplayName(DiscountType discountType, bool isAgent = false)
        {
            switch (discountType)
            {
                case DiscountType.Staff:
                    {
                        if (isAgent)
                        {
                            return "买一送一";
                        }
                        return "员工";
                    }
                case DiscountType.Member:
                    {
                        if (isAgent)
                        {
                            return "买一送一";
                        }
                        return "VIP";
                    }
                default:
                    return "";

            }
        }

        /// <summary>
        /// 获得品酒师微信
        /// </summary>
        /// <returns></returns>
        public Tuple<string, string> GetPJSWechat()
        {
            var xmlPath = HttpContext.Current.Server.MapPath("/Configs/PJSWechat.xml");
            if (File.Exists(xmlPath))
            {
                var doc = new XmlDocument();
                doc.Load(xmlPath);
                var current = doc.SelectSingleNode("/WeChats");
                if (null != current)
                {
                    var time = current.Attributes["Time"].Value;
                    var cycle = current.Attributes["Cycle"].Value.ToInt(1);//循环数，默认1
                    var cAccount = current.Attributes["Account"].Value;
                    var cUrl = current.Attributes["Url"].Value;
                    if (!(time.IsNullOrEmpty() || cAccount.IsNullOrEmpty() || cUrl.IsNullOrEmpty()) && DateTime.Now < DateTime.Parse(time).AddMonths(cycle))
                    {
                        return new Tuple<string, string>(cAccount, cUrl);
                    }
                    else
                    {
                        var sort = current.Attributes["Sort"].Value.ToInt() + 1;
                        var next = current.SelectSingleNode("WeChat[@Sort='" + sort + "']");
                        if (null == next)
                        {
                            sort = 1;
                            next = current.SelectSingleNode("WeChat[@Sort='" + sort + "']");//从排序1开始
                        }
                        if (null != next)
                        {
                            var account = next.Attributes["Account"].Value;
                            var url = next.Attributes["Url"].Value;
                            current.Attributes["Time"].Value = DateTime.Now.ToString();
                            current.Attributes["Sort"].Value = sort.ToString();
                            current.Attributes["Account"].Value = account;
                            current.Attributes["Url"].Value = url;
                            doc.Save(xmlPath);
                            return new Tuple<string, string>(account, url);
                        }
                        else
                        {
                            return new Tuple<string, string>(cAccount, cUrl);
                        }
                    }
                }
            }
            return null;
        }


        public List<UMDataSource> GetCustomerInfo(string date, out string msg)
        {
            try
            {
                var sql = "select  c.UserName as AccountName,d.CustomerName,d.MobilePhone as Mobile,Origin=6 from(  select t.id ,t.username from(" +
                                             " select row_number() over(partition  by username order by OrderGenerateDate) as rownum,a.id, a.UserName ,o.OrderGenerateDate from[order] o" +
                                             " inner join AccountExtend a on o.AccountId = a.id where  a.id not in( (select a.AccountExtend_Id from [Role] r inner join  [Account_Role_Mapping] a on r.Id =a.Role_Id where Code ='Staff')) and  a.AffiliatedSalesman is null and o.[state] in (3,4,5,11) and  o.Isvalid = 1 and a.Isvalid = 1 ) t where  t.rownum = 1 ";
                //if (!date.IsNullOrEmpty())
                //{
                sql += " and t.OrderGenerateDate >= '" + date + "' and t.OrderGenerateDate <= '" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "' ";
                // }
                sql += ") c inner join[Order] d on c.id = d.AccountId  where d.[state] in (3,4,5,11) and d.Isvalid = 1 group by  c.UserName, d.CustomerName, d.MobilePhone";
                var result = _shopUnitOfWork.Context.Database.SqlQuery<UMDataSource>(sql).ToList();
                msg = "true";
                return result;
            }
            catch (Exception e)
            {
                msg = e.Message;
                return null;
            }
        }


        public bool CheckOrderPayState(string serialNumbers)
        {
            var list = serialNumbers.Split(',');
            var count = 0;
            foreach (var item in list)
            {
                var SerialNumber = AESHelper.AESDecrypt(item, _workContext.CurrentAccount.Passwordsalt);
                var order = _shopUnitOfWork.Get<Order>().Where(p => p.SerialNumber == SerialNumber).FirstOrDefault();
                if (order != null)
                {
                    if (order.State == OrderState.Complete || order.State == OrderState.Paid || order.State == OrderState.Shipped || order.State == OrderState.PaidNotCompleted)
                    {
                        count++;
                    }
                    else { return false; }
                }
                else { return false; }
            }
            if (count == list.Count())
            {
                return true;
            }
            else { return false; }
        }

        /// <summary>
        /// 根据Id获取自提仓库
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public OwnTakeWarehouse GetOwnTakeWarehouseById(int id)
        {
            if (id <= 0)
                return null;
            return _shopUnitOfWork.GetById<OwnTakeWarehouse>(id);
        }
        /// <summary>
        /// 获取所有的自提地址
        /// </summary>
        /// <returns></returns>
        public List<OwnTakeWarehouse> GetAllOwnTakeWarehouse()
        {
            return _shopUnitOfWork.Get<OwnTakeWarehouse>(x => x.Isvalid).ToList();
        }
        /// <summary>
        /// 根据orderId获取订单到仓库自提的信息
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public Order_OwnTakeWarehouse_Mapping GetOrder_OwnTakeWarehouse_MappingByOrderId(int orderId)
        {
            if (orderId <= 0)
                return null;
            return _shopUnitOfWork.Get<Order_OwnTakeWarehouse_Mapping>().Where(t => t.OrderId == orderId).FirstOrDefault();
        }


        public List<statistics> statisticsPromotion2017(Nullable<DateTime> StartTime, Nullable<DateTime> EndTime)
        {
            var result = _shopUnitOfWork.Get<Promotions>().Where(p => (StartTime == null || p.EndDate >= StartTime.Value) && (EndTime == null || p.StartDate <= EndTime.Value)).ToList().Select(x =>
            {
                var PromotionsId = x.Id;
                PromotionsCategory PromotionsCategoryId = _shopUnitOfWork.Get<Promotions>().Where(p => p.Id == PromotionsId).FirstOrDefault().PromotionsCategoryId;
                IQueryable<Order> query = null;
                switch (PromotionsCategoryId)
                {   //满免订单
                    case PromotionsCategory.FullFree: query = GetOrderByPromotionsId(PromotionsId, PromotionsCategory.FullFree); break;
                    //买赠订单
                    case PromotionsCategory.Gift: query = GetGiftOrderByPromotionsId(PromotionsId); break;
                    //支付买赠
                    case PromotionsCategory.PayGift: query = GetPayGiftOrderByPromotionsId(PromotionsId); break;
                    //首次APP下载，优惠活动
                    case PromotionsCategory.APPCoupon: query = GetAPPCouponOrderByPromotionsId(PromotionsId); break;

                    //APP推广促销活动(线上没有这个活动)
                    case PromotionsCategory.APPPromotion: query = null; break;
                    //限时优惠活动
                    case PromotionsCategory.NewPricePromotion: query = GetNewPricePromotionOrderByPromotionsId(PromotionsId); break;
                    //赠券活动
                    case PromotionsCategory.GiveCouponPromotion: query = GetGiveCouponPromotionOrderByPromotionsId(PromotionsId); break;
                    //加价换购
                    case PromotionsCategory.FareIncrease: query = GetFareIncreaseOrderByPromotionsId(PromotionsId); break;
                    //满折订单
                    case PromotionsCategory.FullDiscount: query = GetOrderByPromotionsId(PromotionsId, PromotionsCategory.FullDiscount); break;
                    //多买促销（M元任选N件）订单
                    case PromotionsCategory.BuyOptional: query = GetOrderByPromotionsId(PromotionsId, PromotionsCategory.BuyOptional); break;
                    case PromotionsCategory.IntegralAccelerate: query = GetIntegralPromotionOrderByPromotionsId(PromotionsId); break;
                    default: query = null; break;
                }
                statisticsTemp res;
                if (query != null)
                {
                    var temp = query.Where(p => p.OrderType == 1).Distinct(p => p.Id).Where(p => (StartTime == null || p.OrderGenerateDate >= StartTime.Value) && (EndTime == null || p.OrderGenerateDate <= EndTime.Value));
                    res = new statisticsTemp { PromotionId = PromotionsId, Name = x.Name, PromotionsCategory = x.PromotionsCategoryId, StartTime = x.StartDate, EndTime = x.EndDate, OrderCount = temp.Count(), Amount = temp.Sum(p => p.FactPrice) };

                }
                else
                {
                    res = new statisticsTemp { PromotionId = PromotionsId, Name = x.Name, PromotionsCategory = x.PromotionsCategoryId, StartTime = x.StartDate, EndTime = x.EndDate, OrderCount = 0, Amount = 0M };
                }
                return res;
            });
            var dt = (from a in result
                      join b in _shopUnitOfWork.Get<Product_Promotions_Mapping>()
                      on a.PromotionId equals b.PromotionsId
                      join c in _shopUnitOfWork.Get<Product>()
                      on b.ProductId equals c.Id
                      join d in _shopUnitOfWork.Get<GiftPromotions_Product_Mapping>()
                      on a.PromotionId equals d.GiftPromotionsId into tt
                      from t in tt.DefaultIfEmpty()
                      select new statisticsTemp
                      {
                          PromotionId = a.PromotionId,
                          Name = a.Name,
                          PromotionsCategory = a.PromotionsCategory,
                          StartTime = a.StartTime,
                          EndTime = a.EndTime,
                          OrderCount = a.OrderCount,
                          Amount = a.Amount,
                          Name1 = c.Name,
                          GiftProductId = t == null ? 0 : t.ProductId
                      }).ToList();
            List<statistics> data = new List<statistics>();
            foreach (var it in dt.GroupBy(p => p.PromotionId))
            {
                statistics n = new statistics()
                {
                    PromotionId = null,
                    PromotionName = "",
                    StartTime = null,
                    EndTime = null,
                    OrderCount = null,
                    Amount = null,
                    PromotionsCategory = "",
                    ProductName = "",
                    GiftName = ""
                };//隔一行
                if (data.Count() != 0)
                {
                    data.Add(n);
                }
                var GiftIds = it.Select(p => p.GiftProductId).Distinct();
                var ProductNames = it.Select(p => p.Name1).Distinct().ToArray();
                var GiftName = _shopUnitOfWork.Get<Product>().Where(p => GiftIds.Contains(p.Id)).Select(p => p.Name).Distinct().ToArray();
                var a_num = ProductNames.Count();
                var b_num = GiftName.Count();
                var num = a_num > b_num ? a_num : b_num;
                for (int i = 0; i < num; i++)
                {
                    statistics statistics = new statistics();

                    if (i == 0)
                    {
                        var item = it.FirstOrDefault();
                        statistics.PromotionId = item.PromotionId;
                        statistics.PromotionName = item.Name;
                        statistics.StartTime = item.StartTime;
                        statistics.EndTime = item.EndTime;
                        statistics.OrderCount = item.OrderCount;
                        statistics.Amount = item.Amount;
                        switch (item.PromotionsCategory)
                        {
                            case PromotionsCategory.APPCoupon: statistics.PromotionsCategory = "APP首次下载奖励代金券活动"; break;
                            case PromotionsCategory.Gift: statistics.PromotionsCategory = "买赠促销"; break;
                            case PromotionsCategory.FullFree: statistics.PromotionsCategory = "满免促销"; break;
                            case PromotionsCategory.PayGift: statistics.PromotionsCategory = "支付买赠"; break;
                            case PromotionsCategory.APPPromotion: statistics.PromotionsCategory = "APP推广促销活动"; break;
                            case PromotionsCategory.NewPricePromotion: statistics.PromotionsCategory = "限时优惠活动"; break;
                            case PromotionsCategory.GiveCouponPromotion: statistics.PromotionsCategory = "赠券活动"; break;
                            case PromotionsCategory.FareIncrease: statistics.PromotionsCategory = "加价换购"; break;
                            case PromotionsCategory.FullDiscount: statistics.PromotionsCategory = "满折活动"; break;
                            case PromotionsCategory.BuyOptional: statistics.PromotionsCategory = "多买促销-M元任选N件"; break;
                            case PromotionsCategory.IntegralAccelerate: statistics.PromotionsCategory = "中民积分加速"; break;
                        }
                    }
                    else
                    {
                        statistics.PromotionId = null;
                        statistics.PromotionName = "";
                        statistics.StartTime = null;
                        statistics.EndTime = null;
                        statistics.OrderCount = null;
                        statistics.Amount = null;
                        statistics.PromotionsCategory = "";
                    }
                    if (i < a_num)
                    {
                        statistics.ProductName = ProductNames[i];
                    }
                    else
                    {
                        statistics.ProductName = "";
                    }
                    if (i < b_num)
                    {
                        statistics.GiftName = GiftName[i];
                    }
                    else
                    {
                        statistics.GiftName = "";
                    }
                    data.Add(statistics);
                }
            }
            return data;
        }
    }

    public class OrderGroup
    {
        public OrderGroup()
        {
            OrderType = OrderStyle.Usual;
        }
        public int Key { get; set; }

        public IList<ShoppingCart> Carts { get; set; }

        public decimal Price { get; set; }

        public int IntegrationValue { get; set; }

        public OrderStyle OrderType { get; set; }
        public string Protocol { get; set; }
        public string AgreeProtocolState { get; set; }

    }
}