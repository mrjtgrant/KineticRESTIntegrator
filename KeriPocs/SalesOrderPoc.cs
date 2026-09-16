using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Newtonsoft.Json.Linq;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read side is always-safe; write side is GATED by
    /// <see cref="PocConfig.AllowWrites"/>.</b> Demonstrates the two
    /// shapes a sales-order read can take, and (only when armed)
    /// creating a new sales order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two read patterns:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="SalesOrderSvc.SalesOrdersAsync"/> — the raw OData
    ///     entity-set wrapper with <c>filters</c>, <c>select</c>, and
    ///     <c>top</c> parameters. Use this when you want a list of orders
    ///     matching some criteria, with the columns you choose. Returns
    ///     typed <see cref="OrderHed"/> rows.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="SalesOrderSvc.GetByPONumAsync"/> — a convenience
    ///     method that finds the order for a customer PO number (PO is
    ///     unique per order at the Epicor installation level) and returns
    ///     the wide multi-table <c>GetByID</c> dataset for it. Use this
    ///     when you want the full order by its PO. When no order matches,
    ///     the failure has a 404-shape with a PO-specific message.
    ///     Materialize the header or lines off
    ///     <c>RawResponse</c> via <c>.ToObject&lt;OrderHed&gt;()</c> /
    ///     <c>ExtractDtoList&lt;OrderDtl&gt;()</c> when you need them.
    ///   </description></item>
    /// </list>
    /// <para>
    /// The write path — <see cref="SalesOrderSvc.CreateOrderAsync"/> — is
    /// gated. With <see cref="PocConfig.AllowWrites"/> off, the POC builds
    /// the call arguments, prints exactly what it would send, and stops.
    /// With it on, the POC <i>creates a real sales order in your pilot
    /// company</i> and prints the new order number.
    /// </para>
    /// </remarks>
    internal static class SalesOrderPoc
    {
        // Tune these to a real customer ID that exists on your pilot.
        // Change "TESTCUST" to whatever your demo customer is.
        private const string DemoCustomerID = "TESTCUST";
        private const string DemoPONumber = "KERI-POC-PO";

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("SalesOrder POC (read-only + GATED create)");

            // ---- 1) Read: SalesOrdersAsync (OData entity-set wrapper) ------
            //
            // The OData entity-set wrapper: pass filters, a $select, and a
            // top. Returns OperationResult<List<OrderHed>>, with the columns
            // you asked for populated and the rest at their type defaults.
            // This is the workhorse for "give me a list of orders matching
            // some criteria."

            Console.WriteLine($"Looking up any existing orders for PO '{DemoPONumber}'...");
            var lookup = await client.SalesOrder
                .SalesOrdersAsync(
                    filters: new List<string> { $"PONum eq '{DemoPONumber}'" },
                    select: new List<string> { "OrderNum", "PONum", "OrderDate" })
                .ConfigureAwait(false);

            if (lookup.IsFailure)
            {
                Console.WriteLine($"  FAILED: {lookup.ErrorMessage}");
                if (!string.IsNullOrEmpty(lookup.CorrelationId)) Console.WriteLine($"  CorrelationId: {lookup.CorrelationId}");
                return;
            }

            Console.WriteLine($"  OK — {lookup.Value.Count} order(s) match.");
            foreach (var hed in lookup.Value)
                Console.WriteLine($"    OrderNum = {hed.OrderNum}, OrderDate = {hed.OrderDate:yyyy-MM-dd}");

            // ---- 2) Read: GetByPONumAsync (convenience: full dataset by PO) -
            //
            // GetByPONumAsync is a small orchestrator: it queries
            // SalesOrders for the PONum, then calls GetByID with the matching
            // OrderNum and returns the wide multi-table dataset directly. PO
            // numbers are expected to be unique per order, so this returns
            // at most one order. When nothing matches, the failure has a
            // 404 status and a PO-specific message.

            Console.WriteLine();
            Console.WriteLine($"Fetching full dataset for PO '{DemoPONumber}'...");

            var full = await client.SalesOrder.GetByPONumAsync(DemoPONumber).ConfigureAwait(false);

            if (full.IsFailure)
            {
                Console.WriteLine($"  FAILED: {full.ErrorMessage}");
                if (!string.IsNullOrEmpty(full.CorrelationId)) Console.WriteLine($"  CorrelationId: {full.CorrelationId}");
                if (full.StatusCode == 404)
                    Console.WriteLine("  (no existing order to fetch — skipping fetch demo)");
            }
            else
            {
                // The dataset is at full.Value["ds"][tableName].
                // Walk just enough to prove it came back.
                JToken ds = full.Value?["ds"];
                JToken hedRow = ds?["OrderHed"]?[0];
                JArray dtlRows = ds?["OrderDtl"] as JArray;

                if (hedRow != null)
                {
                    // Materialize the header into the typed DTO.
                    var hed = hedRow.ToObject<OrderHed>();
                    Console.WriteLine($"  OrderHed: cust={hed.CustomerCustID,-12} PO={hed.PONum,-12} status={hed.OrderStatus}");
                    Console.WriteLine($"            total={hed.TotalOrder} {hed.CurrencyCode}  lines={dtlRows?.Count ?? 0}");
                }
                else
                {
                    Console.WriteLine("  (unexpected dataset shape — no OrderHed[0])");
                }
            }

            // ---- 3) Write — GATED -------------------------------------------
            //
            // Construct the create call. We build the arguments either way so
            // the user can see exactly what would be sent.

            DateTime needBy = DateTime.Today.AddDays(14);
            string endpoint = "Erp.BO.SalesOrderSvc/MasterUpdate  (new order via CreateOrderAsync orchestrator)";

            Console.WriteLine();
            Console.WriteLine("Prepared sales-order create call:");
            Console.WriteLine($"    CustID     = {DemoCustomerID}");
            Console.WriteLine($"    NeedByDate = {needBy:yyyy-MM-dd}");
            Console.WriteLine($"    PONum      = {DemoPONumber}");

            if (!PocConfig.AllowWrites)
            {
                PocConfig.PrintDryRunBanner(endpoint);
                return;
            }

            // Writes are armed — actually execute.
            PocConfig.PrintLiveWriteBanner(endpoint);
            var create = await client.SalesOrder
                .CreateOrderAsync(DemoCustomerID, needBy, DemoPONumber)
                .ConfigureAwait(false);

            if (create.IsFailure)
            {
                Console.WriteLine($"  FAILED: {create.ErrorMessage}");
                if (!string.IsNullOrEmpty(create.CorrelationId)) Console.WriteLine($"  CorrelationId: {create.CorrelationId}");
                if (create.StatusCode.HasValue)
                    Console.WriteLine($"  HTTP {create.StatusCode}");
                return;
            }

            // The new order number is on the echoed OrderHed row.
            JToken newHed = create.Value?["ds"]?["OrderHed"]?[0];
            if (newHed != null)
                Console.WriteLine($"  OK — created sales order {newHed["OrderNum"]}.");
            else
                Console.WriteLine("  OK — order created (response shape was unexpected; check raw response).");
        }
    }
}
