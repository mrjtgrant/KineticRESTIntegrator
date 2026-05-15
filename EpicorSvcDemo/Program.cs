using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing.Charts;
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
            string error = ""; 
            // BAQSvc owns an HttpClient and must be disposed.
            using (var epicor = new EpicorClient(/* OPTIONAL on the fly.. Example for allowing the user to pass credential through a web interface
            new RESTSessionKey
            {
                Company = "YOUR_COMPANY",
                Environment = "https://your_company.epicorsaas.com/server",
                AuthObject = new RESTServices.RESTAuthenticationObject
                {
                    ApiKey = "", //leave blank if using Basic Auth (v1)
                    Userkey = "",
                    Username = "username"
                }
            }//IF NOT CONFIGURE HERE > Configure in APP.Config or ENVIRONMENT VARIABLES.. APP.Config useful for scheduled windows task  */
            ))
            {
                var baqResult = await epicor.BAQ.BAQResultsAsync("BAQ_ID_BAQ").ConfigureAwait(false);

                if (baqResult.IsFailure)
                {
                    error = $"BAQ failed: {baqResult.ErrorMessage}";
                    return; 
                }

                JArray reportData = JArray.FromObject(baqResult.Value);  // the actual result rows


                // Construct the email body inline. The library no longer
                // overrides caller-set Subject/Body — what you set here is
                // what gets sent.
                var recipientName = "John Doe";
                var attachmentName = "EXAMPLE_REPORT";

                var body = new System.Text.StringBuilder();
                body.AppendFormat("<p>{0}, </p>", recipientName);
                body.AppendFormat("<p>The following file has been attached: {0}</p><br/><br/><br/>", attachmentName);
                // body.AppendFormat("<p>Sincerely,<br/>Support Team</p>");

                var mailMeta = new EMailMeta
                {
                    From = "noreply@your_company.com",
                    To = "jgrant@your_company.com",
                    CC = "",
                    BCC = "",
                    SMTPHost = "10.10.10.10",
                    RecipientName = recipientName,
                    ExcelSheetName = "EXAMPLE_REPORT",
                    AttachmentName = attachmentName,
                    AttachmentType = "xlsx",
                    AttachmentDateFormat = "yyyy-MM-dd", // Examples: "none", "u", "s", "yyyy-MM-dd HH.mm.ss"
                    AttachmentHeaderMap = AttachmentColHeaderMap,
                    AttachmentData = reportData,
                    Subject = String.Format("Test Email to showcase Attachment {0}", attachmentName),
                    Body = body.ToString()
                    //,Error = error
                };

                // EmailReport returns a step-by-step log; print it so the
                // demo run shows what happened end-to-end.
                List<string> emailResult = FileProcessing.EmailReport(mailMeta);
                Console.WriteLine(String.Join(",\n", emailResult));
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