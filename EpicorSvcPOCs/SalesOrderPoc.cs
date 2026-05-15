using System;
using System.Threading.Tasks;
using EpicorSvcs;
using EpicorSvcs.Dtos;
using Newtonsoft.Json.Linq;

namespace EpicorSvcPOCs
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
    ///     <see cref="SalesOrderSvc.FindOrderByPONumAsync"/> — a narrow
    ///     OData query that returns lightweight <see cref="OrderHed"/> rows
    ///     populated with order numbers only. Use this to look up "do we
    ///     have an order for this PO yet?"
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="SalesOrderSvc.GetByIDAsync"/> — pulls the full order
    ///     dataset (header + lines + releases + tax + ~20 tables) as a raw
    ///     <c>JObject</c>. Materialize the header or lines off
    ///     <c>RawResponse</c> via <c>.ToObject&lt;OrderHed&gt;()</c> /
    ///     <c>ExtractDtoList&lt;OrderDtl&gt;()</c> when you need them.
    ///   </description></item>
    /// </list>
    /// <para>
    /// The write path — <see cref="SalesOrderSvc.NewOrderAsync"/> — is gated.
    /// With <see cref="PocConfig.AllowWrites"/> off, the POC builds the call
    /// arguments, prints exactly what it would send, and stops. With it on,
    /// the POC <i>creates a real sales order in your pilot company</i> and
    /// prints the new order number.
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

            // ---- 1) Read: FindOrderByPONumAsync -----------------------------
            //
            // Narrow OData query, $select=OrderNum only. Useful for "does
            // this PO already exist?" pre-checks. Returns
            // OperationResult<List<OrderHed>> with only OrderNum populated.

            Console.WriteLine($"Looking up any existing orders for PO '{DemoPONumber}'...");
            var lookup = await client.SalesOrder
                .FindOrderByPONumAsync(DemoPONumber)
                .ConfigureAwait(false);

            if (lookup.IsFailure)
            {
                Console.WriteLine($"  FAILED: {lookup.ErrorMessage}");
                return;
            }

            Console.WriteLine($"  OK — {lookup.Value.Count} order(s) match.");
            foreach (var hed in lookup.Value)
                Console.WriteLine($"    OrderNum = {hed.OrderNum}");

            // ---- 2) Read: GetByIDAsync (when there's an order to fetch) -----
            //
            // GetByID returns the whole dataset as a raw JObject — an order
            // IS its full multi-table dataset. Materialize individual rows
            // off RawResponse / Value as needed.

            if (lookup.Value.Count > 0)
            {
                int orderNum = lookup.Value[0].OrderNum;
                Console.WriteLine();
                Console.WriteLine($"Fetching full dataset for order {orderNum}...");

                var full = await client.SalesOrder.GetByIDAsync(orderNum).ConfigureAwait(false);

                if (full.IsFailure)
                {
                    Console.WriteLine($"  FAILED: {full.ErrorMessage}");
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
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("  (no existing order to fetch — skipping GetByID demo)");
            }

            // ---- 3) Write — GATED -------------------------------------------
            //
            // Construct the create call. We build the arguments either way so
            // the user can see exactly what would be sent.

            DateTime needBy = DateTime.Today.AddDays(14);
            string endpoint = "Erp.BO.SalesOrderSvc/MasterUpdate  (new order via NewOrderAsync orchestrator)";

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
                .NewOrderAsync(DemoCustomerID, needBy, DemoPONumber)
                .ConfigureAwait(false);

            if (create.IsFailure)
            {
                Console.WriteLine($"  FAILED: {create.ErrorMessage}");
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
