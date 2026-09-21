using Keri.Files;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests.Files
{
    /// <summary>
    /// Tests for <see cref="TabularRenderer.ConvertJArrayToHTMLTable"/> — encoding
    /// and missing values.
    /// </summary>
    public class HtmlTableRenderingTests
    {
        [Fact]
        public void CellValuesAreHtmlEncoded()
        {
            var rows = JArray.Parse("[{ \"Notes\": \"<script>alert(1)</script> & more\" }]");

            string html = TabularRenderer.ConvertJArrayToHTMLTable(rows);

            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; more", html);
        }

        [Fact]
        public void HeadersAreHtmlEncoded()
        {
            var rows = JArray.Parse("[{ \"<b>Name</b>\": \"x\" }]");

            string html = TabularRenderer.ConvertJArrayToHTMLTable(rows);

            Assert.DoesNotContain("<b>Name</b>", html);
            Assert.Contains("&lt;b&gt;Name&lt;/b&gt;", html);
        }

        [Fact]
        public void ARowMissingAColumnGetsAnEmptyCell()
        {
            var rows = JArray.Parse("[{ \"A\": \"1\", \"B\": \"2\" }, { \"A\": \"3\" }]");

            string html = TabularRenderer.ConvertJArrayToHTMLTable(rows);

            Assert.Contains(">3</td>", html);
            Assert.EndsWith("</table>", html);
        }

        [Fact]
        public void ANullValueGetsAnEmptyCell()
        {
            var rows = JArray.Parse("[{ \"A\": null }]");

            string html = TabularRenderer.ConvertJArrayToHTMLTable(rows);

            Assert.Contains("style='white-space: nowrap;'></td>", html);
        }

        [Fact]
        public void NoRowsGivesAnEmptyString()
        {
            Assert.Equal(string.Empty, TabularRenderer.ConvertJArrayToHTMLTable(new JArray()));
        }
    }
}
