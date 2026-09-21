using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// A generic row of user-defined-column values, intended to drive
    /// reads and writes against any Epicor UD table (<c>UD01</c>, <c>UD22</c>,
    /// etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>not</b> an Epicor table itself. It's a caller-facing value
    /// container — fill in whichever UD columns matter for your use case, then
    /// pass it to one of <see cref="UDTableSvc"/>'s methods alongside the
    /// <c>UDTable</c> argument that selects which Epicor UD table is being
    /// targeted. It exposes the full standard UD column set: 5 key columns,
    /// 10 <c>Character</c>, 20 <c>ShortChar</c>, 20 <c>Number</c>, 20
    /// <c>Date</c>, and 20 <c>CheckBox</c> columns. <see cref="Company"/>
    /// identifies the Epicor tenant — leave it empty to use the session's
    /// company (the single-company common case), or set it to write to a
    /// different company.
    /// </para>
    /// <para>
    /// <b>Column length limits (Epicor).</b> <c>ShortChar</c> columns hold up
    /// to <b>100</b> characters; <c>Character</c> columns hold up to
    /// <b>1000</b>. These are Epicor's storage limits — exceeding them will
    /// cause the write to fail or truncate.
    /// </para>
    /// <para>
    /// <b>Reserved columns — conventions, not rules.</b> Six columns carry a
    /// suggested purpose so that UD rows written by different features stay
    /// readable and consistent. These are <i>strong suggestions</i> only: the
    /// SDK does not enforce them, validate them, or depend on them. You
    /// are free to ignore or repurpose any of them. They exist to give teams a
    /// shared starting convention and sensible defaults:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///   <see cref="Key1"/> — a user-defined <i>category</i> that groups rows
    ///   of the same kind.
    ///   </description></item>
    ///   <item><description>
    ///   <see cref="Character10"/> — a column legend mapping generic columns
    ///   to caller-defined meanings.
    ///   </description></item>
    ///   <item><description>
    ///   <see cref="ShortChar20"/> — a short keyword or comma-separated
    ///   keyword list.
    ///   </description></item>
    ///   <item><description>
    ///   <see cref="Date20"/> — a transaction timestamp. Defaults to
    ///   <see cref="DateTime.Now"/> at construction time.
    ///   </description></item>
    ///   <item><description>
    ///   <see cref="CheckBox20"/> — a valid / active flag. Defaults to
    ///   <c>true</c>.
    ///   </description></item>
    /// </list>
    /// <para>
    /// See <c>EXAMPLES_EPICOR.md</c> for worked, real-world examples of these
    /// conventions in use.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// var row = new UDRow {
    ///     Key1 = "PRINTED_PACKSLIP_LOG",          // the row category
    ///     Key2 = "PACKSLIP-100457",               // the specific row
    ///     Character10 = "ShortChar01:PackNum|Date01:PrintedDate|Number01:Copies",
    ///     ShortChar01 = "100457",
    ///     Date01 = DateTime.Today,
    ///     Number01 = 2,
    ///     ShortChar20 = "REPRINT",
    ///     // Date20, CheckBox20 default to DateTime.Now / true
    /// };
    /// await client.UDTable.SaveAsync(row, UDTable: "UD22");
    /// </code>
    /// </para>
    /// <para>
    /// <b>Why the Date properties carry a <c>[JsonProperty]</c> attribute</b>
    /// (unlike every other DTO in this library): <see cref="UDTableSvc"/> decides
    /// which columns to send or <c>$select</c> by serializing this object and
    /// checking which property names are present. String columns can simply
    /// be absent by being empty, but <see cref="DateTime"/> is a value type —
    /// a nullable date still serializes (as <c>null</c>) and would therefore
    /// always be "present", forcing unset dates into every read and write.
    /// <see cref="NullValueHandling.Ignore"/> makes a null date drop out of
    /// the serialized object entirely, so the existing column-detection logic
    /// in <see cref="UDTableSvc"/> keeps working unchanged — a date is included
    /// only when the caller actually set it. <see cref="Date20"/> is the one
    /// exception: it is non-nullable and always sent, because a transaction
    /// timestamp is meant to always be written.
    /// </para>
    /// </remarks>
    public class UDRow
    {
        #region Tenant

        /// <summary>
        /// The Epicor company this row belongs to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Leave unset (the default — an empty string) and the empty default
        /// resolves to the session's
        /// <see cref="Dtos.EpicorRestSessionKey.Company"/> at call time. The
        /// empty default does <b>not</b> mean "write an empty <c>Company</c>
        /// to Epicor" — it means "use the session's company." This matches
        /// the everyday single-company case, where the session and the row's
        /// tenant are the same and the caller never thinks about it.
        /// </para>
        /// <para>
        /// Set this explicitly only when writing a row in a <i>different</i>
        /// company than the session's default — the multi-company case. A
        /// non-empty value here wins over the session's company, exactly as a
        /// per-call <c>UDTable</c> argument wins over
        /// <see cref="UDTableSvc.UDTableDefault"/>.
        /// </para>
        /// </remarks>
        public string Company { get; set; } = "";

        #endregion

        #region Key columns

        /// <summary>
        /// Primary key segment 1.
        /// <para>
        /// <b>Required (strong suggestion).</b> Use this as a user-defined
        /// <i>category</i> that groups rows of the same kind — it lets one UD
        /// table hold many distinct logical row types. Examples:
        /// <c>"PRINTED_PACKSLIP_LOG"</c>, <c>"WEBSITE_INQUIRY"</c>,
        /// <c>"REPAIR_INTAKE"</c>. The SDK does not enforce this; leave
        /// it unset and it stays null, but a row without a Key1 category is
        /// hard to find or organize later.
        /// </para>
        /// </summary>
        public string Key1 { get; set; }

        /// <summary>Primary key segment 2. Required: identifies the specific row.</summary>
        public string Key2 { get; set; }

        /// <summary>Primary key segment 3. Null when unset (coalesced to an empty string on the wire).</summary>
        public string Key3 { get; set; }

        /// <summary>Primary key segment 4. Null when unset (coalesced to an empty string on the wire).</summary>
        public string Key4 { get; set; }

        /// <summary>Primary key segment 5. Null when unset (coalesced to an empty string on the wire).</summary>
        public string Key5 { get; set; }

        #endregion

        #region Character columns (max 1000 chars each)

        /// <summary>Character column 01. Up to 1000 characters.</summary>
        public string Character01 { get; set; } = "";

        /// <summary>Character column 02. Up to 1000 characters.</summary>
        public string Character02 { get; set; } = "";

        /// <summary>Character column 03. Up to 1000 characters.</summary>
        public string Character03 { get; set; } = "";

        /// <summary>Character column 04. Up to 1000 characters.</summary>
        public string Character04 { get; set; } = "";

        /// <summary>Character column 05. Up to 1000 characters.</summary>
        public string Character05 { get; set; } = "";

        /// <summary>Character column 06. Up to 1000 characters.</summary>
        public string Character06 { get; set; } = "";

        /// <summary>Character column 07. Up to 1000 characters.</summary>
        public string Character07 { get; set; } = "";

        /// <summary>Character column 08. Up to 1000 characters.</summary>
        public string Character08 { get; set; } = "";

        /// <summary>Character column 09. Up to 1000 characters.</summary>
        public string Character09 { get; set; } = "";

        /// <summary>
        /// Character column 10. Up to 1000 characters.
        /// <para>
        /// <b>Reserved (strong suggestion).</b> Use this to hold a <i>column
        /// legend</i> for the row — a record of which generic UD columns hold
        /// what. The format is a <c>|</c>-separated list of
        /// <c>column:meaning</c> pairs, for example
        /// <c>"ShortChar02:PartNum|Number05:Calculated_AvailableQty"</c>. The
        /// meaning can be anything that helps name the column: a business
        /// object field (<c>PartNum</c>, <c>OrderNum</c>), a BAQ column
        /// (<c>Calculated_AvailableQty</c>), or a label meaningful only to
        /// your own process.
        /// </para>
        /// <para>
        /// The string is literal and entirely yours — store it, ignore it, or
        /// parse it however you see fit. For convenience the SDK
        /// provides <see cref="UDTableSvc.ParseColumnLegend"/> and
        /// <see cref="UDTableSvc.BuildColumnLegend"/> to convert between this
        /// string and a dictionary, and <see cref="ToMappedValues"/> to
        /// re-key this row's values by their meanings. Those helpers assume
        /// the <c>|</c> / <c>:</c> format above; they are a quick-start
        /// convention, not a requirement. The SDK does not enforce any
        /// of this. The 1000-character <c>Character</c> limit leaves ample
        /// room for a full legend.
        /// </para>
        /// </summary>
        public string Character10 { get; set; } = "";

        #endregion

        #region ShortChar columns (max 100 chars each)

        /// <summary>ShortChar column 01. Up to 100 characters.</summary>
        public string ShortChar01 { get; set; } = "";

        /// <summary>ShortChar column 02. Up to 100 characters.</summary>
        public string ShortChar02 { get; set; } = "";

        /// <summary>ShortChar column 03. Up to 100 characters.</summary>
        public string ShortChar03 { get; set; } = "";

        /// <summary>ShortChar column 04. Up to 100 characters.</summary>
        public string ShortChar04 { get; set; } = "";

        /// <summary>ShortChar column 05. Up to 100 characters.</summary>
        public string ShortChar05 { get; set; } = "";

        /// <summary>ShortChar column 06. Up to 100 characters.</summary>
        public string ShortChar06 { get; set; } = "";

        /// <summary>ShortChar column 07. Up to 100 characters.</summary>
        public string ShortChar07 { get; set; } = "";

        /// <summary>ShortChar column 08. Up to 100 characters.</summary>
        public string ShortChar08 { get; set; } = "";

        /// <summary>ShortChar column 09. Up to 100 characters.</summary>
        public string ShortChar09 { get; set; } = "";

        /// <summary>ShortChar column 10. Up to 100 characters.</summary>
        public string ShortChar10 { get; set; } = "";

        /// <summary>ShortChar column 11. Up to 100 characters.</summary>
        public string ShortChar11 { get; set; } = "";

        /// <summary>ShortChar column 12. Up to 100 characters.</summary>
        public string ShortChar12 { get; set; } = "";

        /// <summary>ShortChar column 13. Up to 100 characters.</summary>
        public string ShortChar13 { get; set; } = "";

        /// <summary>ShortChar column 14. Up to 100 characters.</summary>
        public string ShortChar14 { get; set; } = "";

        /// <summary>ShortChar column 15. Up to 100 characters.</summary>
        public string ShortChar15 { get; set; } = "";

        /// <summary>ShortChar column 16. Up to 100 characters.</summary>
        public string ShortChar16 { get; set; } = "";

        /// <summary>ShortChar column 17. Up to 100 characters.</summary>
        public string ShortChar17 { get; set; } = "";

        /// <summary>ShortChar column 18. Up to 100 characters.</summary>
        public string ShortChar18 { get; set; } = "";

        /// <summary>ShortChar column 19. Up to 100 characters.</summary>
        public string ShortChar19 { get; set; } = "";

        /// <summary>
        /// ShortChar column 20. Up to 100 characters.
        /// <para>
        /// <b>Reserved (strong suggestion).</b> Use this for a short keyword
        /// or a comma-separated list of keywords that tag the row — a quick,
        /// filterable classification. Keywords can be as short as you like and
        /// can be repurposed freely. Examples: <c>"REPAIR"</c>,
        /// <c>"LOST"</c>, <c>"RETURN"</c>, <c>"{USERNAME}"</c>, or
        /// <c>"RETURN,WARRANTY,EXPEDITE"</c>. Stay within the 100-character
        /// <c>ShortChar</c> limit. The SDK does not enforce this.
        /// </para>
        /// </summary>
        public string ShortChar20 { get; set; } = "";

        #endregion

        #region Number columns

        /// <summary>Number column 01.</summary>
        public double Number01 { get; set; }

        /// <summary>Number column 02.</summary>
        public double Number02 { get; set; }

        /// <summary>Number column 03.</summary>
        public double Number03 { get; set; }

        /// <summary>Number column 04.</summary>
        public double Number04 { get; set; }

        /// <summary>Number column 05.</summary>
        public double Number05 { get; set; }

        /// <summary>Number column 06.</summary>
        public double Number06 { get; set; }

        /// <summary>Number column 07.</summary>
        public double Number07 { get; set; }

        /// <summary>Number column 08.</summary>
        public double Number08 { get; set; }

        /// <summary>Number column 09.</summary>
        public double Number09 { get; set; }

        /// <summary>Number column 10.</summary>
        public double Number10 { get; set; }

        /// <summary>Number column 11.</summary>
        public double Number11 { get; set; }

        /// <summary>Number column 12.</summary>
        public double Number12 { get; set; }

        /// <summary>Number column 13.</summary>
        public double Number13 { get; set; }

        /// <summary>Number column 14.</summary>
        public double Number14 { get; set; }

        /// <summary>Number column 15.</summary>
        public double Number15 { get; set; }

        /// <summary>Number column 16.</summary>
        public double Number16 { get; set; }

        /// <summary>Number column 17.</summary>
        public double Number17 { get; set; }

        /// <summary>Number column 18.</summary>
        public double Number18 { get; set; }

        /// <summary>Number column 19.</summary>
        public double Number19 { get; set; }

        /// <summary>Number column 20.</summary>
        public double Number20 { get; set; }

        #endregion

        #region Date columns

        // Date01-19 are nullable with NullValueHandling.Ignore so that an
        // unset date drops out of the serialized object entirely, keeping
        // UDTableSvc's column-detection logic working unchanged. See the class
        // remarks for the full rationale. Date20 is the deliberate exception.

        /// <summary>Date column 01. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date01 { get; set; }

        /// <summary>Date column 02. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date02 { get; set; }

        /// <summary>Date column 03. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date03 { get; set; }

        /// <summary>Date column 04. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date04 { get; set; }

        /// <summary>Date column 05. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date05 { get; set; }

        /// <summary>Date column 06. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date06 { get; set; }

        /// <summary>Date column 07. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date07 { get; set; }

        /// <summary>Date column 08. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date08 { get; set; }

        /// <summary>Date column 09. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date09 { get; set; }

        /// <summary>Date column 10. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date10 { get; set; }

        /// <summary>Date column 11. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date11 { get; set; }

        /// <summary>Date column 12. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date12 { get; set; }

        /// <summary>Date column 13. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date13 { get; set; }

        /// <summary>Date column 14. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date14 { get; set; }

        /// <summary>Date column 15. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date15 { get; set; }

        /// <summary>Date column 16. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date16 { get; set; }

        /// <summary>Date column 17. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date17 { get; set; }

        /// <summary>Date column 18. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date18 { get; set; }

        /// <summary>Date column 19. Null when unset.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? Date19 { get; set; }

        /// <summary>
        /// Date column 20.
        /// <para>
        /// <b>Reserved (strong suggestion).</b> Use this as the row's
        /// transaction timestamp — when the row was logged or last acted on.
        /// Unlike the other date columns this is non-nullable and always
        /// written; it defaults to <see cref="DateTime.Now"/> at the moment
        /// the <see cref="UDRow"/> is constructed. If the row is built well
        /// before it is sent and you need send-time accuracy, set this
        /// explicitly before the call. The SDK does not enforce this.
        /// </para>
        /// </summary>
        /// 
        public DateTime? Date20 { get; set; } = DateTime.Now;

        #endregion

        #region CheckBox columns

        /// <summary>CheckBox column 01.</summary>
        public bool CheckBox01 { get; set; } = false;

        /// <summary>CheckBox column 02.</summary>
        public bool CheckBox02 { get; set; } = false;

        /// <summary>CheckBox column 03.</summary>
        public bool CheckBox03 { get; set; } = false;

        /// <summary>CheckBox column 04.</summary>
        public bool CheckBox04 { get; set; } = false;

        /// <summary>CheckBox column 05.</summary>
        public bool CheckBox05 { get; set; } = false;

        /// <summary>CheckBox column 06.</summary>
        public bool CheckBox06 { get; set; } = false;

        /// <summary>CheckBox column 07.</summary>
        public bool CheckBox07 { get; set; } = false;

        /// <summary>CheckBox column 08.</summary>
        public bool CheckBox08 { get; set; } = false;

        /// <summary>CheckBox column 09.</summary>
        public bool CheckBox09 { get; set; } = false;

        /// <summary>CheckBox column 10.</summary>
        public bool CheckBox10 { get; set; } = false;

        /// <summary>CheckBox column 11.</summary>
        public bool CheckBox11 { get; set; } = false;

        /// <summary>CheckBox column 12.</summary>
        public bool CheckBox12 { get; set; } = false;

        /// <summary>CheckBox column 13.</summary>
        public bool CheckBox13 { get; set; } = false;

        /// <summary>CheckBox column 14.</summary>
        public bool CheckBox14 { get; set; } = false;

        /// <summary>CheckBox column 15.</summary>
        public bool CheckBox15 { get; set; } = false;

        /// <summary>CheckBox column 16.</summary>
        public bool CheckBox16 { get; set; } = false;

        /// <summary>CheckBox column 17.</summary>
        public bool CheckBox17 { get; set; } = false;

        /// <summary>CheckBox column 18.</summary>
        public bool CheckBox18 { get; set; } = false;

        /// <summary>CheckBox column 19.</summary>
        public bool CheckBox19 { get; set; } = false;

        /// <summary>
        /// CheckBox column 20.
        /// <para>
        /// <b>Reserved (strong suggestion).</b> Use this as the row's
        /// valid / active flag — a quick way to soft-disable a row without
        /// deleting it. Defaults to <c>true</c>. The SDK does not
        /// enforce this.
        /// </para>
        /// </summary>
        public bool CheckBox20 { get; set; } = true;

        #endregion

        #region Column-legend helpers

        /// <summary>
        /// Re-keys this row's column values by their caller-defined meanings,
        /// using the row's own <see cref="Character10"/> legend.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the consumer side of the <see cref="Character10"/>
        /// convention. Each pair in the legend — for example
        /// <c>"ShortChar02:PartNum"</c> — produces an entry in the result
        /// mapping the meaning (<c>"PartNum"</c>) to that column's value as a
        /// string. So a row whose <c>ShortChar02</c> holds <c>"Part1"</c> and
        /// whose legend includes <c>ShortChar02:PartNum</c> yields
        /// <c>result["PartNum"] == "Part1"</c> — letting callers read the row
        /// by their own domain names instead of by opaque generic columns.
        /// </para>
        /// <para>
        /// Values are returned as strings. The generic column family in the
        /// name (<c>Number*</c>, <c>Date*</c>, <c>CheckBox*</c>,
        /// <c>Character*</c>, <c>ShortChar*</c>, <c>Key*</c>) tells you the
        /// underlying type if you need to convert. A column named in the
        /// legend is always included even if its value is empty or default —
        /// the legend declared the mapping. A legend entry that names a
        /// column this class does not have is skipped.
        /// </para>
        /// <para>
        /// If your row's schema is known at compile time, the typed-DTO API
        /// on <see cref="UDTableSvc"/> gives the same access via typed
        /// properties — no dictionary indirection, no string-formatting loss.
        /// This method remains useful when the schema is not known at compile
        /// time (e.g. inspecting legacy rows or scanning a table without a
        /// matching DTO) and for callers working directly with raw
        /// <see cref="UDRow"/> instances.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A dictionary of meaning to column value (as a string). Never null;
        /// empty when <see cref="Character10"/> has no parseable legend.
        /// </returns>
        public Dictionary<string, string> ToMappedValues()
        {
            var result = new Dictionary<string, string>();
            var legend = UDTableSvc.ParseColumnLegend(Character10);

            foreach (var entry in legend)
            {
                string column = entry.Key;
                string meaning = entry.Value;

                var prop = typeof(UDRow).GetProperty(column);
                if (prop == null)
                    continue; // legend names a column this class doesn't have

                result[meaning] = FormatValue(prop.GetValue(this));
            }

            return result;
        }

        // Formats a column value as a string for the mapped-values result.
        // Dates use round-trip ("o") format; other IFormattable values use
        // invariant formatting. A null (an unset nullable Date) becomes an
        // empty string.
        private static string FormatValue(object raw)
        {
            if (raw == null)
                return string.Empty;

            if (raw is DateTime dt)
                return dt.ToString("o", CultureInfo.InvariantCulture);

            if (raw is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return raw.ToString();
        }

        #endregion

        /// <summary>
        /// Unmodeled columns on this row, including installation-specific
        /// custom columns (Epicor's <c>_c</c> suffix convention). Populated
        /// on deserialization with any JSON property the typed DTO does not
        /// have a field for; serialized back out as siblings of the typed
        /// properties. Read or write a custom column by key —
        /// e.g. <c>dto.ExtraData["MyField_c"] = "value"</c>.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
