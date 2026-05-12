using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RESTServices;

namespace EpicorSvcs
{
    public class UDXSvc : EpicorSvc
    {
        public UDXSvc(string env = null) : base(env) { }
        public UDXSvc(RESTSessionKey env) : base(env) { }

        // All UD columns supported by the generic upsert / select logic below.
        private static readonly List<string> udcols = new List<string>
        {
            "Character01", "Character02", "Character03", "Character04", "Character05",
            "Character06", "Character07", "Character08", "Character09", "Character10",
            "Number01", "Number02", "Number03", "Number04", "Number05",
            "Number06", "Number07", "Number08", "Number09", "Number10",
            "Number11", "Number12", "Number13", "Number14", "Number15",
            "Number16", "Number17", "Number18", "Number19", "Number20",
            "Date01", "Date02", "Date03", "Date04", "Date05",
            "Date06", "Date07", "Date08", "Date09", "Date10",
            "Date11", "Date12", "Date13", "Date14", "Date15",
            "Date16", "Date17", "Date18", "Date19", "Date20",
            "CheckBox01", "CheckBox02", "CheckBox03", "CheckBox04", "CheckBox05",
            "CheckBox06", "CheckBox07", "CheckBox08", "CheckBox09", "CheckBox10",
            "CheckBox11", "CheckBox12", "CheckBox13", "CheckBox14", "CheckBox15",
            "CheckBox16", "CheckBox17", "CheckBox18", "CheckBox19", "CheckBox20",
            "ShortChar01", "ShortChar02", "ShortChar03", "ShortChar04", "ShortChar05",
            "ShortChar06", "ShortChar07", "ShortChar08", "ShortChar09", "ShortChar10",
            "ShortChar11", "ShortChar12", "ShortChar13", "ShortChar14", "ShortChar15",
            "ShortChar16", "ShortChar17", "ShortChar18", "ShortChar19", "ShortChar20"
        };


        /***************************************************
        ****       UD Logs                     
        ****************************************************/
        public async Task<JObject> UpdateAsync(
            UDLine udline,
            string UDTable = "UD22",
            bool delete = false,
            CancellationToken ct = default)
        {
            if (delete)
                return await DeleteByIDAsync(udline, UDTable, ct).ConfigureAwait(false);

            string svc = String.Format("Ice.BO.{0}Svc/{0}s", UDTable);
            JObject lineObject = JObject.FromObject(udline);
            JObject ds = new JObject
            {
                new JProperty("Company", sesh.Company),
                new JProperty("Key1", udline.Key1),
                new JProperty("Key2", udline.Key2),
                new JProperty("Key3", udline.Key3),
                new JProperty("Key4", udline.Key4),
                new JProperty("Key5", udline.Key5),
                new JProperty("RowMod", "U")
            };

            foreach (string col in udcols)
            {
                if (lineObject.ContainsKey(col))
                    ds.Add(new JProperty(col, lineObject[col].ToString()));
            }

            return new JObject {
                new JProperty("result", await RESTCallAsync(svc, ds, ct).ConfigureAwait(false))
            };
        }

        public async Task<JObject> GetAllAsync(
            UDLine udline = null,
            string UDTable = "UD22",
            string top = "5000",
            CancellationToken ct = default)
        {
            string svc = String.Format("Ice.BO.{0}Svc/{0}s", UDTable);
            svc += "?$top=" + top;

            if (udline != null)
            {
                JObject lineObject = JObject.FromObject(udline);
                List<string> selectedcols = new List<string>();
                foreach (string col in udcols)
                {
                    if (lineObject.ContainsKey(col))
                        selectedcols.Add(col);
                }
                svc += "&$select=" + string.Join(",", selectedcols);
            }

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }

        public async Task DeleteAllAsync(string UDTable = "UD22", CancellationToken ct = default)
        {
            JObject alluds = await GetAllAsync(null, UDTable, "5000", ct).ConfigureAwait(false);
            JArray uds = JArray.FromObject(alluds["value"]);

            foreach (var ud in uds)
            {
                await DeleteByIDAsync(ud.ToObject<UDLine>(), UDTable, ct).ConfigureAwait(false);
            }
        }

