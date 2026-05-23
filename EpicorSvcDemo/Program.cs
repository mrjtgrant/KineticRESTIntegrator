using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EpicorSvcs;
using EpicorSvcs.Dtos;
using FileHandling;
using FileHandling.Dtos;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcDemo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string error = "";

            // EpicorClient with no arguments loads its connection settings from
            // App.config (or environment variables). Copy App.config.template to
            // App.config and fill in your environment, company, and credentials.
            //
            // To connect programmatically instead — e.g. when credentials come
            // from a web interface or are chosen at runtime — pass a configured
            // EpicorRESTSessionKey:
            //
            //   new EpicorClient(new EpicorRESTSessionKey
            //   {
            //       Company     = "YOUR_COMPANY",
            //       Environment = "https://your-epicor-host/your-app",
            //       AuthObject  = new RESTAuthenticationObject
            //       {
            //           Username = "YOUR_USER",
            //           Userkey  = "YOUR_PASSWORD",
            //           ApiKey   = ""   // set ApiKey instead for v2 OData auth
            //       }
            //   });
            //
            // EpicorClient owns an HttpClient and must be disposed.
            using (var epicor = new EpicorClient())
            {
                var baqResult = await epicor.BAQ.BAQResultsAsync("YOUR_BAQ_ID").ConfigureAwait(false);

                if (baqResult.IsFailure)
                {
                    error = $"BAQ failed: {baqResult.ErrorMessage}";
                    Console.WriteLine(error);
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
                    From = "reports@example.com",
                    To = "recipient@example.com",
                    CC = "",
                    BCC = "",
                    SMTPHost = "smtp.example.com",
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
            {"OrderRel_OrderNum","Order"},
            {"OrderRel_OrderLine","Line"},
            {"OrderRel_OrderRelNum","Release"},
            {"OrderHed_OrderDate","Order Date"},
            {"OrderDtl_PartNum","Part"},
            {"Part_PartDescription","Description"},
            {"Part_TypeCode","TypeCode"},
            {"OrderDtl_IUM","IUM"},
            {"OrderDtl_SalesUM","Sales UM"},
            {"Calculated_DemandQty","Demand Qty"},
            {"Calculated_OnHandQty","On Hand Qty"},
            {"Calculated_OrderQty","OrderQty"},
            {"Customer_Country","Country (Country)"},
            {"Customer_CustID","Customer ID"},
            {"Customer_Name","Customer"},
            {"UserFile_Name","Entry Person"},
            {"Vendor_Country","Country (Supplier)"},
            {"Vendor_Name","Supplier"},
            {"Vendor_VendorID","Supplier ID"},
            {"Warehse_Description","Warehouse"},
            {"WhseBin_Description","Bin"},
            {"RowIdent","REMOVE_COLUMN"}
        };
    }
}