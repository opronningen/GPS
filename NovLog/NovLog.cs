using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;

using IniParser;
using IniParser.Model;
using Range2RINEX;

namespace NovLog
{
    class Log
    {
        public string name = "";
        public string filepath = "";
        public StreamWriter logFile;
        public DateTime logFileDate;
        public string request;
    }

    class NovLog
    {
        static string logDir = "";
        static bool debug = false;

        static StreamWriter errLog = null;
        static void LogError(string msg)
        {
            string nmsg = String.Format("{0} {1}", DateTime.Now, msg);
            Console.Error.WriteLine(nmsg);

            if (errLog != null)
                errLog.WriteLine(nmsg);
        }

        static DateTime FromGpsWeek(int gpsweek, double seconds)
        {
            DateTime ret = new DateTime(1980, 1, 6, 0, 0, 0);
            ret = ret.AddDays(gpsweek * 7);
            ret = ret.AddSeconds(seconds);
            return ret;
        }

        static void OpenLogfile(Log log, DateTime timestamp)
        {
            string filename = String.Format("{0} {1}.{2,1:D2}.{3,1:D2}.tmp", log.name, timestamp.Year, timestamp.Month, timestamp.Day);
            string datafile = Path.Combine(logDir, filename);

            if (log.logFile != null)
            {
                log.logFile.Close();

                // If new day, rename old logfile
                if (!log.filepath.Equals(datafile))
                    System.IO.File.Move(log.filepath, log.filepath.Replace(".tmp", ".asc"));  // Will fail if full path contains ".tmp"..
            }

            log.logFile = new StreamWriter(datafile, true);
            log.filepath = datafile;
            log.logFileDate = timestamp.Date;

            LogError(String.Format("Opening logfile {0}", datafile));
        }

        /*
         */

        static void Main(string[] args)
        {
            List<Log> logs = new List<Log>();

            string inifile = "NovLog.ini";
            if(!File.Exists(inifile))
            {
                Console.Error.WriteLine("Ini-file {0} not found!", inifile);
            }

            // Read ini-file
            FileIniDataParser parser = new FileIniDataParser();
            IniData iniData = parser.ReadFile(inifile);

            logDir = iniData["NovLog"]["log-folder"];

            // Iterate over messages
            foreach (SectionData section in iniData.Sections)
            {
                if (section.SectionName == "NovLog")
                    continue;

                Log l = new Log();
                l.name = section.SectionName.ToUpper();

                if(String.Equals(iniData[section.SectionName]["trigger"], "ontime", StringComparison.InvariantCultureIgnoreCase))
                    l.request = String.Format("log {0} ontime {1}", l.name, iniData[section.SectionName]["interval"]);
                else
                    l.request = String.Format("log {0} {1}", l.name, iniData[section.SectionName]["trigger"]);

                logs.Add(l);
            }

            debug = bool.Parse(iniData["NovLog"]["debug"]);
            bool autol5 = bool.Parse(iniData["NovLog"]["auto-L5"]);

            // Open serialport
            SerialPort gps = new SerialPort(iniData["NovLog"]["com-port"]);
            gps.Open();

            string junk;
            if (String.Equals(iniData["NovLog"]["discard-on-start"], "true", StringComparison.InvariantCultureIgnoreCase))
                junk = gps.ReadExisting();

            // Request logs
            gps.Write("unlogall THISPORT_ALL\r\n");
            logs.ForEach(l => gps.Write(l.request+"\r\n"));

            // Test L5
            int[] BlockIIF = new int[] { 1, 3, 6, 8, 9, 10, 24, 25, 26, 27, 30, 32 };
            int[] L5channels = new int[6];
            int L5interval = 0;
            int L5obsCnt = 0;

            RangeParser rp = new RangeParser();
            rp.ParseL5 = autol5;

            while (true)
            {
                string line = gps.ReadLine().Trim();
                
                // Check CRC, skip junk
                if(!RangeParser.CRCok(line))
                    continue;

                string[] words = line.Split(',');

                // Find Log-object. If not found, skip line
                Log log = logs.Find(s => s.name.Equals(words[0].TrimStart('#')));
                if (log == null)
                    continue;

                DateTime epoch = FromGpsWeek(int.Parse(words[5]), double.Parse(words[6]));
                if (log.logFileDate != epoch.Date)
                    OpenLogfile(log, epoch);

                log.logFile.WriteLine(line);

                // Process RANGEA special - assign the strongest Block IIR SNR sats to the L5 channels
                if (autol5 && line.StartsWith("#RANGEA"))
                {
                    Epoch e = rp.Parse(line);
                    L5obsCnt += e.Count(s => s.L5 != null);

                    if (--L5interval < 1)
                        L5interval = 30;
                    else
                        continue;

                    if(debug)
                        Console.WriteLine("{0} Evaluating L5 channels,  {1} observations since last", DateTime.Now, L5obsCnt);

                    L5obsCnt = 0;

                    // Check to see that the sats we've assigned to the L5 channels are still being tracked
                    for (int i = 0; i < 6; i++)
                    {
                        if (L5channels[i] == 0)
                            continue;

                        if (e.Find(s => s.PRN == L5channels[i]) == null)
                        {
                            if (debug)
                                Console.WriteLine("Not tracking PRN {0} on channel {1}", L5channels[i], i + 14);
                            L5channels[i] = 0;
                        }
                    }
                    // Find BlockIIF satellites not already being tracked, that has L1 observations
                    List<Sat> candidates = e.Where(s => 
                        BlockIIF.Contains( s.PRN ) && 
                        ! L5channels.Contains( s.PRN) && 
                        s.L1 != null && 
                        s.System == 'G'
                    ).ToList<Sat>();

                    // Sort candidate satellites by SNR on L1
                    Sat[] c = candidates.OrderBy(s => s.L1.snr).ToArray<Sat>();
                    int ix = 0;
                    for (int i = 0; i < 6; i++)
                        if(L5channels[i] == 0)
                        {
                            if (ix > c.Length-1)
                                break;

                            L5channels[i] = c[ix++].PRN;

                            if (debug)
                                Console.WriteLine("Assigning PRN {0} to channel {1}", L5channels[i], i + 14);

                            gps.Write(String.Format("assign {0} {1}\r\n", i + 14, L5channels[i]));
                        }
                }
            }
        }
    }
}
