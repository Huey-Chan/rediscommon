using Shop.Core;
using Shop.Data.Domain;
using Shop.Services.Products;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Web.Model.ShopProject.Coupon;
using Web.Model.ShopProject.ProductCoupon;
using Web.Model.ShopProject.Orders;
using Shop.Data.Domain.Customers;
using Web.Model.ShopProject.JDM;
using Web.Model.ShopProject.Product;

namespace Shop.Services.Orders
{
    public interface IOrderService
    {
        #region Orders

        //不区分订单类型
        Order GetOrderById(int orderId);

        /// <summary>
        /// 获取所有的以保存的快递公司
        /// </summary>
        /// <returns> 快递公司列表</returns>
        List<Delivery100> GetDelivery100ByIsvalid();
        Delivery100 GetDelivery100ById(int Id);

        Delivery100 GetDelivery100ByEfastDelivery(string efastDelivery);

        /// <summary>
        /// 通过跨境电商九米接口-快递公司编码获取相应的Id
        /// </summary>
        /// <param name="jiuMiDelivery">跨境电商九米接口-快递公司编码</param>
        /// <returns></returns>
        int GetDelivery100IdForJiuMi(string jiuMiDelivery);

        /// <summary>
        /// Get orders by identifiers
        /// </summary>
        /// <param name="orderIds">Order identifiers</param>
        /// <returns>Order</returns>
        IList<Order> GetOrdersByIds(int[] orderIds);
        /// <summary>
        /// 根据时间段获取已经付款的有效订单的用户信息
        /// </summary>
        /// <param name="BeginTime">开始时间</param>
        /// <param name="EndTime">结束时间</param>
        /// <returns>用户列表</returns>
        IQueryable<AccountExtend> GetOrdersByTime(DateTime BeginTime, DateTime EndTime);
        IList<Order> GetOrdersByIdString(string ids);

        Order GetOrderByNumber(string orderNumber);

        Order GetOrderByJiuYeOrderId(string jiuYeOrderId);

        bool IsCBPOrderByProductId(string orderNumber, int ProductId = 0);

        int GetCombinationProductStockQuantity(int productId, bool isInShoppingCart = false);

        void DeleteOrder(Order order);

        //通过SearchOrderContext.OrderStyle区分订单类型
        IPagedList<Order> SearchOrders(SearchOrderContext searchOrderContext);
        // 2018-1-23 zxw 获取线上合作订单
        IPagedList<Order> SearchCooperationOrders(SearchOrderContext searchOrderContext, string rebateWebSiteId,string PlatformCode);
        /// <summary>
        /// 通过活动ID获取当前活动的所有订单信息
        /// </summary>
        /// <param name="PromotionsId">活动ID</param>
        /// <returns></returns>
        IPagedList<Order> SearchOrdersByPromotionsId(int PromotionsId, int PageIndex, int PageSize, bool isAll = false);

        IPagedList<Order> GetAllOrdersByPage(SearchOrderContext searchOrderContext);

        string GetOrderMessage(IList<Order> orders);

        //IPagedList<Order> GetPagedExchangedOrders(int pageIndex, int pageSize);

        IPagedList<Order> SearchRebateOrders(RebateOrderSearchContext searchOrderContext);

        IList<Order> SearchRebateOrdersNoPages(RebateOrderSearchContext searchOrderContext);

        //通过SearchOrderContext.OrderStyle区分订单类型
        void SearchOrdersStatistics(SearchOrderContext searchOrderContext,
            ref int footerIntegral,
            ref int footerGetIntegration,
            ref decimal footerCoupon,
            ref double footerWCoupon,
            ref double footerWineWorldCoupon,
            ref decimal footerMoney,
            ref decimal footerPrice,
            ref decimal footerFullFreePrice,
            ref decimal footerFullDiscountPrice,
            out double footerMyProductCoupon);

        /// <summary>
        /// 需要支付定金的订单， 未支付的话 会导致订单失效
        /// </summary>
        /// <param name="accountId"></param>
        /// <returns></returns>
        IList<Order> GetNeedPayOrders(int accountId);

