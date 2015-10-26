// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Linq;
using Roguelancer.Functionality;
using Roguelancer.Models;
using System;
using System.Collections.Generic;
namespace Roguelancer.Settings {
    /// <summary>
    /// Commodities Settings
    /// </summary>
    public class CommoditiesSettings {
        /// <summary>
        /// Commodities Models
        /// </summary>
        public List<CommodityModel> CommoditiesModels { get; set; }
        /// <summary>
        /// Station Price models
        /// </summary>
        public List<StationPriceModel> StationPriceModels { get; set; }
        /// <summary>
        /// Constructor
        /// </summary>
        public CommoditiesSettings(string iniFileCommodities, string iniFilePrices) {
            try {
                StationPriceModels = new List<StationPriceModel>();
                CommoditiesModels = new List<CommodityModel>();
                for (var i = 1; i < Convert.ToInt32(IniFile.ReadINI(iniFileCommodities, "Settings", "Count", "0")) + 1; ++i) {
                    CommoditiesModels.Add(new CommodityModel() {
                        CommodityId = i,
                        Description = IniFile.ReadINI(iniFileCommodities, i.ToString(), "Description", ""),
                        Body = IniFile.ReadINI(iniFileCommodities, i.ToString(), "Body", "")
                    });
                }
                for (var i = 1; i < Convert.ToInt32(IniFile.ReadINI(iniFilePrices, "Settings", "Count", "0")) + 1; ++i) {
                    StationPriceModels.Add(new StationPriceModel() {
                        IsSelling = Convert.ToBoolean(IniFile.ReadINI(iniFilePrices, i.ToString(), "IsSelling", "false")),
                        Price = Convert.ToDecimal(IniFile.ReadINI(iniFilePrices, i.ToString(), "Price", "0.00")),
                        Selling = Convert.ToDecimal(IniFile.ReadINI(iniFilePrices, i.ToString(), "Selling", "0.00")),
                        StarSystemId = Convert.ToInt32(IniFile.ReadINI(iniFilePrices, i.ToString(), "system_index", "0")),
                        StationId = Convert.ToInt32(IniFile.ReadINI(iniFilePrices, i.ToString(), "station_index", "0")),
                        CommoditiesId = Convert.ToInt32(IniFile.ReadINI(iniFilePrices, i.ToString(), "commodities_index", "0")),
                        StationPriceId = i
                    });
                }
                for (var i = 1; i < Convert.ToInt32(IniFile.ReadINI(iniFileCommodities, "Settings", "Count", "0")) + 1; ++i) {
                    CommoditiesModels[i].Prices = StationPriceModels.Where(p => p.CommoditiesId == i).ToList();
                }
            } catch {
                throw;
            }
        }
    }
}