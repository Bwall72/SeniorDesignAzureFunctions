using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTrackerProcessingFunction
{
    class GpsData
    {
        public double speed { get; set; }
        public double lat { get; set; }
        public double lon { get; set; }
        public DateTime time { get; set; }
        public int numSattleites { get; set; }

        public string toString()
        {
            return String.Format("lat: {0} lon:{1} speed:{2} time:{3}",
                lat, lon, speed, time.ToString());
        }
    }
}
