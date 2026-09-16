using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Keri.Epicor.Dtos
{
    /// <summary>
    /// Represents the Epicor <c>MscShpHd</c> table — miscellaneous shipment
    /// header (the pack-level record for non-customer-order shipments:
    /// returns to vendor, transfers, RMAs out, sample shipments, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by <see cref="MiscShipSvc"/> as the row type for
    /// <c>MiscShipsAsync</c> and as the header row inside the
    /// <c>GetByIDAsync</c> dataset. The companion line table is
    /// <see cref="MscShpDt"/>.
    /// </para>
    /// <para>
    /// Property names match Epicor's column names exactly so Newtonsoft.Json
    /// deserialization works without annotations. Models the practical-core
    /// columns every install has: identity, ship-to address, reference
    /// numbers (order/PO/job/RMA/DMR/BOL), tracking, shipping flags, and
    /// package data. Deliberately not modeled: manifest/MF integration
    /// (<c>MF*</c>), service-delivery (<c>Serv*</c>), COD/declared insurance
    /// (<c>COD*</c>, <c>Declared*</c>), UPS Quantum View (<c>UPS*</c>),
    /// freight forwarder (<c>FF*</c>), pay-to address (<c>Pay*</c>),
    /// Argentine localization (<c>AG*</c>), carton tracking, manifest/legal
    /// number processing, display-flattener columns, and audit columns
    /// (<c>SysRevID</c>, <c>SysRowID</c>, <c>BitFlag</c>) — all remain
    /// accessible via <see cref="ExtraData"/>.
    /// </para>
    /// </remarks>
    public class MscShpHd
    {
        // ----- Identity -----

        /// <summary>Epicor company code.</summary>
        public string Company { get; set; }

        /// <summary>The pack number — primary key.</summary>
        public int PackNum { get; set; }

        /// <summary>Plant the shipment originates from.</summary>
        public string Plant { get; set; }

        // ----- Status / dates / audit (visible) -----

        /// <summary>Date of the shipment.</summary>
        public DateTime? ShipDate { get; set; }

        /// <summary>Status of the shipment (e.g. open, shipped).</summary>
        public string ShipStatus { get; set; }

        /// <summary>User who entered the shipment.</summary>
        public string EntryPerson { get; set; }

        /// <summary>User who last changed the shipment.</summary>
        public string ChangedBy { get; set; }

        /// <summary>Date the shipment was last changed.</summary>
        public DateTime? ChangeDate { get; set; }

        // ----- Reference numbers -----

        /// <summary>Sales-order number, if shipping against an order.</summary>
        public int OrderNum { get; set; }

        /// <summary>Purchase-order number, if returning to vendor.</summary>
        public int PONum { get; set; }

        /// <summary>Job number, if linked to a manufacturing job.</summary>
        public string JobNum { get; set; }

        /// <summary>Call (case) number, if linked to a service call.</summary>
        public int CallNum { get; set; }

        /// <summary>Call line, paired with CallNum.</summary>
        public int CallLine { get; set; }

        /// <summary>DMR number (discrepant material report), if applicable.</summary>
        public int DMRNum { get; set; }

        /// <summary>RMA number, if applicable.</summary>
        public int RMANum { get; set; }

        /// <summary>RMA line, paired with RMANum.</summary>
        public int RMALine { get; set; }

        /// <summary>Bill of lading number.</summary>
        public int BOLNum { get; set; }

        /// <summary>Bill of lading line.</summary>
        public int BOLLine { get; set; }

        /// <summary>Legal number assigned to the shipment.</summary>
        public string LegalNumber { get; set; }

        // ----- Linked entities -----

        /// <summary>Customer (CustNum), if shipping to a customer.</summary>
        public int CustNum { get; set; }

        /// <summary>Ship-to address identifier under the customer.</summary>
        public string ShipToNum { get; set; }

        /// <summary>The ship-to customer (CustNum), if different from CustNum.</summary>
        public int ShipToCustNum { get; set; }

        /// <summary>Vendor (VendorNum), if returning to vendor.</summary>
        public int VendorNum { get; set; }

        /// <summary>Purchase-point identifier under the vendor.</summary>
        public string PurPoint { get; set; }

        // ----- Ship-to / pack-to address -----

        /// <summary>Ship-to name.</summary>
        public string Name { get; set; }

        /// <summary>Ship-to address line 1.</summary>
        public string Address1 { get; set; }

        /// <summary>Ship-to address line 2.</summary>
        public string Address2 { get; set; }

        /// <summary>Ship-to address line 3.</summary>
        public string Address3 { get; set; }

        /// <summary>Ship-to city.</summary>
        public string City { get; set; }

        /// <summary>Ship-to state.</summary>
        public string State { get; set; }

        /// <summary>Ship-to ZIP / postal code.</summary>
        public string ZIP { get; set; }

        /// <summary>Ship-to country.</summary>
        public string Country { get; set; }

        /// <summary>Ship-to country number.</summary>
        public int CountryNum { get; set; }

        /// <summary>Ship-to phone number.</summary>
        public string PhoneNum { get; set; }

        /// <summary>Ship-to fax number.</summary>
        public string FaxNum { get; set; }

        // ----- Ship-via / tracking -----

        /// <summary>Ship-via code (carrier).</summary>
        public string ShipViaCode { get; set; }

        /// <summary>Ship-via code as freighted (may differ from ShipViaCode).</summary>
        public string FreightedShipViaCode { get; set; }

        /// <summary>Person who shipped the pack.</summary>
        public string ShipPerson { get; set; }

        /// <summary>Shipping log entry.</summary>
        public string ShipLog { get; set; }

        /// <summary>Tracking number from the carrier.</summary>
        public string TrackingNumber { get; set; }

        /// <summary>Waybill number.</summary>
        public string WayBillNbr { get; set; }

        // ----- Comments -----

        /// <summary>Label comment for shipping label.</summary>
        public string LabelComment { get; set; }

        /// <summary>Free-form shipment comment.</summary>
        public string ShipComment { get; set; }

        /// <summary>Internal reference notes.</summary>
        public string RefNotes { get; set; }

        // ----- Shipping flags -----

        /// <summary>True if shipment contains hazardous materials.</summary>
        public bool Hazmat { get; set; }

        /// <summary>True if shipment is documents only (no parts).</summary>
        public bool DocOnly { get; set; }

        /// <summary>True if delivery is to a residential address.</summary>
        public bool ResDelivery { get; set; }

        /// <summary>True if Saturday delivery is requested.</summary>
        public bool SatDelivery { get; set; }

        /// <summary>True if Saturday pickup is requested.</summary>
        public bool SatPickup { get; set; }

        /// <summary>True if shipment is international.</summary>
        public bool IntrntlShip { get; set; }

        /// <summary>True if each pack has individual IDs.</summary>
        public bool IndividualPackIDs { get; set; }

        /// <summary>True to notify on shipment events.</summary>
        public bool NotifyFlag { get; set; }

        /// <summary>Email address for notifications.</summary>
        public string NotifyEMail { get; set; }

        // ----- Weight / package -----

        /// <summary>Total weight of the shipment.</summary>
        public decimal Weight { get; set; }

        /// <summary>Unit of measure for Weight.</summary>
        public string WeightUOM { get; set; }

        /// <summary>Package code.</summary>
        public string PkgCode { get; set; }

        /// <summary>Package class.</summary>
        public string PkgClass { get; set; }

        /// <summary>Package length.</summary>
        public decimal PkgLength { get; set; }

        /// <summary>Package width.</summary>
        public decimal PkgWidth { get; set; }

        /// <summary>Package height.</summary>
        public decimal PkgHeight { get; set; }

        /// <summary>Unit of measure for package dimensions.</summary>
        public string PkgSizeUOM { get; set; }

        // ----- Row state / extension data -----

        /// <summary>
        /// Row state for Epicor's dataset protocol: <c>"A"</c> = added,
        /// <c>"U"</c> = updated, <c>""</c> = unchanged.
        /// </summary>
        public string RowMod { get; set; }

        /// <summary>
        /// Unmodeled columns on this row, including installation-specific
        /// custom columns (Epicor's <c>_c</c> suffix convention). Populated
        /// on deserialization with any JSON property the typed DTO does not
        /// have a field for; serialized back out as siblings of the typed
        /// properties. Read or write a custom column by key —
        /// e.g. <c>dto.ExtraData["MyField_c"] = "value"</c>.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
            = new Dictionary<string, JToken>();
    }
}
