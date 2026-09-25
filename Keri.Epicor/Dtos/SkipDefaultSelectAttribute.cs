using System;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Marks a DTO whose entity-set reads send no <c>$select</c> unless the
    /// caller asks for one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>$select</c> exists to reduce a response to the columns a DTO can
    /// bind. That pays when the DTO is a narrow core of a wide table. It stops
    /// paying when the DTO already names nearly every column: the projection
    /// then asks for what an unprojected read returns anyway, and costs more
    /// rather than less, because naming every column explicitly makes Epicor
    /// emit fields it otherwise omits. Measured on a live install at 25 rows,
    /// a DTO modelling all 81 columns of its table produced a response 15.6%
    /// larger with the projection than without it.
    /// </para>
    /// <para>
    /// <b>This changes nothing a caller asked for.</b> Passing an explicit
    /// <c>select</c>, or naming <c>additionalColumns</c>, projects exactly as it
    /// always did. The attribute only decides what happens when the caller
    /// expresses no preference.
    /// </para>
    /// <para>
    /// <b>It cannot cost a caller data.</b> An unprojected read returns at least
    /// the columns the DTO models and often more — installation-specific
    /// <c>_c</c> columns among them — and anything the typed properties do not
    /// consume arrives through the DTO's <c>[JsonExtensionData]</c> dictionary.
    /// The attribute trades bytes, never fields.
    /// </para>
    /// <para>
    /// <b>When to use it.</b> On a DTO that models its table completely enough
    /// that the projection cannot make the response smaller — in practice, a
    /// narrow reference table modelled in full. <c>KeriPocs/SchemaProbePoc.cs</c>
    /// reports how many columns the server has against how many the DTO models,
    /// and <c>KeriPocs/ODataProbePoc.cs</c> measures the difference the
    /// projection actually makes. Measure before adding it: column count alone
    /// does not qualify a DTO, and most do not qualify at all.
    /// </para>
    /// <para>
    /// <b>What it does not claim.</b> It says what Keri should do, not what
    /// Epicor's table contains. A version upgrade that widens the table, or an
    /// installation with custom columns, leaves the attribute correct — the read
    /// simply returns more than the DTO models, and the surplus lands in the
    /// overflow dictionary. Whether the attribute is still the best choice for
    /// that DTO is a question for the next measurement, not a correctness
    /// problem.
    /// </para>
    /// <para>
    /// No DTO in this library carries the attribute. It exists for consumers
    /// who define their own DTOs and services on top of
    /// <see cref="Keri.Epicor.EpicorSvc"/>; see <c>ADDING_A_SERVICE.md</c>.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SkipDefaultSelectAttribute : Attribute
    {
    }
}
