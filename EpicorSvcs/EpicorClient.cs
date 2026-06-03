using System;
using RESTServices;
using EpicorSvcs.Dtos;

namespace EpicorSvcs
{
    /// <summary>
    /// A convenience facade for working with multiple Epicor services that
    /// share the same session. Holds one configured <see cref="EpicorRESTSessionKey"/>
    /// and lazy-constructs each service on first access — pay only for what
    /// you use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Designed for the common pattern of "connect once, use many times":
    /// open a client at app startup or when the user signs in, hold it in
    /// a field, call services through it throughout the app's lifetime,
    /// and dispose it when done.
    /// </para>
    /// <para>
    /// All services constructed by the client are disposed automatically
    /// when the client is disposed. You do not need to dispose individual
    /// services yourself when accessing them through this facade.
    /// </para>
    /// <para>
    /// Direct service construction (<c>new BAQSvc()</c>, etc.) still works
    /// as before for callers who prefer one-off, short-lived usage. The
    /// facade is additive, not a replacement.
    /// </para>
    /// <example>
    /// <code>
    /// // Programmatic session (e.g. from a vault or saved connection)
    /// var session = new EpicorRESTSessionKey { /* ... */ };
    ///
    /// using (var epicor = new EpicorClient(session))
    /// {
    ///     var layers = await epicor.GenxData.GenXDatasAsync();
    ///     var menus  = await epicor.Menu.GetRowsAsync();
    ///     var parts  = await epicor.Part.PartsAsync();
    /// }   // all services disposed here
    /// </code>
    /// </example>
    /// </remarks>
    public sealed class EpicorClient : IDisposable
    {
        private readonly EpicorRESTSessionKey _session;
        private bool _disposed;

        // Backing fields for lazy-constructed services.
        // A service is constructed on first property access and reused thereafter.
        private BAQSvc _baq;
        private MenuSvc _menu;
        private UserCodesSvc _userCodes;
        private GenxDataSvc _genxData;
        private UDTableSvc _udTable;
        private ProjectSvc _project;
        private CustomerSvc _customer;
        private VendorSvc _vendor;
        private PartSvc _part;
        private SalesRepSvc _salesRep;
        private PayMethodSvc _payMethod;
        private PaymentEntrySvc _paymentEntry;
        private SerialNoSvc _serialNo;
        private MiscShipSvc _miscShip;
        private SelectedSerialNumbersSvc _selectedSerialNumbers;
        private InvTransferSvc _invTransfer;
        private BomSearchSvc _bomSearch;
        private EngWorkBenchSvc _engWorkBench;
        private JobEntrySvc _jobEntry;
        private POSvc _po;
        private ReceiptSvc _receipt;
        private QuoteSvc _quote;
        private SalesOrderSvc _salesOrder;