        int GetOrderNumByOrderState(int accountId, OrderState orderState);

        IQueryable<Order> PrepareSearchUsualOrderQuery(SearchOrderContext searchOrderContext);
        /// <summary>
        /// 导出初次购买的用户
        /// </summary>
        /// <param name="StartTime">开始时间</param>
        /// <param name="EndTime">结束时间</param>
        /// <param name="orderStyle">订单类型（跨境、期酒、现货）</param>
        /// <returns></returns>
        DataTable ExportFirstBuysUserName(Nullable<DateTime> StartTime, Nullable<DateTime> EndTime, OrderStyle orderStyle = OrderStyle.Usual);
        DataTable ExportResult(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo);      
        DataTable ExportRebateOrderList(SearchOrderContext searchOrderContext);
        DataTable SearchRebateOrderList(SearchOrderContext searchOrderContext);
        DataTable ExportRebateOrderDetail(SearchOrderContext searchOrderContext);
        DataTable ExportOrderList(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo);
        /// <summary>
        /// 获取中民积分没有赠送成功的订单
        /// </summary>
        /// <returns></returns>
        List<Order> GetOrdersByGetJiFen();
        DataTable ExportResultDetail(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo,bool show_tc,bool isExpect);
        /// <summary>
        /// 导出跨境电商订单明细
        /// </summary>
        /// <param name="searchOrderContext"></param>
        /// <param name="excelContainCustomerInfo"></param>
        /// <returns></returns>
        DataTable ExportCBPResult(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo);
        /// <summary>
        /// 导出跨境电商订单详情
        /// </summary>
        /// <param name="searchOrderContext"></param>
        /// <param name="excelContainCustomerInfo"></param>
        /// <returns></returns>
        DataTable ExportCBPResultDetail(SearchOrderContext searchOrderContext, bool excelContainCustomerInfo, bool show_tc);

        //通过SearchOrderContext.OrderStyle区分订单类型
        IList<Order> SearchOrdersWithNoPage(SearchOrderContext searchOrderContext);

        IList<OrderCombinationProduct> GetOrderCombinationProductsByOrderProductId(int orderProductId);

        bool IsOrderProductsHasStockQuantity(Order order, out string message);

        /// <summary>
        /// 检查商品库存、每人限制数量
        /// </summary>
        /// <param name="product"></param>
        /// <param name="quantity"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        bool CheckProductHasStockQuantity(Product product, int quantity, out string message, bool isInShoppingCart = false, CartType cartType = CartType.Usual);
        /// <summary>
        /// 根据订单的类型返回购物车的类型（主要用于库存等的检验）
        /// </summary>
        /// <param name="os">订单的类型</param>
        /// <returns>购物车的类型</returns>
        CartType GetCartTypeByOrderStyle(OrderStyle os);
        /// <summary>
        /// 获得未付款订单
        /// </summary>
        /// <returns></returns>
        IList<Order> GetNotPaiedOrders();

        /// <summary>
        /// Inserts an order
        /// </summary>
        /// <param name="order">Order</param>
        void InsertOrder(Order order);

        IList<Order> SubmitOrder(
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
            );
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
        IList<Order> SubmitColumnOrder(
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
            );
        IList<Order> SubmitOrder_New(
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
            );

        bool SubmitPaidOrderFromGiftCard(int accountId,
            List<Web.Model.ShopProject.Orders.OtherCartModel> list,
            Address address,
            string remark,
            OrderStyle orderStyle, string platformCode);

        /// <summary>
        /// 生成邀请值兑换的订单
        /// </summary>
        /// <param name="accountId">用户ID</param>
        /// <param name="list">商品list</param>
        /// <param name="address">地址</param>
        /// <param name="remark">备注</param>
        /// <param name="orderStyle">订单类型</param>
        /// <returns>返回订单</returns>
        Order SubmitPaidOrderFromInvitation(int accountId,
            List<Web.Model.ShopProject.Orders.OtherCartModel> list,
            Address address,
            string remark,
            OrderStyle orderStyle);
        /// <summary>
        /// Updates the order
        /// </summary>
        /// <param name="order">The order</param>
        void UpdateOrder(Order order);