        public async Task<JObject> GetByIDAsync(
            UDLine udline,
            string UDTable = "UD22",
            CancellationToken ct = default)
        {
            string svc = String.Format("Ice.BO.{0}Svc/{0}s", UDTable);
            JObject lineObject = JObject.FromObject(udline);
            List<string> selectedcols = new List<string>();

            //used only cols in the udline model
            foreach (string col in udcols)
            {
                if (lineObject.ContainsKey(col))
                    selectedcols.Add(col);
            }

            List<string> filteritems = new List<string>();
            filteritems.Add(String.Format("Key1 eq '{0}'", udline.Key1));
            filteritems.Add(String.Format("Key2 eq '{0}'", udline.Key2));
            filteritems.Add(String.Format("Key3 eq '{0}'", udline.Key3));
            filteritems.Add(String.Format("Key4 eq '{0}'", udline.Key4));
            filteritems.Add(String.Format("Key5 eq '{0}'", udline.Key5));

            svc += "?";
            svc += RESTFilterBuilder(filteritems);

            svc += "&$select=" + string.Join(",", selectedcols);

            return await RESTCallAsync(svc, null, ct).ConfigureAwait(false);
        }

        public async Task<JObject> DeleteByIDAsync(
            UDLine udline,
            string UDTable = "UD22",
            CancellationToken ct = default)
        {
            string svc = String.Format("Ice.BO.{0}Svc/DeleteByID", UDTable);
            return await RESTCallAsync(svc, new JObject {
                new JProperty("key1", udline.Key1),
                new JProperty("key2", udline.Key2),
                new JProperty("key3", udline.Key3),
                new JProperty("key4", udline.Key4),
                new JProperty("key5", udline.Key5)
            }, ct).ConfigureAwait(false);
        }

        public async Task<JObject> GetaNewUD22Async(string UDTable = "UD22", CancellationToken ct = default)
        {
            string svc = String.Format("Ice.BO.{0}Svc/GetaNew{0}", UDTable);
            return await RESTCallAsync(svc, NewDS, ct).ConfigureAwait(false);
        }
    }

    public class UDLine
    {
        public String Key1 { get; set; } = "ROW_INDICATOR"; //static
        public String Key2 { get; set; }  //required: identify line type association
        public String Key3 { get; set; } = "";
        public String Key4 { get; set; } = "";
        public String Key5 { get; set; } = "";
        public String Character01 { get; set; } = ""; //reserve for column mapping
        public String Character02 { get; set; } = "";
        public String Character03 { get; set; } = "";
        public String Character04 { get; set; } = "";
        public String Character05 { get; set; } = "";
        public String ShortChar01 { get; set; } = "";
        public String ShortChar02 { get; set; } = "";
        public String ShortChar03 { get; set; } = "";
        public String ShortChar04 { get; set; } = "";
        public String ShortChar05 { get; set; } = "";
        public String ShortChar06 { get; set; } = "";
        public String ShortChar07 { get; set; } = "";
        public String ShortChar08 { get; set; } = "";
        public String ShortChar09 { get; set; } = "";
        public String ShortChar10 { get; set; } = "";
        public String ShortChar19 { get; set; } = "";
        public String ShortChar20 { get; set; } = "";
        public double Number01 { get; set; }
        public double Number02 { get; set; }
        public double Number03 { get; set; }
        public double Number04 { get; set; }
        public double Number05 { get; set; }
        public double Number06 { get; set; }
        public double Number07 { get; set; }
        public double Number08 { get; set; }
        public double Number09 { get; set; }
        public double Number10 { get; set; }
        public double Number20 { get; set; } //checksum
        public Boolean CheckBox01 { get; set; } = false;
        //public DateTime Date01 { get; set; } = DateTime.Now;
    }
}
