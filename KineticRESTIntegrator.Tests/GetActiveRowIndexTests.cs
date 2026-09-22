using Keri.Epicor;
using Keri.Epicor.Dtos;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="EpicorSvc.GetActiveRowIndex"/> — the helper that
    /// finds the row an Epicor <c>GetNew*</c> call just added, or the last row a
    /// caller marked as changed, by scanning <c>RowMod</c>.
    /// </summary>
    /// <remarks>
    /// The "nothing is marked" case returned an index off a null match before
    /// and threw a <see cref="System.NullReferenceException"/>, contradicting
    /// the documented null return. Offline — construction is network-free, as
    /// in SelectForTests.
    /// </remarks>
    public class GetActiveRowIndexTests
    {
        private static EpicorSvc NewSvc()
        {
            return new EpicorSvc(new EpicorRestSessionKey());
        }

        private static JArray Rows(params string[] rowMods)
        {
            var rows = new JArray();
            foreach (string mod in rowMods)
                rows.Add(new JObject(new JProperty("RowMod", mod)));
            return rows;
        }

        [Fact]
        public void FindsAnAddedRow()
        {
            Assert.Equal(1, NewSvc().GetActiveRowIndex(Rows("", "A")));
        }

        [Fact]
        public void FindsAnUpdatedRow()
        {
            Assert.Equal(0, NewSvc().GetActiveRowIndex(Rows("U", "")));
        }

        [Fact]
        public void ReturnsTheLastMarkedRow()
        {
            Assert.Equal(2, NewSvc().GetActiveRowIndex(Rows("A", "", "U")));
        }

        [Fact]
        public void ReturnsNullWhenNoRowIsMarked()
        {
            Assert.Null(NewSvc().GetActiveRowIndex(Rows("", "", "")));
        }

        [Fact]
        public void ReturnsNullForAnEmptyTable()
        {
            Assert.Null(NewSvc().GetActiveRowIndex(new JArray()));
        }

        [Fact]
        public void ReturnsNullWhenRowsCarryNoRowMod()
        {
            var rows = new JArray { new JObject(new JProperty("PartNum", "ABC")) };
            Assert.Null(NewSvc().GetActiveRowIndex(rows));
        }

        [Fact]
        public void ReturnsNullForNull()
        {
            Assert.Null(NewSvc().GetActiveRowIndex(null));
        }
    }
}