        /// <summary>
        /// 批量更新order,或全部更新或全部不更新
        /// </summary>
        /// <param name="orders"></param>
        void UpdateOrderList(IList<Order> orders);

        string GetCompanyName(int type);
        ///// <summary>
        ///// 获得之前购买该主题商品信息
        ///// </summary>
        ///// <param name="customerId"></param>
        ///// <param name="topicProductId"></param>
        ///// <returns></returns>
        //IList<OrderProduct> GetTopicProductByCustomerAndTopicProductId(int customerId, int topicProductId);

        /// <summary>
        /// 检查商品库存、每人限制数量
        /// </summary>
        /// <param name="product"></param>
        /// <param name="quantity"></param>
        void ValidateProductSaleInfo(Product product, int quantity, bool isInShoppingCart = false, CartType cartType = CartType.Usual);

        /// <summary>
        /// 获得用户支付金额
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        decimal GetPayMoney(Order order);

        /// <summary>
        /// 全并付款
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="payment"></param>
        /// <param name="payNumber"></param>
        void MergerPayOrder(string serialNumber, PaymentType paymentType, out string payNumber, out decimal payMoney, out string subject, string platform = null);

        /// <summary>
        /// 获取该订单所能得到的中民积分
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        int GetIntegrationValueByOrder(Order order);

        /// <summary>
        /// 支付成功
        /// </summary>
        /// <param name="order"></param>
        /// <param name="tradeNo">交易号</param>
        /// <param name="useWXCoupon">使用微信立减金 金额</param>
        void PaySucess(IList<Order> orders, string tradeNo = "", string payNumber = "", bool fullVirtualMoney = false, int useWXCoupon = 0);

        /// <summary>
        /// 获得用户 30分钟之前的所有历史有效订单总数（支付金额大于0的有效单）
        /// </summary>
        /// <param name="accountid"></param>
        /// <returns></returns>
        int GetAccountOrderCountRecord(int accountid);
        /**************
        bool SaledNotShipOrder(int orderId);
        bool GiveBackStock(int orderId);
         * *****************/
        /// <summary>
        /// 根据订单的商品ID获取代金券赠品
        /// </summary>
        /// <param name="opID">OrderProductorID</param>
        /// <returns>代金券集合</returns>
        ICollection<OrderProductCouponGifts> getopcg(int opID);


        /// <summary>
        /// 获得流水号
        /// </summary>
        /// <returns></returns>
        string GetOrderId(string preWord);

        string GetOrderSerialNumber(string preWord);

        /// <summary>
        /// 根据订单状态 获取当天的订单数
        /// </summary>
        /// <param name="orderStatus"></param>
        /// <returns></returns>
        int GetTodayOrderCountByStatus(OrderState orderStatus);

        IList<Order> GetTopOrdersByAccountId(int accountId, int top);

        IList<Order> GetListPageByState(int accountId, int pageIndex, int pageSize, OrderState state);

        IList<Order> GetListPageByState(int accountId, int pageIndex, int pageSize, List<OrderState> state);

        //用户常购清单-产品Id，和产品购买次数
        IList<OrderedProduct> GetProductOrderedList(int accountId, int pageIndex, int pageSize);
        IList<OrderedProduct> GetProductOrderedList(int accountId, int pageIndex, int pageSize, out int total);

        IPagedList<Order> GetOrderNotNeedPayListPage(int accountId, int pageIndex, int pageSize);

        int GetCountByState(int accountId, OrderState state);

        int GetCountByState(int accountId, List<OrderState> state);

        int GetAllCount(int accountId);

        int GetAllCountOrderNotNeedPay(int accountId);

        int GetInvalidCount(int accountId);

        /// <summary>
        /// 交易报表
        /// </summary>
        /// <param name="startdate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        IList<OrderView> GetOrderView(DateTime startdate, DateTime endDate);

        /// <summary>
        /// 根据地址获取 该地址的订单
        /// </summary>
        /// <param name="addressId"></param>
        /// <returns></returns>
        IList<Order> GetOrdersByAddressId(int addressId);

