using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Keri.RestTransport;
using Keri.Epicor.Dtos;
using System.Net.Http;

namespace Keri.Epicor
{
    /// <summary>
    /// A convenience facade for working with multiple Epicor services that
    /// share the same session. Holds one configured <see cref="EpicorRestSessionKey"/>
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
    /// Direct service construction still works for callers who prefer
    /// one-off, short-lived usage, but services are now session-only —
    /// e.g. <c>new BAQSvc(session)</c>, where the session is built by the
    /// composition root (KeriConfigurator) or constructed directly.
    /// The facade is additive, not a replacement.
    /// </para>
    /// <example>
    /// <code>
    /// // Programmatic session (e.g. from a vault or saved connection)
    /// var session = new EpicorRestSessionKey { /* ... */ };
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
        private readonly EpicorRestSessionKey _session;
        private bool _disposed;

        // Backing fields for lazy-constructed services.
        // A service is constructed on first property access and reused thereafter.
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;

        private BAQSvc _baq;
        private MenuSvc _menu;
        private UserCodesSvc _userCodes;
        private GenxDataSvc _genxData;
        private UDTableSvc _udTable;
        private FunctionSvc _function;
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
        /// A fully-configured <see cref="EpicorRestSessionKey"/> with company,
        /// environment URL, and authentication.
        /// </param>
        public EpicorClient(EpicorRestSessionKey session) : this(session, null) { }