        /// <summary>
        /// Construct an EpicorClient using a programmatic session — bypasses
        /// any <c>App.config</c> / environment variable lookup.
        /// </summary>
        /// <param name="session">
        /// A fully-configured <see cref="EpicorRESTSessionKey"/> with company,
        /// environment URL, and authentication.
        /// </param>
        public EpicorClient(EpicorRESTSessionKey session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>
        /// Construct an EpicorClient from <c>App.config</c> / environment
        /// variables, with optional environment override. Validates that
        /// configuration is present and complete; throws
        /// <see cref="InvalidOperationException"/> if not.
        /// </summary>
        /// <param name="env">
        /// Optional environment selector that overrides
        /// <c>DefaultEnvironment</c> from config. Typical values:
        /// <c>"prod"</c>, <c>"pilot"</c>, <c>"test"</c>, or a literal URL.
        /// </param>
        public EpicorClient(string env = null)
        {
            // Construct a throwaway base service to trigger validation and
            // produce a session key. We use the existing BAQSvc constructor
            // path since it's the lightest service and the validation lives
            // in EpicorSvc's base.
            using (var probe = new BAQSvc(env))
            {
                _session = (EpicorRESTSessionKey)probe.sesh;
            }
        }

        /// <summary>
        /// The session this client is using. Read-only; create a new client
        /// to switch sessions.
        /// </summary>
        public EpicorRESTSessionKey Session => _session;

        /// <summary>True after <see cref="Dispose"/> has been called.</summary>
        public bool IsDisposed => _disposed;

        // ---------------------------------------------------------------------
        // Platform services
        // ---------------------------------------------------------------------

        /// <summary>Run Business Activity Queries.</summary>
        public BAQSvc BAQ
        {
            get { ThrowIfDisposed(); return _baq ?? (_baq = new BAQSvc(_session)); }
        }

        /// <summary>Read Epicor menu structure.</summary>
        public MenuSvc Menu
        {
            get { ThrowIfDisposed(); return _menu ?? (_menu = new MenuSvc(_session)); }
        }

        /// <summary>Read user-defined code tables.</summary>
        public UserCodesSvc UserCodes
        {
            get { ThrowIfDisposed(); return _userCodes ?? (_userCodes = new UserCodesSvc(_session)); }
        }

        /// <summary>
        /// Read and write Epicor's generic-data table. Commonly used for
        /// Kinetic customization layers.
        /// </summary>
        public GenxDataSvc GenxData
        {
            get { ThrowIfDisposed(); return _genxData ?? (_genxData = new GenxDataSvc(_session)); }
        }

        /// <summary>Generic UD-table service — read and write rows across any UD table (UD01–UD30).</summary>
        public UDTableSvc UDTable
        {
            get { ThrowIfDisposed(); return _udTable ?? (_udTable = new UDTableSvc(_session)); }
        }

        /// <summary>Project header lookup and creation.</summary>
        public ProjectSvc Project
        {
            get { ThrowIfDisposed(); return _project ?? (_project = new ProjectSvc(_session)); }
        }

        // ---------------------------------------------------------------------
        // Master data services
        // ---------------------------------------------------------------------

        /// <summary>Customer record lookup.</summary>
        public CustomerSvc Customer
        {
            get { ThrowIfDisposed(); return _customer ?? (_customer = new CustomerSvc(_session)); }
        }

        /// <summary>Vendor (supplier) record lookup.</summary>
        public VendorSvc Vendor
        {
            get { ThrowIfDisposed(); return _vendor ?? (_vendor = new VendorSvc(_session)); }
        }

        /// <summary>Part master operations and attachments.</summary>
        public PartSvc Part
        {
            get { ThrowIfDisposed(); return _part ?? (_part = new PartSvc(_session)); }
        }

        /// <summary>Sales rep lookup.</summary>
        public SalesRepSvc SalesRep
        {
            get { ThrowIfDisposed(); return _salesRep ?? (_salesRep = new SalesRepSvc(_session)); }
        }

        // ---------------------------------------------------------------------
        // AR services
        // ---------------------------------------------------------------------

        /// <summary>Payment-method lookup.</summary>
        public PayMethodSvc PayMethod
        {
            get { ThrowIfDisposed(); return _payMethod ?? (_payMethod = new PayMethodSvc(_session)); }
        }

        /// <summary>AR payment entry lookup.</summary>
        public PaymentEntrySvc PaymentEntry
        {
            get { ThrowIfDisposed(); return _paymentEntry ?? (_paymentEntry = new PaymentEntrySvc(_session)); }
        }

        // ---------------------------------------------------------------------
        // Inventory services
        // ---------------------------------------------------------------------

        /// <summary>Serial-number lookup.</summary>
        public SerialNoSvc SerialNo
        {
            get { ThrowIfDisposed(); return _serialNo ?? (_serialNo = new SerialNoSvc(_session)); }
        }

        /// <summary>Misc shipment header / detail.</summary>
        public MiscShipSvc MiscShip
        {
            get { ThrowIfDisposed(); return _miscShip ?? (_miscShip = new MiscShipSvc(_session)); }
        }

        /// <summary>Selected serial number assignment (used by inventory transfers).</summary>
        public SelectedSerialNumbersSvc SelectedSerialNumbers
        {
            get { ThrowIfDisposed(); return _selectedSerialNumbers ?? (_selectedSerialNumbers = new SelectedSerialNumbersSvc(_session)); }
        }

        /// <summary>Inventory transfer between bins, with serial-tracking support.</summary>
        public InvTransferSvc InvTransfer
        {
            get { ThrowIfDisposed(); return _invTransfer ?? (_invTransfer = new InvTransferSvc(_session)); }
        }

        // ---------------------------------------------------------------------
        // Engineering services
        // ---------------------------------------------------------------------

        /// <summary>BOM tree retrieval and lookup.</summary>
        public BomSearchSvc BomSearch
        {
            get { ThrowIfDisposed(); return _bomSearch ?? (_bomSearch = new BomSearchSvc(_session)); }
        }

        /// <summary>ECO group management — check-out, add materials, approve.</summary>
        public EngWorkBenchSvc EngWorkBench
        {
            get { ThrowIfDisposed(); return _engWorkBench ?? (_engWorkBench = new EngWorkBenchSvc(_session)); }
        }

        // ---------------------------------------------------------------------
        // Production services
        // ---------------------------------------------------------------------

        /// <summary>Manufacturing job header reads and creation.</summary>
        public JobEntrySvc JobEntry
        {
            get { ThrowIfDisposed(); return _jobEntry ?? (_jobEntry = new JobEntrySvc(_session)); }
        }

        // ---------------------------------------------------------------------
        // Purchasing services
        // ---------------------------------------------------------------------

        /// <summary>Purchase order header / line / release reads and creation.</summary>
        public POSvc PO
        {
            get { ThrowIfDisposed(); return _po ?? (_po = new POSvc(_session)); }
        }

        /// <summary>Purchase-order receipt header / line / attachment reads and creation.</summary>
        public ReceiptSvc Receipt
        {
            get { ThrowIfDisposed(); return _receipt ?? (_receipt = new ReceiptSvc(_session)); }
        }

        // ---------------------------------------------------------------------
        // Sales services
        // ---------------------------------------------------------------------

        /// <summary>Quote header creation and lookup.</summary>
        public QuoteSvc Quote
        {
            get { ThrowIfDisposed(); return _quote ?? (_quote = new QuoteSvc(_session)); }
        }

        /// <summary>Sales order header / line creation and lookup.</summary>
        public SalesOrderSvc SalesOrder
        {
            get { ThrowIfDisposed(); return _salesOrder ?? (_salesOrder = new SalesOrderSvc(_session)); }
        }

        // ---------------------------------------------------------------------
        // Disposal
        // ---------------------------------------------------------------------

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EpicorClient));
        }

        /// <summary>
        /// Disposes every service that was constructed by this client. Services
        /// that were never accessed are not constructed and not disposed.
        /// </summary>
        /// <remarks>
        /// After disposal, accessing any service property throws
        /// <see cref="ObjectDisposedException"/>.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;

            _baq?.Dispose();
            _menu?.Dispose();
            _userCodes?.Dispose();
            _genxData?.Dispose();
            _udTable?.Dispose();
            _project?.Dispose();
            _customer?.Dispose();
            _vendor?.Dispose();
            _part?.Dispose();
            _salesRep?.Dispose();
            _payMethod?.Dispose();
            _paymentEntry?.Dispose();
            _serialNo?.Dispose();
            _miscShip?.Dispose();
            _selectedSerialNumbers?.Dispose();
            _invTransfer?.Dispose();
            _bomSearch?.Dispose();
            _engWorkBench?.Dispose();
            _jobEntry?.Dispose();
            _po?.Dispose();
            _receipt?.Dispose();
            _quote?.Dispose();
            _salesOrder?.Dispose();

            _disposed = true;
        }
    }
}