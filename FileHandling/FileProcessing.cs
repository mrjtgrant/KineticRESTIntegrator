using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FileHandling
{
    public static class FileProcessing
    {
        public static ExcelParser ExcelParser = new ExcelParser(); 

        public static List<string> EmailReport(EMailMeta mailMeta)
        {
            var steplist = new List<string> { "Emailer Start" };
            try
            {
                steplist.Add(1.ToString() + " EMAIL Setup");
                var EmailSpecs = new EmailSpecs
                {
                    EmailSubject = mailMeta.Subject,
                    EmailBody = mailMeta.Body
                };

                steplist.Add(2.ToString() + " Create " + mailMeta.AttachmentType);
                EmailSpecs.FileAddress = mailMeta.AttachmentType == "csv" ?
                    WriteDataToCSVFile(mailMeta.AttachmentData, mailMeta.AttachmentName) :
                    WriteDataToExcelFile(mailMeta.AttachmentData, mailMeta.AttachmentName, mailMeta.ExcelSheetName, mailMeta.AttachmentHeaderMap);

                if(!String.IsNullOrEmpty(mailMeta.Error))
                {
                    EmailSpecs.FileAddress = "EMPTY_DATASET"; 
                    EmailSpecs.EmailError = mailMeta.Error; 
                    EmailSpecs.EmailBody = mailMeta.Error;
                }

                if(EmailSpecs.FileAddress == "EMPTY_DATASET")
                {
                    steplist.Add("3 No Data to Report");
                }


                if (!string.IsNullOrEmpty(mailMeta.From))
                    EmailSpecs.EmailFrom = mailMeta.From;

                if (!String.IsNullOrEmpty(mailMeta.To))
                    EmailSpecs.EmailRecipients = new List<string> { mailMeta.To };

                if (!String.IsNullOrEmpty(mailMeta.CC))
                    EmailSpecs.EmailCCRecipients = new List<string> { mailMeta.CC };

                if (!String.IsNullOrEmpty(mailMeta.BCC))
                    EmailSpecs.EmailBCCRecipients = new List<string> { mailMeta.BCC };

                if (!String.IsNullOrEmpty(mailMeta.SMTPHost))
                    EmailSpecs.smtpspecs.host = mailMeta.SMTPHost;


                var emailresult = JObject.FromObject(Emailer.DotNetEmail(EmailSpecs)).ToString();

                steplist.Add(emailresult);

                
            }
            catch (Exception ex)
            {
                steplist.Add("ProcessFile ERROR: " + ex.Message);
            }

            return steplist; 
        }

        public static string WriteDataToCSVFile(JArray data, string filename, Dictionary<string, string> ColumnMapping = null)
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), filename);
            using (StreamWriter sw = new StreamWriter(tempFilePath))
            {
                sw.Write(ConvertJArrayToCSV(data, ColumnMapping));
            }
            return tempFilePath;    
        }

        public static string WriteDataToExcelFile(JArray data, string filename, string SheetName = null, Dictionary<string,string> HeaderMap = null)
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), filename);

            if (data == null)
                return "EMPTY_DATASET"; 

            DataTable dt = (DataTable)JsonConvert.DeserializeObject(data.ToString(Formatting.None), (typeof(DataTable)));

            return ExcelParser.CreateExcelFileFromDT(dt, tempFilePath, SheetName, HeaderMap); 
        }

        /**
         * Convert DataTable into HTML Table
         */
        public static string ConvertJArrayToHTMLTable(JArray data)
        {
            if (data == null || data.Count == 0) return string.Empty;

            List<string> Columns = GetPropertyNames(JObject.FromObject(data[0]));
            string html = "<table border='1' style = 'border-collapse:collapse; white-space:nowrap;'  cellpadding = '10'> ";
            //add header row
            html += "<tr>";
            for (int i = 0; i < Columns.Count; i++)
                html += "<th style='white-space: nowrap;'>" + Columns[i] + "</th>";
            html += "</tr>";
            //add rows
            for (int i = 0; i < data.Count; i++)
            {
                html += "<tr>";
                for (int j = 0; j < Columns.Count; j++)
                    html += "<td cellpadding='5'  style='white-space: nowrap;'>" + data[i][Columns[j]].ToString() + "</td>";
                html += "</tr>";
            }
            html += "</table>";
            return html;
        }

        public static string ConvertJArrayToCSV(JArray data, Dictionary<string, string> HeaderMap = null) 
        {
            List<string> Columns = GetPropertyNames(JObject.FromObject(data[0]), HeaderMap);
            StringBuilder csv = new StringBuilder();
            csv.AppendLine(String.Join(",", Columns)); 
            //add rows
            foreach (JObject line in data)
                csv.AppendLine(String.Join(",", GetPropertyValues(line))); 

            return csv.ToString();
        }

        public static List<string> GetPropertyNames(JObject line, Dictionary<string,string> HeaderMap = null)
        {
            List<string> headers = (from row in line.Properties() select row.Name).ToList();
            if (HeaderMap != null)
            {
                headers = headers.Select(x => (HeaderMap.ContainsKey(x)? HeaderMap[x] : x)).ToList();
            }
            return headers;
        }
        private static List<dynamic> GetPropertyValues(JObject line)
        {
            return (from row in line.Properties() select row.Value.ToString().Replace(",", "")).ToList<dynamic>();
        }


    }
}
