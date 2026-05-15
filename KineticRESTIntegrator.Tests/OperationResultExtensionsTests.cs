using System.Collections.Generic;
using EpicorSvcs;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KineticRESTIntegrator.Tests
{
    /// <summary>
    /// Tests for <see cref="OperationResultExtensions"/> — the bridge between
    /// the raw <see cref="JObject"/> transport layer and typed
    /// <see cref="OperationResult{T}"/> returns. Pure, offline tests built on
    /// hand-constructed JObjects shaped like real Epicor responses.
    /// </summary>
    public class OperationResultExtensionsTests
    {
        // A minimal DTO standing in for a real Epicor table row. Kept local
        // to the test so the test does not depend on any particular shipping
        // DTO's column set.
        private class FakeRow
        {
            public string Name { get; set; }
            public int Qty { get; set; }
        }

        // -- ToOperationResult: success path ---------------------------------

        [Fact]
        public void ToOperationResult_NoError_InvokesSuccessSelectorAndSucceeds()
        {
            var response = new JObject { ["greeting"] = "hi" };

            var result = response.ToOperationResult(r => (string)r["greeting"]);

            Assert.True(result.IsSuccess);
            Assert.Equal("hi", result.Value);
            Assert.Same(response, result.RawResponse);
        }

        // -- ToOperationResult: error detection ------------------------------

        [Fact]
        public void ToOperationResult_WithErrorMessage_ProducesFailure()
        {
            // The standard Keri error shape: an "ErrorMessage" property on the
            // response. ToOperationResult must detect it and short-circuit.
            var response = new JObject
            {
                ["ErrorMessage"] = "Part not found",
                ["statusCode"] = 404,
                ["resource"] = "Erp.BO.PartSvc/GetByID"
            };

            var result = response.ToOperationResult(r => "should not be reached");

            Assert.True(result.IsFailure);
            Assert.Equal("Part not found", result.ErrorMessage);
            Assert.Equal(404, result.StatusCode);
            Assert.Equal("Erp.BO.PartSvc/GetByID", result.ResourcePath);
        }

        [Fact]
        public void ToOperationResult_EmptyErrorMessage_IsTreatedAsSuccess()
        {
            // An empty-string ErrorMessage is not an error — the selector
            // should still run.
            var response = new JObject
            {
                ["ErrorMessage"] = "",
                ["payload"] = "data"
            };

            var result = response.ToOperationResult(r => (string)r["payload"]);

            Assert.True(result.IsSuccess);
            Assert.Equal("data", result.Value);
        }

        [Fact]
        public void ToOperationResult_NullResponse_ProducesFailure()
        {
            JObject response = null;

            var result = response.ToOperationResult(r => "unreachable");

            Assert.True(result.IsFailure);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void ToOperationResult_SelectorThrows_IsCaughtAsFailure()
        {
            // If the success selector throws (e.g. unexpected shape), the
            // extension converts it to a failure rather than letting it
            // escape.
            var response = new JObject { ["x"] = 1 };

            var result = response.ToOperationResult<string>(
                r => throw new System.InvalidOperationException("bad shape"));

            Assert.True(result.IsFailure);
            Assert.Equal("bad shape", result.ErrorMessage);
            Assert.NotNull(result.Exception);
        }

        // -- ExtractDto: single-row dataset shape ----------------------------

        [Fact]
        public void ExtractDto_PullsFirstRowOfNamedTable()
        {
            // Standard Epicor BO shape: { "ds": { "TableName": [ {...} ] } }
            var response = new JObject
            {
                ["ds"] = new JObject
                {
                    ["FakeRow"] = new JArray
                    {
                        new JObject { ["Name"] = "Widget", ["Qty"] = 5 },
                        new JObject { ["Name"] = "Gadget", ["Qty"] = 9 }
                    }
                }
            };

            var dto = response.ExtractDto<FakeRow>("FakeRow");

            Assert.NotNull(dto);
            Assert.Equal("Widget", dto.Name);   // first row only
            Assert.Equal(5, dto.Qty);
        }

        [Fact]
        public void ExtractDto_MissingTable_ReturnsDefault()
        {
            var response = new JObject { ["ds"] = new JObject() };

            var dto = response.ExtractDto<FakeRow>("FakeRow");

            Assert.Null(dto);   // default(FakeRow)
        }

        [Fact]
        public void ExtractDto_MissingDs_ReturnsDefault()
        {
            var response = new JObject { ["somethingElse"] = 1 };

            var dto = response.ExtractDto<FakeRow>("FakeRow");

            Assert.Null(dto);
        }

        [Fact]
        public void ExtractDto_EmptyTable_ReturnsDefault()
        {
            var response = new JObject
            {
                ["ds"] = new JObject { ["FakeRow"] = new JArray() }
            };

            var dto = response.ExtractDto<FakeRow>("FakeRow");

            Assert.Null(dto);
        }

        // -- ExtractDtoList: multi-row dataset shape -------------------------

        [Fact]
        public void ExtractDtoList_MaterializesEveryRow()
        {
            var response = new JObject
            {
                ["ds"] = new JObject
                {
                    ["FakeRow"] = new JArray
                    {
                        new JObject { ["Name"] = "A", ["Qty"] = 1 },
                        new JObject { ["Name"] = "B", ["Qty"] = 2 },
                        new JObject { ["Name"] = "C", ["Qty"] = 3 }
                    }
                }
            };

            List<FakeRow> rows = response.ExtractDtoList<FakeRow>("FakeRow");

            Assert.Equal(3, rows.Count);
            Assert.Equal("B", rows[1].Name);
            Assert.Equal(3, rows[2].Qty);
        }

        [Fact]
        public void ExtractDtoList_MissingTable_ReturnsEmptyListNotNull()
        {
            var response = new JObject { ["ds"] = new JObject() };

            List<FakeRow> rows = response.ExtractDtoList<FakeRow>("FakeRow");

            Assert.NotNull(rows);
            Assert.Empty(rows);
        }

        [Fact]
        public void ExtractDtoList_NullResponse_ReturnsEmptyList()
        {
            JObject response = null;

            List<FakeRow> rows = response.ExtractDtoList<FakeRow>("FakeRow");

            Assert.NotNull(rows);
            Assert.Empty(rows);
        }

        // -- ExtractValueList: OData "value" shape ---------------------------

        [Fact]
        public void ExtractValueList_MaterializesODataValueArray()
        {
            // OData / BAQ shape: { "value": [ {...}, {...} ] }
            var response = new JObject
            {
                ["value"] = new JArray
                {
                    new JObject { ["Name"] = "X", ["Qty"] = 10 },
                    new JObject { ["Name"] = "Y", ["Qty"] = 20 }
                }
            };

            List<FakeRow> rows = response.ExtractValueList<FakeRow>();

            Assert.Equal(2, rows.Count);
            Assert.Equal("X", rows[0].Name);
            Assert.Equal(20, rows[1].Qty);
        }

        [Fact]
        public void ExtractValueList_NoValueArray_ReturnsEmptyList()
        {
            var response = new JObject { ["notValue"] = new JArray() };

            List<FakeRow> rows = response.ExtractValueList<FakeRow>();

            Assert.NotNull(rows);
            Assert.Empty(rows);
        }

        [Fact]
        public void ExtractValueList_CanMaterializeRawJObjectRows()
        {
            // BAQ results are returned as List<JObject> — verify the
            // extension handles JObject as its row type, not just DTOs.
            var response = new JObject
            {
                ["value"] = new JArray
                {
                    new JObject { ["Col1"] = "a" },
                    new JObject { ["Col1"] = "b" }
                }
            };

            List<JObject> rows = response.ExtractValueList<JObject>();

            Assert.Equal(2, rows.Count);
            Assert.Equal("a", (string)rows[0]["Col1"]);
        }
    }
}