        /// <summary>
        /// Constructs the facade over an <see cref="HttpClient"/> you supply — one
        /// from <c>IHttpClientFactory</c>, or one carrying your own handlers for
        /// retry, logging or a proxy.
        /// </summary>
        /// <remarks>
        /// A client you pass in is never disposed by this class and never has
        /// headers or a timeout set on it: credentials go on each request, so the
        /// client stays free of this session's state and can be shared with the
        /// rest of your application. Passing null behaves like the
        /// single-argument constructor, which creates one client for this facade
        /// and disposes it with the facade.
        /// </remarks>
        /// <param name="session">The Epicor session every service will use.</param>
        /// <param name="httpClient">The client to send on, or null to create one.</param>
        public EpicorClient(EpicorRestSessionKey session, HttpClient httpClient)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));

            // One client for the whole facade, and therefore one connection pool,
            // shared by every service constructed below. Each service used to
            // create its own, so a client touching five services held five.
            if (httpClient == null)
            {
                _httpClient = new HttpClient { Timeout = session.Timeout };
                _ownsHttpClient = true;
            }
            else
            {
                _httpClient = httpClient;
                _ownsHttpClient = false;
            }
        }

        /// <summary>
        /// The session this client is using. Read-only; create a new client
        /// to switch sessions.
        /// </summary>
        public EpicorRestSessionKey Session => _session;

        /// <summary>True after <see cref="Dispose"/> has been called.</summary>
        public bool IsDisposed => _disposed;

        // ---------------------------------------------------------------------
        // Diagnostics
        // ---------------------------------------------------------------------

        /// <summary>
        /// Verifies that this client's session can reach Epicor and authenticate,
        /// by issuing a minimal read (top&#160;1 <c>PartNum</c> from the Part
        /// service). A lightweight connectivity probe — it does not validate any
        /// particular data, only that a real request round-trips successfully.
        /// </summary>
        /// <remarks>
        /// On success the result wraps <c>true</c>. On failure the underlying
        /// <see cref="OperationResult{T}"/> carries the diagnostic detail —
        /// inspect <see cref="OperationResult{T}.StatusCode"/> to distinguish
        /// causes: <c>401</c> (bad credentials), <c>404</c> (bad base URL or
        /// company), or a null status with a socket-level
        /// <see cref="OperationResult{T}.ErrorMessage"/> (host unreachable).
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// A successful <see cref="OperationResult{T}"/> wrapping <c>true</c>
        /// when the round-trip succeeds; otherwise a failure carrying the probe's
        /// status code, resource path, error message, and raw response.
        /// </returns>
        public async Task<OperationResult<bool>> TestConnectionAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            var probe = await Part
                .PartsAsync(select: new List<string> { "PartNum" }, top: 1, ct: ct)
                .ConfigureAwait(false);

            return probe.IsSuccess
                ? OperationResult<bool>.Success(true, probe.RawResponse)
                : probe.Retype<bool>();
        }

        // ---------------------------------------------------------------------
        // Platform services
        // ---------------------------------------------------------------------

        /// <summary>Run Business Activity Queries.</summary>
        public BAQSvc BAQ
        {
            get { ThrowIfDisposed(); return _baq ?? (_baq = new BAQSvc(_session, _httpClient)); }
        }

        /// <summary>Read Epicor menu structure.</summary>
        public MenuSvc Menu
        {
            get { ThrowIfDisposed(); return _menu ?? (_menu = new MenuSvc(_session, _httpClient)); }
        }

        /// <summary>Read user-defined code tables.</summary>
        public UserCodesSvc UserCodes
        {
            get { ThrowIfDisposed(); return _userCodes ?? (_userCodes = new UserCodesSvc(_session, _httpClient)); }
        }

        /// <summary>
        /// Read and write Epicor's generic-data table. Commonly used for
        /// Kinetic customization layers.
        /// </summary>
        public GenxDataSvc GenxData
        {
            get { ThrowIfDisposed(); return _genxData ?? (_genxData = new GenxDataSvc(_session, _httpClient)); }
        }

        /// <summary>Generic UD-table service — read and write rows across any UD table (UD01–UD30).</summary>
        public UDTableSvc UDTable
        {
            get { ThrowIfDisposed(); return _udTable ?? (_udTable = new UDTableSvc(_session, _httpClient)); }
        }

        /// <summary>Call Epicor Functions.</summary>
        public FunctionSvc Function
        {
            get { ThrowIfDisposed(); return _function ?? (_function = new FunctionSvc(_session, _httpClient)); }
        }

        /// <summary>Project header lookup and creation.</summary>
        public ProjectSvc Project
        {
            get { ThrowIfDisposed(); return _project ?? (_project = new ProjectSvc(_session, _httpClient)); }
        }

        // ---------------------------------------------------------------------
        // Master data services
        // ---------------------------------------------------------------------

        /// <summary>Customer record lookup.</summary>
        public CustomerSvc Customer
        {
            get { ThrowIfDisposed(); return _customer ?? (_customer = new CustomerSvc(_session, _httpClient)); }
        }

        /// <summary>Vendor (supplier) record lookup.</summary>
        public VendorSvc Vendor
        {
            get { ThrowIfDisposed(); return _vendor ?? (_vendor = new VendorSvc(_session, _httpClient)); }
        }

        /// <summary>Part master operations and attachments.</summary>
        public PartSvc Part
        {
            get { ThrowIfDisposed(); return _part ?? (_part = new PartSvc(_session, _httpClient)); }
        }

        /// <summary>Sales rep lookup.</summary>
        public SalesRepSvc SalesRep
        {
            get { ThrowIfDisposed(); return _salesRep ?? (_salesRep = new SalesRepSvc(_session, _httpClient)); }
        }

        // ---------------------------------------------------------------------
        // AR services
        // ---------------------------------------------------------------------

        /// <summary>Payment-method lookup.</summary>
        public PayMethodSvc PayMethod
        {
            get { ThrowIfDisposed(); return _payMethod ?? (_payMethod = new PayMethodSvc(_session, _httpClient)); }
        }

        /// <summary>AR payment entry lookup.</summary>
        public PaymentEntrySvc PaymentEntry
        {
            get { ThrowIfDisposed(); return _paymentEntry ?? (_paymentEntry = new PaymentEntrySvc(_session, _httpClient)); }
        }

        // ---------------------------------------------------------------------
        // Inventory services
        // ---------------------------------------------------------------------

        /// <summary>Serial-number lookup.</summary>
        public SerialNoSvc SerialNo
        {
            get { ThrowIfDisposed(); return _serialNo ?? (_serialNo = new SerialNoSvc(_session, _httpClient)); }
        }

        /// <summary>Misc shipment header / detail.</summary>
        public MiscShipSvc MiscShip
        {
            get { ThrowIfDisposed(); return _miscShip ?? (_miscShip = new MiscShipSvc(_session, _httpClient)); }
        }

        /// <summary>Selected serial number assignment (used by inventory transfers).</summary>
        public SelectedSerialNumbersSvc SelectedSerialNumbers
        {
            get { ThrowIfDisposed(); return _selectedSerialNumbers ?? (_selectedSerialNumbers = new SelectedSerialNumbersSvc(_session, _httpClient)); }
        }

        /// <summary>Inventory transfer between bins, with serial-tracking support.</summary>
        public InvTransferSvc InvTransfer
        {
            get { ThrowIfDisposed(); return _invTransfer ?? (_invTransfer = new InvTransferSvc(_session, _httpClient)); }
        }

        // ---------------------------------------------------------------------
        // Engineering services
        // ---------------------------------------------------------------------

        /// <summary>BOM tree retrieval and lookup.</summary>
        public BomSearchSvc BomSearch
        {
            get { ThrowIfDisposed(); return _bomSearch ?? (_bomSearch = new BomSearchSvc(_session, _httpClient)); }
        }

        /// <summary>ECO group management — check-out, add materials, approve.</summary>
        public EngWorkBenchSvc EngWorkBench
        {
            get { ThrowIfDisposed(); return _engWorkBench ?? (_engWorkBench = new EngWorkBenchSvc(_session, _httpClient)); }
        }

        // ---------------------------------------------------------------------
        // Production services
        // ---------------------------------------------------------------------

        /// <summary>Manufacturing job header reads and creation.</summary>
        public JobEntrySvc JobEntry
        {
            get { ThrowIfDisposed(); return _jobEntry ?? (_jobEntry = new JobEntrySvc(_session, _httpClient)); }
        }

        // ---------------------------------------------------------------------
        // Purchasing services
        // ---------------------------------------------------------------------

        /// <summary>Purchase order header / line / release reads and creation.</summary>
        public POSvc PO
        {
            get { ThrowIfDisposed(); return _po ?? (_po = new POSvc(_session, _httpClient)); }
        }

        /// <summary>Purchase-order receipt header / line / attachment reads and creation.</summary>
        public ReceiptSvc Receipt
        {
            get { ThrowIfDisposed(); return _receipt ?? (_receipt = new ReceiptSvc(_session, _httpClient)); }
        }

        // ---------------------------------------------------------------------
        // Sales services
        // ---------------------------------------------------------------------

        /// <summary>Quote header creation and lookup.</summary>
        public QuoteSvc Quote
        {
            get { ThrowIfDisposed(); return _quote ?? (_quote = new QuoteSvc(_session, _httpClient)); }
        }

        /// <summary>Sales order header / line creation and lookup.</summary>
        public SalesOrderSvc SalesOrder
        {
            get { ThrowIfDisposed(); return _salesOrder ?? (_salesOrder = new SalesOrderSvc(_session, _httpClient)); }
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
            _function?.Dispose();
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

            // The services share this facade's client and never dispose it, so it
            // is closed here — and only when this facade created it.
            if (_ownsHttpClient) _httpClient?.Dispose();

            _disposed = true;
        }
    }
}