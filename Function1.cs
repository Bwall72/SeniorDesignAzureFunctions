
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using NmeaParser.Nmea;
using NmeaParser.Nmea.Gnss;
using NmeaParser.Nmea.Glonass;
using NmeaParser.Nmea.Gps;
using NmeaParser.Nmea.Galileo;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace AutoTrackerProcessingFunction
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];
            string speedQuery = req.Query["speed"];
            string rpmQuery = req.Query["rpm"];
            string timeQuery = req.Query["time"];
            string gpsQuery = req.Query["gps"];
            string didQuery = req.Query["id"];

            string[] speeds = null;
            string[] rpms = null;
            string[] times = null;
            string actionResultString = "";

            if (gpsQuery == null)
            {
                

                if (speedQuery != null)
                {
                    speeds = seperateReadings(speedQuery);
                    processSpeeds(speeds);
                }
                if (rpmQuery != null)
                {
                    rpms = seperateReadings(rpmQuery);
                }
                if (timeQuery != null)
                {
                    times = seperateReadings(timeQuery);
                }

                if (speeds.Length == 1 && rpms.Length == 1 && didQuery != null)
                { 
                    return (ActionResult)new OkObjectResult(updateDatabase(speeds[0], rpms[0], didQuery.ToString()));
                }

                for (int i = 0; i < speeds.Length; i++)
                {
                    if (i >= rpms.Length || i >= times.Length)
                        break;
                    actionResultString += $"speed: {speeds[i]}, rpm: {rpms[i]}, time: {times[i]}\n";
                }

                return (ActionResult)new OkObjectResult(actionResultString);
            }
            else
            {
                Console.Out.WriteLine(gpsQuery);
                var gpsData = processGpsSentence(gpsQuery);
                if (gpsData != null)
                {
                    Console.Out.WriteLine(gpsData.toString());
                    return (ActionResult)new OkObjectResult(gpsData.toString());
                }
                return null;
            }
        }

        private static string[] seperateReadings(string readings)
        {
            if (readings.Contains(","))
            {
                return readings.Split(',');
            }
            else
            {
                return new string[] { readings };
            }
        }

        private static void processSpeeds(string[] speeds)
        {
            foreach (string i in speeds)
            {
                try
                {
                    var speed = Int16.Parse(i);
                    if (speed > 200)
                    {
                        sendText("Current speed exceeding threshold: " + speed + "kph");
                        break;
                    }
                }
                catch
                {

                }
            }
        }

        private static void sendText(string msg)
        { 
            TwilioClient.Init(Credentials.accountSid, Credentials.authToken);

            var message = MessageResource.Create(
                to: Credentials.to,
                from: Credentials.from,
                body: msg);
        }

        private static GpsData processGpsSentence(string gpsSentence)
        {
            if (gpsSentence == null)
            {
                return null;
            }
            else
            {
                try
                {                   
                    var parsedMessage = NmeaMessage.Parse(gpsSentence);
                    
                    if (parsedMessage is Gprmc)
                    {
                        var gprmc = (Gprmc)parsedMessage;

                        var data = new GpsData();
                        data.speed = gprmc.Speed;
                        data.lat = gprmc.Latitude;
                        data.lon = gprmc.Longitude;
                        data.time = gprmc.FixTime;

                        return data;
                    }
                    else if (parsedMessage is Gpgga)
                    {
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
        
        private static string updateDatabase(string speed, string rpm, string deviceId)
        {
            string combinedUpdate = speed + " " + rpm;
            var dict = new Dictionary<string, string>();
            dict.Add(deviceId, combinedUpdate);

            var json = JsonConvert.SerializeObject(dict);

            var request = WebRequest.CreateHttp(Credentials.firebaseURL);
            request.Method = "PATCH";
            request.ContentType = "application/json";
            var buffer = Encoding.UTF8.GetBytes(json);
            request.ContentLength = buffer.Length;
            request.GetRequestStream().Write(buffer, 0, buffer.Length);
            var response = request.GetResponse();
            json = (new StreamReader(response.GetResponseStream())).ReadToEnd();

            return json.ToString();
        }
    }
}
