using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;

namespace NovLog
{
    class NovLog
    {
        static string logDir = @"D:\GPS\Data";

        static StreamWriter log = null;
        static void LogError(string msg)
        {
            string nmsg = String.Format("{0} {1}", DateTime.Now, msg);
            Console.Error.WriteLine(nmsg);

            if (log != null)
                log.WriteLine(nmsg);
        }

        static StreamWriter data = null;
        static DateTime logfiledate;

        static DateTime FromGpsWeek(int gpsweek, double seconds)
        {
            DateTime ret = new DateTime(1980, 1, 6, 0, 0, 0);
            ret = ret.AddDays(gpsweek * 7);
            ret = ret.AddSeconds(seconds);
            return ret;
        }

        static void OpenLogfile(DateTime timestamp)
        {
            if (data != null)
                data.Close();

            logfiledate = timestamp.Date;
            string filename = String.Format("{0}.{1,1:D2}.{2,1:D2}.asc", logfiledate.Year, logfiledate.Month, logfiledate.Day);
            string datafile = Path.Combine(logDir, filename);

            LogError(String.Format("Opening logfile {0}", datafile));
            data = new StreamWriter(datafile, true);
            data.AutoFlush = true;
        }

        static void Main(string[] args)
        {
            SerialPort gps = new SerialPort("com16");
            gps.Open();

            string j = gps.ReadExisting();
            gps.Write("unlogall THISPORT_ALL\r\n");
            gps.Write("log rangea ontime 5\r\n");
            j = gps.ReadLine();

            while (true)
            {
                string line = gps.ReadLine().Trim();

                if (!line.StartsWith("#RANGEA"))
                    continue;

                string[] words = line.Split(',');


                DateTime epoch = FromGpsWeek(int.Parse(words[5]), double.Parse(words[6]));
                if (logfiledate != epoch.Date)
                    OpenLogfile(epoch);

                data.WriteLine(line);
            }
        }
    }
}
