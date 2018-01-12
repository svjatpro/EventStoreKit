using System;
using EventStoreKit.Aggregates;

namespace EventStoreKit.Northwind.Aggregates
{
    public class OrderDetail : TrackableAggregateBase
    {
        #region private fields

        private Guid OrderId;
        private Guid ProductId;
        private decimal UnitPrice;
        private decimal Quantity;
        private decimal Discount;

        #endregion


    }
}
