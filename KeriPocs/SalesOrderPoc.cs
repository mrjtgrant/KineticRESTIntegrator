using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.Epicor.Dtos;
using Newtonsoft.Json.Linq;

namespace KeriPocs
{
    /// <summary>
    /// <b>Read side is always-safe; the write is gated by
    /// <see cref="PocConfig.ConfirmWrite"/>.</b> Demonstrates the two shapes a
    /// sales-order read can take, and — only after you say yes — creating a new
    /// sales order.
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
    /// The write path — <see cref="SalesOrderSvc.CreateOrderAsync"/> followed by
    /// <see cref="SalesOrderSvc.AddOrderLineAsync"/> — needs
    /// <c>KERI_POC_ALLOW_WRITES</c> set <i>and</i> a yes at the prompt. Unarmed,
    /// the POC still prints the customer, PO number, need-by date and the part
    /// it would put on the line, so you can see what an armed run would do.
    /// </para>
    /// <para>
    /// <b>One prompt covers both calls.</b> A sales order with a line on it is
    /// one record to agree to, not two, even though it takes two SDK calls and
    /// several HTTP requests. The unit of consent is what appears in Epicor, not
    /// what appears in the call stack.
    /// </para>
    /// <para>
    /// <b>The part comes from a live read</b>, not a constant. Hard-coding a
    /// placeholder would fail on any install that does not happen to have it;
    /// reading one part means the POC configures itself. When no part is found
    /// the order is still created, the prompt says there will be no line, and
    /// the line step is skipped — the prompt never promises what the run cannot
    /// do.
    /// </para>
    /// <para>
    /// <b>The order it creates stays.</b> Keri wraps no delete for sales orders
    /// — <c>UDTableSvc.DeleteByIDAsync</c> is the only delete in the public API
    /// — so the prompt says so rather than implying the POC will tidy up after
    /// itself.
    /// </para>
    /// <para>
    /// <b>On failure it says which kind.</b> The order is committed before the
    /// line is attempted, so a failed line leaves an order behind either way.
    /// <c>Uncommitted</c> means no line was written and the order simply has
    /// none; anything else means the commit was attempted and the order should
    /// be checked before another line is added. The step trail is printed in
    /// both cases — demonstrating it is half the point of this POC.
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
            PocBanner.Section("SalesOrder POC (read-only + confirmed create)");

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

            // ---- 3) Pick a part for the line --------------------------------
            //
            // Read one real part rather than hard-coding a placeholder that may
            // not exist on this install. If the read finds nothing, the order is
            // still created — just without a line — and the prompt says so, so
            // it never promises something it cannot do.

            Console.WriteLine();
            Console.WriteLine("Looking for a part to put on the line...");

            string linePart = null;
            var parts = await client.Part.PartsAsync(top: 1).ConfigureAwait(false);

            if (parts.IsSuccess && parts.Value.Count > 0)
            {
                linePart = parts.Value[0].PartNum;
                Console.WriteLine($"  Using {linePart}.");
            }
            else
            {
                Console.WriteLine("  None found — the order will be created without a line.");
            }

            // ---- 4) Write — confirmed ---------------------------------------
            //
            // One prompt for the whole thing. A sales order with a line on it is
            // one record to agree to, not two, even though it takes two SDK calls
            // and several HTTP requests. The unit is what appears in Epicor.
            //
            // ConfirmWrite prints the arguments whether or not writes are
            // armed, so the unarmed path is still a description of what the
            // armed one would do. It returns true only when writes are armed,
            // someone is there to answer, and they said yes.

            DateTime needBy = DateTime.Today.AddDays(14);

            var details = new List<string>
            {
                $"Customer  : {DemoCustomerID}",
                $"PO number : {DemoPONumber}",
                $"Need by   : {needBy:yyyy-MM-dd}",
                linePart == null
                    ? "Line      : none — no part was found to add"
                    : $"Line      : one, for part {linePart}"
            };

