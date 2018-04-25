using System;
using System.Collections.Generic;
using System.Globalization;

namespace Range2RINEX
{
    #region Supporting structures
    public enum TrackingState
    {
        L1Idle,
        L1SkySearch,
        L1WidePullIn,
        L1NarrowPullIn,
        L1Phaselock,
        L1Reacquisition,
        L1Steering,
        L1Frequencylock,
        L2Idle,
        L2Pcodealign,
        L2Search,
        L2Phaselock,
        L2Steering
    }

    public enum CorrelatorType
    {
        NA,
        Standard,
        Narrow,
        Reserved,
        PAC             /* Pulse Aperture Correlator */
    }

    public class TrackStat
    {
        public TrackingState State = 0;
        public int SVchnum = 0;
        public bool Phaselock = false;
        public bool ParityKnown = false;
        public bool CodeLocked = false;
        public CorrelatorType Correlator = CorrelatorType.NA;
        public int SatelliteSystem = 0;
        public bool Grouped = false;
        public int SignalType = 0;              // GPS: 0 = L1 C/A, 5 = L2 P, 9 = L2 P codeless, 14 = L5, 17 = L2C. GLONASS: 0 = L1 C/A, 5 = L2 P. SBAS: 0 = L1 C/A
        public bool FEC = false;
        public bool HalfCycleAdded = false;
        public bool PrimaryL1 = false;          // L1 (or L5) if true, L2 if false
        public bool PRNlocked = false;
        public bool ChannelForced = false;

        public TrackStat (int state) { 
            State = (TrackingState)(state & 31);
            SVchnum = (state >>= 5) & 31;
            Phaselock = ((state >>= 5) & 1) > 0;
            ParityKnown = ((state >>= 1) & 1) > 0;
            CodeLocked = ((state >>= 1) & 1) > 0;
            Correlator = (CorrelatorType)((state >>= 1) & 7);
            SatelliteSystem = ((state >>= 3) & 7);
            Grouped = ((state >>= 4) & 1) > 0;
            SignalType = ((state >>= 1) & 31);
            FEC = ((state >>= 5) & 1) > 0;
            PrimaryL1 = ((state >>= 1) & 1) > 0;
            HalfCycleAdded = ((state >>= 1) & 1) > 0;
            PRNlocked = ((state >>= 2) & 1) > 0;
            ChannelForced = ((state >>= 1) & 1) > 0;
        }
    }

    public class Obs
    {
        public double psr = 0;      // Pseudorange measurement (m)
        public double psr_std = 0;  // Pseudorange measurement standard deviation (m)
        public double adr = 0;      // Carrier phase, in cycles (accumulated Doppler range)
        public double adr_std = 0;  // Estimated carrier phase standard deviation (cycles)
        public double dopp = 0;     // Instantaneous carrier Doppler frequency (Hz)
        public double snr = 0;      // Carrier to noise density ratio C/No = 10[log10(S / N0)] (dB-Hz)
        public double locktime = 0; // # of seconds of continuous tracking (no cycle slipping)
        public TrackStat trackstat;
    }

    public class Sat
    {
        public string Name
        {
            get
            {
                return (String.Format("{0}{1:00}", System, PRN));
            }
        }

        public int PRN;                 // For GPS, PRN. For GLONASS, slot-number.
        public char System = ' ';       // From RINEX-standard; G=GPS, R=GLONASS, S=SBAS
        public Obs L1;
        public Obs L2;
        public Obs L5;

        public Sat(char system, int prn)
        {
            System = system;
            PRN = prn;
        }
    }

    public class Epoch : List<Sat>
    {
        public DateTime timestamp = new DateTime(1980, 1, 6, 0, 0, 0);

        public Epoch(int GPSWeek, double GPSsecs)
        {
            timestamp = timestamp.AddDays(GPSWeek*7);
            timestamp = timestamp.AddSeconds(GPSsecs);
        }
    }
    #endregion

    public class RangeParser
    {
        public bool ParseGLO = true;    // Parse or ignore GLONASS observations
        public bool ParseL5 = true;     // Parse or ignore L5 observations
        public bool ParseSBAS = true;   // Parse or ignore SBAS observations

