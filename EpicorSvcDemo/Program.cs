using System.Collections.Generic;
using System.Security.Policy;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.ExtendedProperties;
using EpicorSvcs;
using FileHandling;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcDemo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // BAQSvc owns an HttpClient and must be disposed.
            using (var baq = new BAQSvc(/*new RESTSessionKey
                {
                //these details can be set and referenced here, through Environment variables, Windows Credentials manager, or App.config.  
                //whichver option suits the need of the application being built.  
                //this option would allow the user to pass credentials on the fly, such as through a web interface for example. 
                //If you have an executable script that runs in a local, closed network, it might make more sense to set it up with App.Config 

                Company = "EPIC06",
                Environment = "https://example-live.epicorsaas.com/server",
                AuthObject = new RESTServices.RESTAuthenticationObject
                {
                    ApiKey = "", //leave blank for Basic(v1) Authentication
                    Userkey = "manager",
                    Username = "manger"
                }
            }*/))
            {
                JObject BAQResult = await baq.BAQResultsAsync("EXAMPLE_BAQ").ConfigureAwait(false);

                string error = BAQResult["ErrorMessage"]?.ToString();
                JArray reportData = BAQResult["value"]?.ToObject<JArray>() ?? new JArray();

                FileProcessing.EmailDataReport(new EMailMeta
                {
                    From = "noreply@company.com",
                    To = "user@company.com",
                    CC = "",
                    BCC = "",
                    //SMTPHost = "10.10.10.10", //Can be set here or in App.Config
                    RecipientName = "John Doe",
                    ExcelSheetName = "EXAMPLE_REPORT",
                    AttachmentName = "EXAMPLE_REPORT",
                    AttachmentType = "xlsx",
                    AttachmentDateFormat = "none", // Examples: "none", "u", "s", "yyyy-MM-dd HH.mm.ss"
                    AttachmentHeaderMap = AttachmentColHeaderMap,
                    AttachmentData = reportData,
                    Error = error
                });
            }
        }


        /**
         *  Column Mapping Key/Value pair
         *  Translate Key (Assumed to be the Column ID coming from the BAQ), and setting the ColumnName in the DataTable for the Excel Worksheet
         *  "BAQ_COLUMN", "Formatted Display for Excel Column"
         */
        private static Dictionary<string, string> AttachmentColHeaderMap = new Dictionary<string, string> {
            {"OrderRel_Company","Company"},
            {"Calculated_OrderDateFormat","Order Date Formatted"},
            {"OrderRel_OrderNum","Order"},
            {"OrderRel_OrderLine","Line"},
            {"OrderRel_OrderRelNum","Release"},
            {"OrderDtl_PartNum","Part"},
            {"Part_PartDescription","Description"},
            {"Part_TypeCode","TypeCode"},
            {"Calculated_SellingQuantity","SellingQuantity"},
            {"OrderDtl_SalesUM","Sales UM"},
            {"Calculated_OrderQty","OrderQty"},
            {"OrderDtl_IUM","IUM"},
            {"Calculated_SysDateFormat","System Date"},
            {"Calculated_TranDateFormat","Transaction Date"},
            {"Calculated_TranTimeAMPM","Transaction Time"},
            {"Calculated_OnHandQty","On Hand Qty"},
            {"Calculated_DemandQty","Demand Qty"},
            {"Calculated_AvailableQty","Available Qty"},
            {"Calculated_SalesDemandQty","Sales Demand Qty"},
            {"Calculated_JobDemandQty","Job Demand Qty"},
            {"Warehse_Description","Warehouse"},
            {"WhseBin_Description","Bin"},
            {"PartTran_TranType","Type"},
            {"Calculated_TranQty","TranQty"},
            {"PartTran_UM","UOM"},
            {"PartTran_JobNum","Job"},
            {"PartTran_JobSeq","Sequence"},
            {"JobHead_PartNum","Part (Job)"},
            {"PartTran_OrderNum","Order (Transaction)"},
            {"PartTran_OrderLine","Line (Transaction)"},
            {"PartTran_OrderRelNum","Release (Transaction)"},
            {"OrderHed_OrderDate","Order Date"},
            {"Customer_CustID","Customer ID"},
            {"Customer_Name","Customer"},
            {"Customer_Country","Country (Country)"},
            {"PartTran_PONum","PO"},
            {"PartTran_POLine","Line (PO)"},
            {"PartTran_PORelNum","Release (PO)"},
            {"Calculated_POOrderDateFormat","Order Date (PO)"},
            {"UserFile_Name","Entry Person"},
            {"PartTran_PackSlip","Packing Slip"},
            {"Vendor_VendorID","Supplier ID"},
            {"Vendor_Name","Supplier"},
            {"Vendor_Country","Country (Supplier)"},
            {"Calculated_Bld_01_Desc","Primary Building"},
            {"Calculated_Bay_01_Desc","Primary Bay"},
            {"Calculated_Shelf_01","Primary Shelf"},
            {"Calculated_Bld_02_Desc","Secondary Building"},
            {"Calculated_Bay_02_Desc","Secondary Bay"},
            {"Calculated_Shelf_02","Secondary Shelf"},
            {"Calculated_Bld_03_Desc","Tertiary Building"},
            {"Calculated_Bay_03_Desc","Tertiary Bay"},
            {"Calculated_Shelf_03","Tertiary Shelf"},
            {"RowIdent","REMOVE_COLUMN"}
        };
    }
}