            bool proceed = PocConfig.ConfirmWrite(
                linePart == null
                    ? $"Create a sales order in company {client.Session.Company}."
                    : $"Create a sales order with one line in company {client.Session.Company}.",
                details,
                "Erp.BO.SalesOrderSvc/MasterUpdate, then GetNewOrderDtl → ChangePartNum → "
                    + "MasterUpdate  (CreateOrderAsync, then AddOrderLineAsync)",
                "The order remains on your server. Keri wraps no delete for sales orders, "
                    + "so removing it means doing so in Epicor.");

            if (!proceed) return;

            var create = await client.SalesOrder
                .CreateOrderAsync(DemoCustomerID, needBy, DemoPONumber)
                .ConfigureAwait(false);

            if (create.IsFailure)
            {
                Console.WriteLine($"  FAILED: {create.ErrorMessage}");
                if (!string.IsNullOrEmpty(create.CorrelationId)) Console.WriteLine($"  CorrelationId: {create.CorrelationId}");
                if (create.StatusCode.HasValue)
                    Console.WriteLine($"  HTTP {create.StatusCode}");

                // CreateOrderAsync is not idempotent. On an Indeterminate
                // failure the commit was attempted and an order may exist, so
                // say so rather than letting the reader assume a clean no-op.
                if (create.FailureStage == FailureStage.Indeterminate)
                    Console.WriteLine("  The commit was attempted — check for a new order "
                                    + $"under PO '{DemoPONumber}' before running this again.");
                return;
            }

            // The new order number is on the echoed OrderHed row. Read it with
            // ExtractDto rather than by indexing — the documented way to narrow
            // a dataset to a typed row, and the only visible bridge to one.
            OrderHed header = create.Value.ExtractDto<OrderHed>("OrderHed");

            if (header == null || header.OrderNum == 0)
            {
                Console.WriteLine("  OK — order created, but the saved dataset carries no OrderNum.");
                Console.WriteLine("  Not adding a line: there is no order number to add it to.");
                return;
            }

            Console.WriteLine($"  OK — created sales order {header.OrderNum}.");

            if (linePart == null) return;

            // ---- 5) The line, on the order we just made ---------------------
            //
            // No second prompt. This is the rest of the record already agreed
            // to, and it adds nothing to the server that the one prompt did not
            // name.

            Console.WriteLine();
            Console.WriteLine($"Adding a line for {linePart} to order {header.OrderNum}...");

            var line = await client.SalesOrder
                .AddOrderLineAsync(header.OrderNum, linePart)
                .ConfigureAwait(false);

            if (line.IsFailure)
            {
                Console.WriteLine($"  FAILED: {line.ErrorMessage}");
                if (!string.IsNullOrEmpty(line.CorrelationId)) Console.WriteLine($"  CorrelationId: {line.CorrelationId}");

                // The order exists either way — it was committed above. Say which
                // of the two situations this is, because they need different
                // responses: Uncommitted is safe to retry, Indeterminate is not.
                if (line.FailureStage == FailureStage.Uncommitted)
                    Console.WriteLine($"  No line was written. Order {header.OrderNum} stands with none.");
                else
                    Console.WriteLine($"  The commit was attempted — check order {header.OrderNum} "
                                    + "for a line before adding another.");

                foreach (string s in line.Steps)
                    Console.WriteLine($"    {s}");
                return;
            }

            // AddOrderLineAsync returns the order dataset as MasterUpdate echoed
            // it, so the lines are countable off the same document.
            var lines = line.Value.ExtractDtoList<OrderDtl>("OrderDtl");
            Console.WriteLine($"  OK — order {header.OrderNum} now has {lines.Count} line(s).");

            foreach (var d in lines)
                Console.WriteLine($"    line {d.OrderLine,-4} {d.PartNum,-20} qty={d.SellingQuantity}");

            // The step trail is the point of the orchestrators — print it, since
            // this is the POC that demonstrates one.
            Console.WriteLine();
            Console.WriteLine("  What AddOrderLineAsync did:");
            foreach (string s in line.Steps)
                Console.WriteLine($"    {s}");
        }
    }
}