        //Parse a #RANGEA message. Return Epoch
        public Epoch Parse(string line)
        {
            if (!line.StartsWith("#RANGEA"))
                return null;

            // Check CRC of message
            if (!CRCok(line))
            {
                Console.Error.WriteLine("CRC failed.");
                return null;
            }

            string header = line.Split(';')[0];
            string[] fields = header.Split(',');

            Epoch e = new Epoch(int.Parse(fields[5]), double.Parse(fields[6]));

            string data = line.Split(new char[] { ';', '*' })[1];
            fields = data.Split(',');

            int i = 1;  // Skip "number of observations to follow"
            while (fields.Length - i > 9)
            {
                int prn = int.Parse(fields[i++]);
                int glofreq = short.Parse(fields[i++]) - 7;

                Obs o = new Obs
                {
                    psr = double.Parse(fields[i++]),
                    psr_std = double.Parse(fields[i++]),
                    adr = Math.Abs(double.Parse(fields[i++])),
                    adr_std = double.Parse(fields[i++]),
                    dopp = double.Parse(fields[i++]),
                    snr = double.Parse(fields[i++]),
                    locktime = float.Parse(fields[i++]),
                    trackstat = new TrackStat(int.Parse(fields[i++], NumberStyles.HexNumber))
                };

                // Accept GPS, GLONASS, SBAS
                if (o.trackstat.SatelliteSystem > 2)
                    continue;

                if (!ParseGLO && o.trackstat.SatelliteSystem == 1)
                    continue;

                if (!ParseSBAS && o.trackstat.SatelliteSystem == 2)
                    continue;

                if (!ParseL5 && o.trackstat.SignalType == 14)
                    continue;

                // Throw out observations where parity is unknown
                if ( !o.trackstat.ParityKnown)
                    continue;

                // o.trackstat.SatelliteSystem: 0 = GPS, 1 = GLONASS, 2 = WAAS, 7 = Other
                char system = 'G';                          // Default to GPS
                if (o.trackstat.SatelliteSystem == 1)
                {
                    prn -= 37;                              // GLONASS PRN's are shown +37; fix.
                    system = 'R';
                }
                else if(o.trackstat.SatelliteSystem == 2)
                {
                    prn -= 100;                             // SBAS should be reported -100, according to spec
                    system = 'S';
                }

                // Add observation to Sat, if already exists. Else create new
                Sat sat = e.Find(s => (s.PRN == prn && s.System == system));
                if (sat == null)
                {
                    sat = new Sat(system, prn);

                    e.Add(sat);
                }

                if (o.trackstat.PrimaryL1)
                {
                    if(o.trackstat.SignalType == 14)
                    {
                        sat.L5 = o;
                    }
                    else
                    {
                        // If a sat is manually assigned to a channel, we might see more than one observation of each type for the same sat. 
                        if (sat.L1 != null)
                            Console.Error.WriteLine("Observation on L1 for PRN {0} already parsed!", prn);

                        sat.L1 = o;
                    }
                }
                else
                {
                    if (sat.L2 != null)
                        Console.Error.WriteLine("Observation on L2 for PRN {0} already parsed!", prn);

                    sat.L2 = o;
                }
            }

            return e;
        }

        #region Support methods
        /* --------------------------------------------------------------------------
            Calculate a CRC value to be used by CRC calculation functions.
        -------------------------------------------------------------------------- */
        const long CRC32_POLYNOMIAL = 0xEDB88320L;
        static ulong CRC32Value(int i)
        {
            int j;
            ulong ulCRC;
            ulCRC = (ulong)i;
            for (j = 8; j > 0; j--)
            {
                if ((ulCRC & 1) > 0)
                    ulCRC = (ulCRC >> 1) ^ CRC32_POLYNOMIAL;
                else
                    ulCRC >>= 1;
            }
            return ulCRC;
        }

        /* --------------------------------------------------------------------------
            Calculates the CRC-32 of a block of data all at once
        -------------------------------------------------------------------------- */
        static ulong CalculateBlockCRC32(string msg)
        {
            ulong ulTemp1;
            ulong ulTemp2;
            ulong ulCRC = 0;
            int i = 0;

            while (i < msg.Length)
            {
                ulTemp1 = (ulCRC >> 8) & 0x00FFFFFFL;
                ulTemp2 = CRC32Value(((int)ulCRC ^ msg[i++]) & 0xff);
                ulCRC = ulTemp1 ^ ulTemp2;
            }

            return (ulCRC);
        }

        public static bool CRCok(string message)
        {
            string[] parts = message.Split('*');
            if (parts.Length != 2)
                return false;

            string msg = parts[0].TrimStart('#');
            ulong crc = ulong.Parse(parts[1], NumberStyles.HexNumber);

            return (CalculateBlockCRC32(msg) == crc);
        }
        #endregion
    }
}