        #endregion Orders

        #region OrdrProductGift

        /// <summary>
        /// 获得赠品信息
        /// </summary>
        /// <param name="orderProductId"></param>
        /// <returns></returns>
        IList<OrderProductGifts> GetOrderProductGiftsByOrderProductId(int orderProductId);

        /// <summary>
        /// 获得赠品信息(订单的所有礼品：单量活动跟总量活动)
        /// </summary>
        /// <param name="orderProductId"></param>
        /// <returns></returns>
        IList<OrderProductGifts> GetOrderProductGiftsByOrderId(int orderId);

        /// <summary>
        /// 获得赠品信息（总量活动类型的礼品）
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        IList<OrderProductGifts> GetToTotalGiftsByOrderId(int orderId);

        #endregion OrdrProductGift

        #region PayNumber

        void InsertPayNumber(PayNumber model);

        PayNumber GetPayNumberModelByPayNumber(string payNumber);

        PayNumber GetPayNumberById(int id);

        void UpdatePayNumber(PayNumber model);

        /// <summary>
        /// 获取订单号的 最新支付号
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        string GetPayNumberByOrderId(int orderId);

        List<Order_PayNumber_Mapping> GetPayNumberMapByOrderId(int orderId);


        #endregion PayNumber

        #region OrderMoneySource
        /// <summary>
        /// 获取订单所使用的虚拟币来源
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        List<OrderMoneySource> GetOrderMoneySourceByOrderId(int orderId);

        void DeleteOrderMoneySourceByOrderId(int orderId);

        void AddOrderMoneySource(OrderMoneySource model);

        void AddRangeMoneySource(List<OrderMoneySource> models);
        /// <summary>
        /// 获取所有的待 记录中民积分明细来源的订单
        /// </summary>
        /// <returns></returns>
        DataTable GetOrdersForGetOrderMoneyDetail();
        #endregion

        #region OrderProduct

        /// <summary>
        /// 获取订单商品列表（未传送到酒业系统的已付款订单）
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        IQueryable<OrderProduct> GetOrderProductListPayNotSendJiuYe(int productId);

        #endregion OrderProduct

        void ModOrderState(int orderId, OrderState orderState);
        /// <summary>
        /// 修改订单的收货
        /// </summary>
        /// <param name="orderId">订单ID</param>
        /// <param name="CustomerName">收货人姓名</param>
        /// <param name="MobilePhone">收货人手机号（电话号码）</param>
        /// <param name="strAddress">收货人地址</param>
        /// <param name="ReceiveEmail">通讯邮箱</param>
        void EditOrderReceive(int orderId, string CustomerName, string MobilePhone, string strAddress, string ReceiveEmail);

        void ModOrderProductPrice(int orderProductId, decimal unitPrice, string reason);

        void AddOrderModifyLog(string reason, string action, int orderId, bool saveChanges = true);

        //不区分订单类型
        IList<OrderModifyLog> GetAllOrderModifyLog(int orderId);

        void UpdateOrderAccount(int gustId, int accountId);

        /// <summary>
        /// 获取订单是否存在唯一的支付方式
        /// </summary>
        /// <returns></returns>
        bool CheckIsPayGiftOrder(string serialNumber);

        /// <summary>
        /// 插入唯一的支付方式的订单
        /// </summary>
        /// <param name="payGiftOrder"></param>
        void InsertPayGiftOrder(PayGiftOrder payGiftOrder);

        PayGiftOrder GetPayGiftOrderBySN(string serialNumber);

        /// <summary>
        /// 检查订单是否属于支付满赠并且如果支付方式符合满赠条件则进行更新
        /// </summary>
        /// <param name="order"></param>
        /// <param name="paymentType"></param>
        void CheckOrderAndUpdateByPayGift(Order order, PaymentType paymentType);

        void GetJiHua(Order model, JiHua jihua);
        void GetJiHua(OrderModel model, JiHua jihua);

