using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor
{
    /// <summary>
    /// Which UD tables this installation has claimed, and what the claimed ones
    /// are being used for.
    /// </summary>
    /// <remarks>
    /// Epicor gives every installation the same fixed set of UD tables and no
    /// screen that says which are in use. Choosing one for a new purpose means
    /// finding an empty one, and this is how to find it.
    /// </remarks>
    public partial class UDTableSvc
    {
        /// <summary>
        /// Epicor's own audit column, ordered on to make the sampled row the
        /// newest one.
        /// </summary>
        private const string LedgerOrderColumn = "ChangeDate";

        /// <summary>
        /// The column Keri's convention keeps a UD table's column legend in.
        /// </summary>
        private const string LedgerLegendColumn = "Character10";

        /// <summary>
        /// Every UD table Epicor provides, parents only.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The set is fixed per Epicor version, so it is named here rather than
        /// discovered — there is no call that enumerates it. A name this server
        /// does not have is reported as unreadable rather than silently skipped,
        /// which is what keeps a wrong list visible.
        /// </para>
        /// <para>
        /// Child tables (<c>UD01A</c> and so on) are not included: a child only
        /// matters once its parent is in use. Pass your own list to
        /// <see cref="GetLedgerAsync"/> to cover them, or to narrow the read.
        /// </para>
        /// </remarks>
        public static IReadOnlyList<string> TableNames { get; } = BuildTableNames();

        private static IReadOnlyList<string> BuildTableNames()
        {
            var names = new List<string>();
            for (int i = 1; i <= 40; i++)
                names.Add("UD" + i.ToString("00", CultureInfo.InvariantCulture));
            for (int i = 100; i <= 110; i++)
                names.Add("UD" + i.ToString(CultureInfo.InvariantCulture));
            return names.AsReadOnly();
        }

        /// <summary>
        /// Reads one UD table and reports whether anything is using it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One request for one row. A row came back or it did not, and that alone
        /// settles whether the table is claimed — no count is asked for, so no
        /// server's limit on counting can change the answer. The row itself
        /// carries the rest: which of <c>Key1</c>–<c>Key5</c> the table populates,
        /// the column legend, and the change date.
        /// </para>
        /// <para>
        /// A table this server does not have comes back as a success with
        /// <see cref="UDTableUsage.IsReadable"/> false and the reason in
        /// <see cref="UDTableUsage.Note"/>. The call worked; the table is what
        /// was missing, and that is a different answer from an empty table.
        /// </para>
        /// <example>
        /// <code>
        /// var usage = await client.UDTable.GetUsageAsync("UD07");
        /// if (usage.IsSuccess &amp;&amp; usage.Value.IsUnclaimed)
        ///     Console.WriteLine("UD07 is free.");
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="table">The UD table to read, e.g. <c>UD07</c>.</param>
        /// <param name="newestFirst">
        /// Order on Epicor's audit column so the sampled row is the most recently
        /// changed one. A server that will not order on it returns no
        /// <see cref="UDTableUsage.LastChanged"/>; pass false to skip the attempt.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<OperationResult<UDTableUsage>> GetUsageAsync(
            string table,
            bool newestFirst = true,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(table))
                throw new ArgumentException("A UD table name is required.", nameof(table));

            string svc = string.Format(
                CultureInfo.InvariantCulture, "Ice.BO.{0}Svc/{0}s?$top=1", table);

            if (newestFirst) svc += "&$orderby=" + LedgerOrderColumn + " desc";

            JObject response = await RestCallAsync(svc, null, ct).ConfigureAwait(false);

            return OperationResult<UDTableUsage>.Success(ReadUsage(table, response), response);
        }

        /// <summary>
        /// Reads every UD table and reports which are unclaimed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// One request per table, in sequence — the default list is fifty-one of
        /// them, so a full run takes as long as fifty-one round trips to your
        /// server. Pass a narrower list when that matters.
        /// </para>
        /// <para>
        /// Whether this server accepts ordering on the audit column is settled
        /// once, by trying it on the first table rather than by reading the
        /// wording of a rejection, and the answer applies to the rest of the run.
        /// </para>
        /// <para>
        /// The result is in the order read, and always has one entry per name
        /// asked for — a table that could not be read is present and says why.
        /// </para>
        /// <example>
        /// <code>
        /// var ledger = await client.UDTable.GetLedgerAsync();
        /// foreach (var t in ledger.Value.Where(t =&gt; t.IsUnclaimed))
        ///     Console.WriteLine(t.Table + " is free.");
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="tables">
        /// The tables to read. Defaults to <see cref="TableNames"/>; pass your own
        /// to narrow the read or to include child tables.
        /// </param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<OperationResult<List<UDTableUsage>>> GetLedgerAsync(
            IEnumerable<string> tables = null,
            CancellationToken ct = default)
        {
            List<string> names = (tables ?? TableNames)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            var ledger = new List<UDTableUsage>();
            if (names.Count == 0) return OperationResult<List<UDTableUsage>>.Success(ledger);

            bool newestFirst = await OrderingAcceptedAsync(names[0], ct).ConfigureAwait(false);

            foreach (string table in names)
            {
                OperationResult<UDTableUsage> one =
                    await GetUsageAsync(table, newestFirst, ct).ConfigureAwait(false);

                ledger.Add(one.IsSuccess
                    ? one.Value
                    : new UDTableUsage { Table = table, Note = one.ErrorMessage });
            }

            return OperationResult<List<UDTableUsage>>.Success(ledger);
        }

        /// <summary>
        /// Whether this server accepts ordering on the audit column, decided by
        /// trying it.
        /// </summary>
        /// <remarks>
        /// A UD table is mostly <c>Key1</c>–<c>Key5</c> and the numbered user
        /// columns, and whether it carries an audit column is a question about
        /// this server. Matching on the wording of a rejection would be a guess a
        /// version or a locale could break; two reads of one table are an answer.
        /// Ordered read succeeds — ordering works. It fails where the unordered
        /// one succeeds — the column is the problem. Both fail — the table is,
        /// and ordering stays on so the failure is reported as itself.
        /// </remarks>
        /// <param name="probe">The table to decide it on.</param>
        /// <param name="ct">Cancellation token.</param>
        private async Task<bool> OrderingAcceptedAsync(string probe, CancellationToken ct)
        {
            OperationResult<UDTableUsage> ordered =
                await GetUsageAsync(probe, true, ct).ConfigureAwait(false);

            if (ordered.IsSuccess && ordered.Value.IsReadable) return true;

            OperationResult<UDTableUsage> plain =
                await GetUsageAsync(probe, false, ct).ConfigureAwait(false);

            return !(plain.IsSuccess && plain.Value.IsReadable);
        }

        /// <summary>
        /// Reads a one-row response into a usage record.
        /// </summary>
        /// <remarks>
        /// The set of rows is the answer: present and empty means the table was
        /// read and holds nothing; present with a row means something is using it;
        /// absent means the read did not happen, which is a third answer and is
        /// kept as one.
        /// </remarks>
        /// <param name="table">The table the response came from.</param>
        /// <param name="response">What the transport returned.</param>
        internal static UDTableUsage ReadUsage(string table, JObject response)
        {
            var usage = new UDTableUsage { Table = table };

            if (response == null)
            {
                usage.Note = "no response";
                return usage;
            }

            var error = (string)response["ErrorMessage"];
            if (error != null)
            {
                usage.Note = OneLine(error);
                return usage;
            }

            var rows = response["value"] as JArray;
            if (rows == null)
            {
                usage.Note = "no row set in the response";
                return usage;
            }

            usage.IsReadable = true;

            var row = rows.FirstOrDefault() as JObject;
            if (row == null) return usage;

            usage.IsInUse = true;

            for (int k = 1; k <= 5; k++)
            {
                string name = "Key" + k.ToString(CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace((string)row[name])) usage.Keys.Add(name);
            }

            var legend = (string)row[LedgerLegendColumn];
            if (!string.IsNullOrWhiteSpace(legend)) usage.Legend = legend.Trim();

            usage.LastChanged = DateOnly((string)row[LedgerOrderColumn]);

            return usage;
        }

        /// <summary>
        /// The day part of a schema value, or null when there is not one.
        /// </summary>
        /// <remarks>
        /// Parsed rather than cast: a value in a shape this does not expect costs
        /// the date, not the row.
        /// </remarks>
        /// <param name="value">The raw value from the row.</param>
        internal static string DateOnly(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            DateTime parsed;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                     DateTimeStyles.None, out parsed)
                ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : null;
        }

        /// <summary>One line of a message, short enough to report in a column.</summary>
        /// <param name="message">The message to shorten.</param>
        internal static string OneLine(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "unreadable";

            message = message.Replace("\r", " ").Replace("\n", " ").Trim();
            return message.Length <= 70 ? message : message.Substring(0, 67) + "...";
        }
    }
}
