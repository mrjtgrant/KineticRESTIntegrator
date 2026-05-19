using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RESTServices;

namespace EpicorSvcs.Dtos
{
    public class EpicorRESTSessionKey:RESTSessionKey
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