        /// <summary>
        /// 供视图调用
        /// </summary>
        /// <param name="model"></param>
        ///<param name="jihua"></param>
        void GetJiHua(CartListModel model, JiHua jihua);

        void GetJiHua(IList<CartModel> cartList, JiHua jihua);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        void GetJiHua(IList<ShoppingCart> model, JiHua jihua);

        /// <summary>
        ///  根据加密后的 订单号 校验用户的集花礼品数 ( 当前用户)
        /// </summary>
        /// <param name="model"></param>
        void GetJiHua(string serialNumbers, JiHua jihua);


        /// <summary>
        /// 新版 集花兑换方法
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="num"></param>
        /// <param name="jihua"></param>
        void GetJiHua(int productId, int num, JiHua jihua);

        /// <summary>
        /// 根据订单状态 返回订单状态描述。解决[ForMember(dest => dest.StrState, mo => mo.MapFrom(t => t.State.GetLocalizedEnum(localizationService, workContext)))]偶尔异常
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        string GetOrderStateStrByOrderState(OrderState state);
        bool insertgiftcoupon(Order order);
        /// <summary>
        /// 根据订单ID获取订单计划兑换信息
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        IList<OrderJiHuaGift> GetOrderJiHuaGiftListByOrderId(int orderId);

        void UpdateOrderJiHua(OrderJiHuaGift model);

        void UpdateOrderJiHua(Order order, JiHua jihua);


        void ModOrderProductNum(int orderproductId, int p, string reason);



        OrderFormPrintStatus GetOrderFormPrintStatusByOrderId(int orderId);

        void UpdateOrderFormPrintStatus(OrderFormPrintStatus orderForm);

        void InsertOrderFormPrintStatus(OrderFormPrintStatus orderForm);

        #region 京东商城订单处理
        /// <summary>
        /// 检索商家物流公司信息（只可获取商家后台已设置的物流公司信息）
        /// </summary>
        /// <param name="fields">字段</param>
        /// <returns>商家后台配置的能用的物流公司</returns>
        List<JDM_CompanyExpressCanUse> getDeliveryCompanyCanUse(string fields);
        /// <summary>
        /// 订单SOP出库
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        JDM_SopOutstorageResult OrderOutstorage(JDM_SopOutstorage model, out string message);
        #endregion
        /// <summary>
        /// 订单来源统计分析
        /// </summary>
        /// <param name="searchModel">查询</param>
        /// <param name="isDetail">是否显示用户细节</param>
        /// <returns></returns>
        IEnumerable<OrderStatisticsModel> OrderReferStatistics(OrderStatisticsSearchModel searchModel, bool isDetail, out int totalCount);

        /// <summary>
        /// 获得给定价格的VIP价
        /// </summary>
        /// <param name="price"></param>
        /// <returns></returns>
        decimal GetVipPrice(decimal price);

        /// <param name="isAgent">是否独代商品</param>
        /// <returns></returns>
        string GetMemberDisplayName(DiscountType discountType, bool isAgent = false);
        /// <summary>
        /// 获得品酒师微信
        /// </summary>
        /// <returns></returns>
        Tuple<string, string> GetPJSWechat();
        /// <summary>
        /// 获取date起始时间以后的新增收货人信息
        /// </summary>
        /// <param name="date"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        List<UMDataSource> GetCustomerInfo(string date, out string msg);

        bool CheckOrderPayState(string serialNumbers);

        List<string> GetTopHotSaleWine(int top, int channel);

        /// <summary>
        /// 根据Id获取自提仓库
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        OwnTakeWarehouse GetOwnTakeWarehouseById(int id);
        /// <summary>
        /// 获取所有的自提地址
        /// </summary>
        /// <returns></returns>
        List<OwnTakeWarehouse> GetAllOwnTakeWarehouse();
        /// <summary>
        /// 根据orderId获取订单到仓库自提的信息
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        Order_OwnTakeWarehouse_Mapping GetOrder_OwnTakeWarehouse_MappingByOrderId(int orderId);

        List<statistics> statisticsPromotion2017(Nullable<DateTime> StartTime, Nullable<DateTime> EndTime);
    }
}