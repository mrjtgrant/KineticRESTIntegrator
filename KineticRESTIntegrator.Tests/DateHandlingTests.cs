using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Keri.Epicor;
using Keri.RestTransport;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// A date must name the same day wherever the call ran. Offline, over a
    /// scripted handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured 2026-09-30 against <c>JobHead.DueDate</c> for job 06105. The stored
    /// date is 2019-10-02. The business-object dataset returns it with no zone and
    /// it bound to 2019-10-02. The OData entity set returns it carrying a numeric
    /// offset, and it bound to <b>2019-10-01</b> on a machine at UTC-07:00 — one
    /// day early, from the same column.
    /// </para>
    /// <para>
    /// Neither package configured serialization, so Newtonsoft's default
    /// <c>RoundtripKind</c> applied: a value carrying a numeric offset is converted
    /// into the reading machine's zone and marked <see cref="DateTimeKind.Local"/>.
    /// A .NET <see cref="DateTime"/> cannot hold an arbitrary offset, so that
    /// conversion is the only way to preserve the instant in that type — which is
    /// why the default does it, and why the answer is to fix the zone rather than
    /// to try to keep the value "as written".
    /// </para>
    /// <para>
    /// The conversion happens where the response body is parsed —
    /// <c>RestConnect.ParseSuccessBody</c> — not where a DTO is bound, so these
    /// tests drive a real call through the transport. A test that parsed its own
    /// <see cref="JObject"/> would sit below the fix and stay red after it.
    /// </para>
    /// </remarks>
    public class DateHandlingTests
    {
        // A minimal row standing in for any DTO with a date, so the test does not
        // depend on a shipping DTO's column set.
        private class DatedRow
        {
            public string Id { get; set; }
            public DateTime? DueDate { get; set; }
        }

        /// <summary>
        /// The wire value measured on 2026-09-30: midnight on 2019-10-02 in a zone
        /// at UTC-05:00. The offset is numeric rather than <c>Z</c>, which the
        /// observation itself establishes — RoundtripKind leaves a <c>Z</c> value as
        /// <see cref="DateTimeKind.Utc"/> and only converts an offset-bearing value
        /// to <see cref="DateTimeKind.Local"/>.
        /// </summary>
        private const string EntitySetBody =
            @"{""value"":[{""Id"":""06105"",""DueDate"":""2019-10-02T00:00:00-05:00""}]}";

        /// <summary>The same column as a business object returns it: no zone at all.</summary>
        private const string DatasetBody =
            @"{""ds"":{""JobHead"":[{""Id"":""06105"",""DueDate"":""2019-10-02T00:00:00""}]}}";

        private static RestSessionKey Session(StubHttpHandler handler, out HttpClient client)
        {
            client = new HttpClient(handler);
            return new RestSessionKey
            {
                BaseUrl = "https://example.invalid/server",
                AuthObject = new RestAuthenticationObject { Username = "user", Password = "pass" }
            };
        }

        /// <summary>Runs one scripted body through the real transport and returns the parsed response.</summary>
        private static async Task<JObject> ReadAsync(string body)
        {
            var handler = new StubHttpHandler().Respond(HttpStatusCode.OK, body);
            RestSessionKey session = Session(handler, out HttpClient client);

            using (client)
            using (var connect = new RestConnect(session, client))
            {
                return await connect.RestCallAsync("Erp.BO.JobEntrySvc/JobEntries");
            }
        }

        [Fact]
        public async Task AnEntitySetDateIsNotConvertedIntoTheReadingMachinesZone()
        {
            JObject response = await ReadAsync(EntitySetBody);

            var rows = response.ExtractValueList<DatedRow>();

            Assert.Single(rows);
            Assert.True(rows[0].DueDate.HasValue);

            // Machine-independent: RoundtripKind yields Local on every machine for an
            // offset-bearing value, so this fails everywhere until the conversion is
            // settled. It is what makes this a reliable guard rather than one that
            // passes or fails by geography.
            Assert.NotEqual(DateTimeKind.Local, rows[0].DueDate.Value.Kind);

            // What a caller cares about: the day Epicor meant.
            Assert.Equal(new DateTime(2019, 10, 2), rows[0].DueDate.Value.Date);
        }

        [Fact]
        public async Task TheTwoReadPathsAgreeOnTheDate()
        {
            // The divergence that exposed this: one column, two documented reads,
            // must not name two different days.
            var fromEntitySet = (await ReadAsync(EntitySetBody)).ExtractValueList<DatedRow>();
            DatedRow fromDataset = (await ReadAsync(DatasetBody)).ExtractDto<DatedRow>("JobHead");

            Assert.True(fromEntitySet[0].DueDate.HasValue);
            Assert.True(fromDataset.DueDate.HasValue);

            Assert.Equal(fromDataset.DueDate.Value.Date, fromEntitySet[0].DueDate.Value.Date);
        }

        [Fact]
        public async Task ADatasetDateStillReadsAsTheDayItNames()
        {
            // A business object sends no zone, so this path was already correct.
            // Pinned so the fix cannot quietly move it.
            DatedRow row = (await ReadAsync(DatasetBody)).ExtractDto<DatedRow>("JobHead");

            Assert.True(row.DueDate.HasValue);
            Assert.Equal(new DateTime(2019, 10, 2), row.DueDate.Value.Date);
        }
    }
}
