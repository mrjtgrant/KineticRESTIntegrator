using Keri.Epicor;
using Keri.RestTransport;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="RestConnect.BuildResourceUrl"/> — the URL
    /// join that assembles every outbound request URL from three parts:
    /// an environment, a modifier, and a service path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>How a request URL is shaped.</b> Every outbound REST call in the
    /// library produces a URL of the form
    /// <c>{environment}/{modifier}/{servicePath}</c>. The three parts come
    /// from different layers:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Environment</b> — the application-server base URL, owned by
    ///     <c>RestSessionKey.BaseUrl</c>. Configured per-environment
    ///     (live/pilot/test) and copied verbatim from the URL shown in the
    ///     Epicor client's address bar. For non-Epicor REST APIs it's
    ///     whatever base URL the caller chose. Treated as an opaque string;
    ///     this method only normalizes a stray trailing slash.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Modifier</b> — the per-service URL segment, owned by
    ///     <c>RestAuthenticationObject.DynamicUrlModifier</c>. This is the
    ///     extension point that adapts the transport to different REST
    ///     APIs without changing the transport itself. For Epicor's v2
    ///     OData endpoint, <c>EpicorSvc</c> sets the modifier to
    ///     <c>/api/v2/odata/{Company}/</c> — the company segment is
    ///     substituted at session-construction time from
    ///     <c>EpicorRestSessionKey.Company</c>. For Epicor v1 it would be
    ///     <c>/api/v1/</c>. For a non-Epicor REST API the modifier is
    ///     empty, and the URL collapses to <c>environment/servicePath</c>.
    ///     The modifier is also <i>the</i> place where the v1-vs-v2
    ///     decision lives — setting an API key on the auth object switches
    ///     the modifier shape, which is what flips the request from v1
    ///     into v2 OData mode.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Service path</b> — the per-call segment, supplied by the
    ///     individual service method building its request: e.g.
    ///     <c>"Erp.BO.PartSvc/Parts"</c> or <c>"Erp.BO.SalesOrderSvc/GetByID?orderNum=12345"</c>.
    ///     May carry a query string; the join must not break it.
    ///   </description></item>
    /// </list>
    /// <para>
    /// <b>Why this test exists.</b> The three parts come from three
    /// different sources and are concatenated together. Each source can —
    /// independently of the others — carry an unintended leading or
    /// trailing slash. The environment commonly does, because users
    /// copy-paste it directly from the Epicor client's address bar (which
    /// includes a trailing slash). The modifier sometimes does, because
    /// the canonical Epicor modifier is written as <c>/api/v2/odata/...</c>
    /// with leading and trailing slashes by convention. The service path
    /// rarely does, but a contributor writing a new service wrapper might
    /// type one in. Any one of those slashes, naively concatenated,
    /// produces a malformed URL — and the resulting HTTP error (typically
    /// a 404 from a doubled slash) is uninformative about its root cause.
    /// </para>
    /// <para>
    /// The bug these tests lock down was real and shipped before being
    /// noticed: see the <c>### Fixed</c> note in <c>CHANGELOG.md</c> under
    /// <c>[0.1.1]</c>. <see cref="RestConnect.BuildResourceUrl"/> now
    /// normalizes each seam to exactly one slash, regardless of which
    /// neighbor contributed the slashes. These tests cover the
    /// permutations: a clean input, each kind of unintended slash in
    /// isolation, and all three seams messy at once. They also cover the
    /// degenerate cases (empty modifier for non-Epicor APIs, empty service
    /// path that should yield a usable base URL, null arguments) and the
    /// must-not-break case of a service path carrying a query string.
    /// </para>
    /// <para>
    /// The company segment in the expected URL is <c>EPIC01</c> — a
    /// generic placeholder that matches the README and changelog examples.
    /// It has no significance beyond demonstrating the modifier-substitution
    /// shape; any non-empty company code would exercise the same join
    /// behavior.
    /// </para>
    /// </remarks>
    public class BuildResourceUrlTests
    {
        private const string ExpectedV2 =
            "https://example.epicorsaas.com/server/api/v2/odata/EPIC01/Erp.BO.PartSvc/Parts";

        [Fact]
        public void Environment_WithoutTrailingSlash_JoinsCorrectly()
        {
            string url = RestConnect.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "api/v2/odata/EPIC01/",
                "Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void Environment_WithStrayTrailingSlash_JoinsCorrectly()
        {
            // The user pasted the environment URL with a trailing slash —
            // the common copy-paste artifact from the Epicor client's
            // address bar.
            string url = RestConnect.BuildResourceUrl(
                "https://example.epicorsaas.com/server/",
                "api/v2/odata/EPIC01/",
                "Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void Modifier_WithLeadingAndTrailingSlashes_IsNormalized()
        {
            // EpicorSvc canonically writes the modifier as
            // "/api/v2/odata/{Company}/" — both leading and trailing slash.
            string url = RestConnect.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "/api/v2/odata/EPIC01/",
                "Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void ServicePath_WithLeadingSlash_IsNormalized()
        {
            // A contributor writing a new service wrapper might type a
            // leading slash on the service path. It shouldn't break the join.
            string url = RestConnect.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "api/v2/odata/EPIC01/",
                "/Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void AllSeams_Messy_StillProduceOneCleanUrl()
        {
            // Every seam has a slash problem at once — the worst-case
            // permutation.
            string url = RestConnect.BuildResourceUrl(
                "https://example.epicorsaas.com/server/",
                "/api/v2/odata/EPIC01/",
                "/Erp.BO.PartSvc/Parts");

            Assert.Equal(ExpectedV2, url);
        }

        [Fact]
        public void EmptyModifier_IsOmitted()
        {
            // The non-Epicor case: when the modifier is empty (the default
            // on a bare RestAuthenticationObject), the URL collapses to
            // environment + service path with no extra segment between them.
            string url = RestConnect.BuildResourceUrl(
                "https://api.example.com",
                "",
                "v1/widgets");

            Assert.Equal("https://api.example.com/v1/widgets", url);
        }

        [Fact]
        public void EmptyServicePath_YieldsTheBaseUrl()
        {
            // No service path — the environment (plus modifier, if any) is
            // a valid result. Useful for callers who want to issue requests
            // to a service root.
            string url = RestConnect.BuildResourceUrl(
                "https://api.example.com",
                "",
                "");

            Assert.Equal("https://api.example.com", url);
        }

        [Fact]
        public void EmptyServicePath_WithModifier_YieldsEnvironmentPlusModifier()
        {
            string url = RestConnect.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "api/v1/",
                "");

            Assert.Equal("https://example.epicorsaas.com/server/api/v1", url);
        }

        [Fact]
        public void NullArguments_AreTreatedAsEmpty()
        {
            // Defensive: a null modifier or service path is treated as an
            // empty string, matching the behavior of the degenerate-empty
            // cases above. Callers shouldn't have to defend against this
            // themselves.
            string url = RestConnect.BuildResourceUrl(
                "https://api.example.com",
                null,
                null);

            Assert.Equal("https://api.example.com", url);
        }

        [Fact]
        public void ServicePath_QueryString_IsPreserved()
        {
            // A service path may carry an OData query string. The
            // normalization must not touch anything after the '?'.
            string url = RestConnect.BuildResourceUrl(
                "https://example.epicorsaas.com/server",
                "api/v1/",
                "Erp.BO.PartSvc/Parts?$top=10");

            Assert.Equal(
                "https://example.epicorsaas.com/server/api/v1/Erp.BO.PartSvc/Parts?$top=10",
                url);
        }
    }
}
