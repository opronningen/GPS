using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Range2RINEX
{
    // Valid relevant (GPS L1/L2/L5, GLONASS L1/L2 and SBAS) observation-types in RINEX 2.11.
    public enum RINEX211ObsType
    {
        C1,     // Pseudorange C/A derived on L1 (GPS, GLONASS and SBAS)
        L1,     // Carrier-phase on L1
        D1,     // Dopplershift on L1
        S1,     // SNR on L1
        C2,     // Pseudorange L2C-derived on L2
        P2,     // Pseudorange  P-code derived on L2
        L2,     // Carrier-phase on L2
        D2,     // Pseudorange C/A-derived on L1
        S2,     // SNR on L2
        C5,     // Pseudorange C/A-derived on L1
        L5,     // Carrier-phase on L5
        D5,     // Pseudorange C/A-derived on L1
        S5      // SNR on L5
    }
    // Implement some RINEX211 specifics
    class RINEX211Sat {
        Sat s;

        public string Name
        {
            get
            {
                return s.Name;
            }
        }

        public RINEX211Sat(Sat s)
        {
            this.s = s;
        }

        // Return a formatted string with the requested observation, "    " if not found
        public string GetObservationAsString(RINEX211ObsType obsType)
        {
            int LLI = 0;
            double obs = 0;
            double strength = 0;

            // C1 L1 D1 S1 P2 L2 D2 S2 C5 L5 D5 S5
            switch (obsType)
            {
                case RINEX211ObsType.C1:
                    if (s.L1 == null)
                        break;

                    obs = s.L1.psr;
                    strength = s.L1.snr;

                    break;

                case RINEX211ObsType.L1:
                    if (s.L1 == null)
                        break;

                    obs = s.L1.adr;
                    strength = s.L1.snr;

                    if (s.L1.locktime < 10)
                        LLI = 1;
                    //if (!s.L1.trackstat.ParityKnown || s.L1.trackstat.HalfCycleAdded)
                    //    LLI += 2;

                    break;

                case RINEX211ObsType.D1:
                    if (s.L1 == null)
                        break;

                    obs = s.L1.dopp;
                    break;

                case RINEX211ObsType.S1:
                    if (s.L1 == null)
                        break;

                    obs = s.L1.snr;
                    break;

                case RINEX211ObsType.P2:
                    if (s.L2 == null || s.L2.trackstat.SignalType == 17) // Might be tracking L2C
                        break;

                    obs = s.L2.psr;
                    strength = s.L2.snr;

                    if (s.L2.trackstat.SignalType == 9)
                        LLI += 4;   // Antispoofing

                    break;

                case RINEX211ObsType.C2:
                    if (s.L2 == null || s.L2.trackstat.SignalType != 17) // Might be tracking L2P
                        break;

                    obs = s.L2.psr;
                    strength = s.L2.snr;

                    break;

                case RINEX211ObsType.L2:
                    if (s.L2 == null)
                        break;

                    obs = s.L2.adr;
                    strength = s.L2.snr;

                    if (s.L2.locktime < 10)
                        LLI = 1;
                    //if (!s.L2.trackstat.ParityKnown || s.L2.trackstat.HalfCycleAdded)
                    //    LLI += 2;

                    if (s.L2.trackstat.SignalType == 9)
                        LLI += 4;   //Antispoofing if tracking P-code codeless

                    break;

                case RINEX211ObsType.D2:
                    if (s.L2 == null)
                        break;

                    obs = s.L2.dopp;
                    break;

                case RINEX211ObsType.S2:
                    if (s.L2 == null)
                        break;

                    obs = s.L2.snr;
                    break;

                case RINEX211ObsType.C5:
                    if (s.L5 == null)
                        break;

                    obs = s.L5.psr;
                    strength = s.L5.snr;

                    if (s.L5.locktime < 10)
                        LLI = 1;

                    break;

                case RINEX211ObsType.L5:
                    if (s.L5 == null)
                        break;

                    obs = s.L5.adr;
                    strength = s.L5.snr;

                    if (s.L5.locktime < 10)
                        LLI = 1;
                    //if (!s.L5.trackstat.ParityKnown || s.L5.trackstat.HalfCycleAdded)
                    //    LLI += 2;

                    break;

                case RINEX211ObsType.D5:
                    if (s.L5 == null)
                        break;

                    obs = s.L5.dopp;
                    break;

                case RINEX211ObsType.S5:
                    if (s.L5 == null)
                        break;

                    obs = s.L5.snr;
                    break;
            }

            if (obs == 0)
                return String.Format("{0,16}", "");
            else
                return String.Format("{0,14:F3}{1,1:#}{2,1:#}", obs, LLI, (int)Math.Min(Math.Max(Math.Round(strength / 6.0), 0), 9));
        }
    }

    class RINEX211Epoch : List<RINEX211Sat>
    {
        public DateTime timestamp;

        // Return Correctly formatted header for this Epoch
        public string GetHeaderAsString()
        {
            StringBuilder header = new StringBuilder();

            header.AppendFormat(" {0,2:00} {1,2} {2,2} {3,2} {4,2}{5,11:F7}  0{6,3}",
                timestamp.Year - 2000,
                timestamp.Month,
                timestamp.Day,
                timestamp.Hour,
                timestamp.Minute,
                timestamp.Second,
                this.Count);

            int n = 0;          // Max 12 sats, then use continuation line
            foreach(RINEX211Sat s in this){
                header.Append(s.Name);
                if (n++ > 10 && this.IndexOf(s) < this.Count-1)
                {
                    header.AppendFormat("\n{0,32}", "");
                    n = 0;
                }
            }
            header.Append("\n");

            return header.ToString();
        }

        public string GetEpochAsString(List<RINEX211ObsType> ObsTypeList)
        {
            StringBuilder sb = new StringBuilder(this.GetHeaderAsString());

            foreach (var s in this)
            {
                int n = 0;      // Max 5 observations per line.
                foreach (RINEX211ObsType obsType in ObsTypeList)
                {
                    sb.Append(s.GetObservationAsString(obsType));
                    if(++n > 4)
                    {
                        sb.Append("\n");
                        n = 0;
                    }
                }
                sb.Append("\n");
            }

            return sb.ToString();
        }

        public RINEX211Epoch(Epoch e)
        {
            timestamp = e.timestamp;

            foreach(Sat s in e)
            {
                this.Add(new RINEX211Sat(s));
            }
        }
    }

    // Represents an entire log, one RINEX file
    class RINEX211Log : List<RINEX211Epoch>
    {
        List<string> SatList = new List<string>();                          // List of all satellites seen in this session
        List<RINEX211ObsType> ObsTypeList = new List<RINEX211ObsType>();    // List of all observation-types seen
        int interval = 10;

        public double PosX = 0;
        public double PosY = 0;
        public double PosZ = 0;
        public int RecNr = 1;
        public string RecType = "NOV OEMV3";
        public double RecVers = 3.907;
        public string MarkerName = "Novatel OEMV-3";
        public int AntNr = 1;
        public string AntType = "LEIAT502";

        public void Add(Epoch e)
        {
            if (e == null)
                return;

            var re = new RINEX211Epoch(e);
            this.Add(re);

            // Record all sats seen
            re.ForEach(s => { if (!SatList.Contains(s.Name)) SatList.Add(s.Name); });

            // Record all observation-types seen
            if (!ObsTypeList.Contains(RINEX211ObsType.C1))           // Only look for L1 observations if not already seen
                if (e.Exists(a => a.L1 != null))
                    ObsTypeList.AddRange(new[] { RINEX211ObsType.C1, RINEX211ObsType.L1, RINEX211ObsType.D1, RINEX211ObsType.S1 });

            if (!ObsTypeList.Contains(RINEX211ObsType.C5))
                if (e.Exists(a => a.L5 != null))
                    ObsTypeList.AddRange(new[] { RINEX211ObsType.C5, RINEX211ObsType.L5, RINEX211ObsType.D5, RINEX211ObsType.S5 });

            if (!ObsTypeList.Contains(RINEX211ObsType.L2))
                if (e.Exists(a => a.L2 != null))
                    ObsTypeList.AddRange(new[] { RINEX211ObsType.L2, RINEX211ObsType.D2, RINEX211ObsType.S2 });

            // L2 will have P2 or C2, in addition to L2, D2 and S2 - some sats may track C2 if configured (L2C)
            if (!ObsTypeList.Contains(RINEX211ObsType.P2))
                if (e.Exists(a => a.L2 != null && (a.L2.trackstat.SignalType == 5 || a.L2.trackstat.SignalType == 9)))
                    ObsTypeList.Add(RINEX211ObsType.P2);

            if (!ObsTypeList.Contains(RINEX211ObsType.C2))
                if (e.Exists(a => a.L2 != null && a.L2.trackstat.SignalType == 17))
                    ObsTypeList.Add(RINEX211ObsType.C2);

            ObsTypeList.Sort();
        }

        public RINEX211Log(List<Epoch> epochs)
        {
            foreach (Epoch e in epochs)
                this.Add(e);
        }

        public RINEX211Log()
        {
        }

        public string GetHeaderAsString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("{0,9}{1,11}{2,-20}{3,-20}{4,-20}", "2.11", "", "OBSERVATION DATA", "M (MIXED)", "RINEX VERSION / TYPE\n");
            sb.AppendFormat("{0,-20}{1,-20}{2,-20}{3,-20}\n", "Range2Rinex", Environment.GetEnvironmentVariable("USERNAME"), DateTime.Now.ToShortDateString(), "PGM / RUN BY / DATE");
            sb.AppendFormat("{0,-60}{1,-20}\n", MarkerName, "MARKER NAME");
            sb.AppendFormat("{0,-20}{0,-40}{1,-20}\n", "None", "OBSERVER / AGENCY");
            sb.AppendFormat("{0,-20}{1,-20}{2,-20}{3,-20}\n", RecNr, RecType, RecVers, "REC # / TYPE / VERS");
            sb.AppendFormat("{0,-20}{1,-20}{2,-20}{3,-20}\n", AntNr, AntType, "", "ANT # / TYPE");
            sb.AppendFormat("{0,14:F4}{1,14:F4}{2,14:F4}{3,18}{4,-20}\n", PosX, PosY, PosZ, "", "APPROX POSITION XYZ");
            sb.AppendFormat("{0,14}{1,14}{2,14}{3,18}{4,-20}\n", "0.0000", "0.0000", "0.0000", "", "ANTENNA: DELTA H/E/N");

            // Observation types, max 9 per line. Continuation lines require header in col 61-80
            sb.AppendFormat("{0,6}", ObsTypeList.Count);

            int n = 0;
            foreach(RINEX211ObsType o in ObsTypeList)
            {
                sb.AppendFormat("{0,6}", o.ToString());
                if(++n > 8 )
                {
                    if(ObsTypeList.IndexOf(o) < ObsTypeList.Count() - 1)
                    {
                        sb.Append("# / TYPES OF OBSERV\n");
                        sb.AppendFormat("{0,6}", "");
                    }

                    n = 0;
                }
            }
            sb.Append(' ', (9-n)*6);
            sb.Append("# / TYPES OF OBSERV\n");

            sb.AppendFormat("{0,10}{1, 50}{2,-20}\n", interval, "", "INTERVAL");
            sb.AppendFormat("{0,6}{1,6}{2,6}{3,6}{4,6}{5,13:F7}{6,8}{7,9}{8,-20}\n",
                this.First().timestamp.Year,
                this.First().timestamp.Month,
                this.First().timestamp.Day,
                this.First().timestamp.Hour,
                this.First().timestamp.Minute,
                this.First().timestamp.Second,
                "GPS", "", "TIME OF FIRST OBS");

            sb.AppendFormat("{0,6}{1,6}{2,6}{3,6}{4,6}{5,13:F7}{6,8}{7,9}{8,-20}\n",
                this.Last().timestamp.Year,
                this.Last().timestamp.Month,
                this.Last().timestamp.Day,
                this.Last().timestamp.Hour,
                this.Last().timestamp.Minute,
                this.Last().timestamp.Second,
                "GPS", "", "TIME OF LAST OBS");

            sb.AppendFormat("{0,6}{1,54}{2,-20}\n", SatList.Count, "", "# OF SATELLITES");
            sb.AppendFormat("{0,60}{1, -20}\n", "", "END OF HEADER");

            return sb.ToString();
        }

        public void WriteLog()
        {
            Console.Write(GetHeaderAsString());
            foreach(RINEX211Epoch e in this)
            {
                Console.Write(e.GetEpochAsString(ObsTypeList));
            }
        }
    }

    class RINEX211Writer
    {

    }
}
