using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Keri.RestTransport;

namespace Keri.Epicor
{
    /// <summary>
    /// Session settings for Epicor: the transport's <see cref="RestSessionKey"/>
    /// plus the Epicor company. Pass one to <see cref="EpicorClient"/> or to any
    /// service constructor.
    /// </summary>
    /// <remarks>
    /// Lives in <c>Keri.Epicor</c> rather than <c>Keri.Epicor.Dtos</c>: this is
    /// the session a caller constructs first, not a row shape. Filing it with
    /// the table DTOs meant a developer with only <c>using Keri.Epicor;</c> in
    /// scope saw nothing when they typed <c>new Epicor…</c>.
    /// </remarks>
    public class EpicorRestSessionKey : RestSessionKey
    {
        /// <summary>
        /// Epicor company ID. This is an Epicor-specific concept: it is used
        /// only by the Epicor service layer (<c>EpicorSvc</c>), which substitutes
        /// it into the v2 OData URL. It has no meaning for a non-Epicor REST API —
        /// leave it unset when using the transport directly against another service.
        /// </summary>
        public string Company { get; set; }
    }
}
