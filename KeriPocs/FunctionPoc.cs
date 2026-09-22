using System;
using System.Threading.Tasks;
using Keri.Epicor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KeriPocs
{
    /// <summary>
    /// Calls one Epicor Function through <see cref="FunctionSvc"/> and prints
    /// the raw result. Which function to call comes from environment variables,
    /// so nothing about a particular Epicor install is committed to the repo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set these before running, then <c>dotnet run --project KeriPocs</c>:
    /// </para>
    /// <code>
    /// $env:KERI_POC_FUNCTION_LIB    = "YourLibrary"
    /// $env:KERI_POC_FUNCTION_NAME   = "YourFunction"
    /// $env:KERI_POC_FUNCTION_PARAMS = '{"SomeInput":"value"}'   # optional
    /// $env:KERI_POC_FUNCTION_STAGED = "true"                    # optional
    /// </code>
    /// <para>
    /// The POC skips itself when the library and function are not both set.
    /// </para>
    /// <para>
    /// <b>A function can write.</b> Epicor Functions are arbitrary server-side
    /// code, and this POC cannot tell a lookup from a posting routine. It
    /// therefore honors the same <see cref="PocConfig.AllowWrites"/> gate as the
    /// other write POCs: with the gate off it prints the URL and payload it
    /// would send and stops. Set <c>KERI_POC_FUNCTION_READONLY=true</c> to
    /// declare that your function only reads, and it will run without the gate.
    /// </para>
    /// </remarks>
    internal static class FunctionPoc
    {
        private const string LibVar      = "KERI_POC_FUNCTION_LIB";
        private const string NameVar     = "KERI_POC_FUNCTION_NAME";
        private const string ParamsVar   = "KERI_POC_FUNCTION_PARAMS";
        private const string StagedVar   = "KERI_POC_FUNCTION_STAGED";
        private const string ReadOnlyVar = "KERI_POC_FUNCTION_READONLY";

        public static async Task RunAsync(EpicorClient client)
        {
            PocBanner.Section("Epicor Function POC");

            string library  = Environment.GetEnvironmentVariable(LibVar);
            string function = Environment.GetEnvironmentVariable(NameVar);

            if (string.IsNullOrWhiteSpace(library) || string.IsNullOrWhiteSpace(function))
            {
                Console.WriteLine($"Skipped — set {LibVar} and {NameVar} to call a function.");
                return;
            }

            bool staged   = IsTrue(Environment.GetEnvironmentVariable(StagedVar));
            bool readOnly = IsTrue(Environment.GetEnvironmentVariable(ReadOnlyVar));

            // Parse the input parameters up front so a typo in the JSON is
            // reported here rather than as a confusing server-side error.
            JObject parameters;
            string raw = Environment.GetEnvironmentVariable(ParamsVar);
            if (string.IsNullOrWhiteSpace(raw))
            {
                parameters = new JObject();
            }
            else
            {
                try
                {
                    parameters = JObject.Parse(raw);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"  {ParamsVar} is not a JSON object: {ex.Message}");
                    return;
                }
            }

            Console.WriteLine($"Library:    {library}");
            Console.WriteLine($"Function:   {function}");
            Console.WriteLine($"Staged:     {staged}");
            Console.WriteLine($"Parameters: {parameters.ToString(Formatting.None)}");
            Console.WriteLine($"Endpoint:   {client.Session.BaseUrl}/api/v2/efx/"
                              + (staged ? "staging/" : "") + $"{client.Session.Company}/{library}/{function}");
            Console.WriteLine();

            if (!readOnly && !PocConfig.AllowWrites)
            {
                Console.WriteLine("  Dry run — a function may write, so the call is gated.");
                Console.WriteLine($"  Set {ReadOnlyVar}=true if this function only reads,");
                Console.WriteLine("  or KERI_POC_ALLOW_WRITES=true to arm writes.");
                return;
            }

            var result = await client.Function
                .InvokeAsync(library, function, parameters, staged)
                .ConfigureAwait(false);

            // Everything below is what the SDK gives a caller back. Printing all
            // of it is the point of this POC: it shows how a function's output
            // parameters arrive, and what a failure looks like.
            Console.WriteLine($"IsSuccess:    {result.IsSuccess}");
            Console.WriteLine($"ResourcePath: {result.ResourcePath}");

            if (result.IsFailure)
            {
                Console.WriteLine($"  FAILED: {result.ErrorMessage}");
                if (result.StatusCode.HasValue) Console.WriteLine($"  HTTP {result.StatusCode}");
                if (!string.IsNullOrEmpty(result.ErrorType)) Console.WriteLine($"  ErrorType: {result.ErrorType}");
                if (!string.IsNullOrEmpty(result.CorrelationId)) Console.WriteLine($"  CorrelationId: {result.CorrelationId}");
                return;
            }

            JObject outputs = result.Value;
            Console.WriteLine($"Output parameters ({outputs.Count}):");
            Console.WriteLine(outputs.Count == 0
                ? "  (none — the function returned no output parameters)"
                : outputs.ToString(Formatting.Indented));

            // Read a single output by name, the way calling code would.
            foreach (var property in outputs.Properties())
            {
                Console.WriteLine($"  {property.Name} = {property.Value} ({property.Value.Type})");
            }
        }

        private static bool IsTrue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            switch (value.Trim().ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                case "on":
                    return true;
                default:
                    return false;
            }
        }
    }
}
