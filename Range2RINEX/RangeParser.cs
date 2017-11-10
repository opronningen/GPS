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
        PAC             /* Pulse Aprerture Correlator */
    }

    public class TrackStat
    {
        public TrackingState trackingstate = 0;
        public int SVchnum = 0;
        public bool Phaselock = false;
        public bool ParityKnown = false;
        public bool CodeLocked = false;
        public CorrelatorType Correlator = CorrelatorType.NA;
        public int SatelliteSystem = 0;
        public bool Grouped = false;
        public int SignalType = 0;              // GPS: 0 = L1 C/A, 5 = L2 P, 9 = L2 P codeless, 14 = L5??, 17 = L2C. GLONASS: 0 = L1 C/A, 5 = L2 P. SBAS: 0 = L1 C/A
        public bool FEC = false;
        public bool HalfCycleAdded = false;
        public bool PrimaryL1 = false;          // L1 if true, L2 if false
        public bool PRNlocked = false;
        public bool ChannelForced = false;
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
        public int PRN;     // For GPS, PRN. For GLONASS, slot-number.
        public char System; // From RINEX-standard; G=GPS, R=GLONASS, S=SBAS
        public Obs L1;
        public Obs L2;
    }

    public class Epoch
    {
        public DateTime timestamp;
        public List<Sat> SV = new List<Sat>();
    }
    #endregion

    public class RangeParser
    {
        static TrackStat ParseTrackingState(int state)
        {
            TrackStat s = new TrackStat();

            // Tracking state
            s.trackingstate = (TrackingState)(state & 31);
            state >>= 5;

            // SV channel number
            s.SVchnum = state & 31;
            state >>= 5;

            s.Phaselock = (state & 1) > 0;
            state >>= 1;

            s.ParityKnown = (state & 1) > 0;
            state >>= 1;

            s.CodeLocked = (state & 1) > 0;
            state >>= 1;

            s.Correlator = (CorrelatorType)(state & 7);
            state >>= 3;

            s.SatelliteSystem = (state & 7);
            state >>= 3;

            // Skip reserved bit
            state >>= 1;

            s.Grouped = (state & 1) > 0;
            state >>= 1;

            s.SignalType = (state & 31);
            state >>= 5;

            s.FEC = (state & 1) > 0;
            state >>= 1;

            s.PrimaryL1 = (state & 1) > 0;
            state >>= 1;

            s.HalfCycleAdded = (state & 1) > 0;
            state >>= 1;

            // Skip reserved
            state >>= 1;

            s.PRNlocked = (state & 1) > 0;
            state >>= 1;

            s.ChannelForced = (state & 1) > 0;

            return s;
        }

        //Parse a #RANGEA message. Return Epoch
        public static Epoch Parse(string line)
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

            Epoch e = new Epoch();
            e.timestamp = new DateTime(1980, 1, 6, 0, 0, 0);
            e.timestamp = e.timestamp.AddDays(int.Parse(fields[5]) * 7);
            e.timestamp = e.timestamp.AddSeconds(double.Parse(fields[6]));

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
                    trackstat = ParseTrackingState(int.Parse(fields[i++], NumberStyles.HexNumber))
                };

                // For now, accept only 0 (GPS) or 1 (GLONASS)
                if (o.trackstat.SatelliteSystem > 1)
                    continue;

                // Throw out observations that are not phaselocked..
                if (!o.trackstat.Phaselock)
                    continue;

                // GLONASS PRN's are shown +37; fix.
                if (o.trackstat.SatelliteSystem == 1)
                    prn -= 37;

                // o.trackstat.SatelliteSystem: 0 = GPS, 1 = GLONASS, 2 = WAAS, 7 = Other 
                char system = o.trackstat.SatelliteSystem == 0 ? 'G' : 'R';

                // Add observation to Sat, if already exists. Else create new
                Sat sat = e.SV.Find(s => (s.PRN == prn && s.System == system));
                if (sat == null)
                {
                    sat = new Sat
                    {
                        System = system,
                        PRN = prn
                    };

                    e.SV.Add(sat);
                }

                // Note - this will fail if tracking L5 - the documentation is not clear on what bits are set on L5 observations..
                // Signaltype may be 14 - to be verified.
                if (o.trackstat.PrimaryL1)
                {
                    if (sat.L1 != null)
                        Console.Error.WriteLine("Observation on L1 for PRN {0} already parsed!", prn);

                    sat.L1 = o;
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

        // Parse #RANGEA, add to given list of Epoch
        public static void Parse(string line, List<Epoch> epochs)
        {
            epochs.Add(Parse(line));
        }

        // Parse list of #RANGEA, return list of Epoch
        public static List<Epoch> Parse(List<string> lines, List<Epoch> epochs = null)
        {
            if (epochs == null)
                epochs = new List<Epoch>();

            foreach(string line in lines)
            {
                Epoch e = Parse(line);

                if (e != null)
                    epochs.Add(e);
            }

            return epochs;
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

        static bool CRCok(string message)
        {
            string[] parts = message.Split('*');
            string msg = parts[0].TrimStart('#');
            ulong crc = ulong.Parse(parts[1], NumberStyles.HexNumber);

            return (CalculateBlockCRC32(msg) == crc);
        }
        #endregion
    }
}
