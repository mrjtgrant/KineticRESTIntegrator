using System;
using EpicorSvcs;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="OperationResult{T}"/> — the standard return type
    /// for Keri operations. These are pure, offline tests: no Epicor server,
    /// no network. They prove the success/failure factories behave as the
    /// rest of the library assumes.
    /// </summary>
    public class OperationResultTests
    {
        // -- Success factory -------------------------------------------------

        [Fact]
        public void Success_SetsIsSuccessTrueAndIsFailureFalse()
        {
            var result = OperationResult<string>.Success("hello");

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
        }

        [Fact]
        public void Success_CarriesTheValue()
        {
            var result = OperationResult<int>.Success(42);

            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void Success_HasNoErrorMessage()
        {
            var result = OperationResult<string>.Success("ok");

            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void Success_CanCarryRawResponseAndResourcePath()
        {
            var raw = new JObject { ["x"] = 1 };
            var result = OperationResult<string>.Success("ok", raw, "Erp.BO.Thing/Method");

            Assert.Same(raw, result.RawResponse);
            Assert.Equal("Erp.BO.Thing/Method", result.ResourcePath);
        }

        // -- Failure factory (Epicor-reported error) -------------------------

        [Fact]
        public void Failure_FromMessage_SetsIsFailureTrue()
        {
            var result = OperationResult<string>.Failure("something broke");

            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void Failure_FromMessage_CarriesAllErrorDetail()
        {
            var raw = new JObject { ["ErrorMessage"] = "bad" };
            var result = OperationResult<string>.Failure("bad", 404, "Erp.BO.Part/GetByID", raw);

            Assert.Equal("bad", result.ErrorMessage);
            Assert.Equal(404, result.StatusCode);
            Assert.Equal("Erp.BO.Part/GetByID", result.ResourcePath);
            Assert.Same(raw, result.RawResponse);
        }

        [Fact]
        public void Failure_FromMessage_HasNullException()
        {
            // An Epicor-reported error (HTTP 4xx/5xx with a body) is not a
            // transport exception — Exception should stay null.
            var result = OperationResult<string>.Failure("epicor said no");

            Assert.Null(result.Exception);
        }

        // -- Failure factory (transport-level exception) ---------------------

        [Fact]
        public void Failure_FromException_CarriesExceptionAndItsMessage()
        {
            var ex = new TimeoutException("the request timed out");
            var result = OperationResult<string>.Failure(ex, "Erp.BO.Part/GetByID");

            Assert.True(result.IsFailure);
            Assert.Same(ex, result.Exception);
            Assert.Equal("the request timed out", result.ErrorMessage);
            Assert.Equal("Erp.BO.Part/GetByID", result.ResourcePath);
        }

        [Fact]
        public void Failure_FromNullException_DoesNotThrow()
        {
            // Defensive: the factory tolerates a null exception rather than
            // throwing while trying to build a failure result.
            var result = OperationResult<string>.Failure((Exception)null);

            Assert.True(result.IsFailure);
            Assert.Null(result.ErrorMessage);
        }

        // -- The "Value on failure" contract ---------------------------------

        [Fact]
        public void Failure_ValueIsDefault_ForReferenceType()
        {
            // Documented contract: reading Value on a failed result returns
            // default(T) — it does NOT throw. Callers must check IsSuccess.
            var result = OperationResult<string>.Failure("nope");

            Assert.Null(result.Value);
        }

        [Fact]
        public void Failure_ValueIsDefault_ForValueType()
        {
            var result = OperationResult<int>.Failure("nope");

            Assert.Equal(0, result.Value);
        }
    }
}
