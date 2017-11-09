using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;

/*
 * Parse Range-message from Novatel OEMV.
 * Status:
 *  GPS handled correct, except L2C and L5
 *  GLONASS handled correct
 *  
 * To do:
 *      Handle GPS L2C
 *      Handle GPS L5
 *      Handle SBAS
 *      Parse RangeCMP
 *      Parse binary format
 * 
 */
namespace Range2RINEX
{
    enum TrackingState
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

    enum CorrelatorType
    {
        NA,
        Standard,
        Narrow,
        Reserved,
        PAC             /* Pulse Aprerture Correlator */
    }

    class TrackStat
    {
        public TrackingState trackingstate = 0;
        public int SVchnum = 0;
        public bool Phaselock = false;
        public bool ParityKnown = false;
        public bool CodeLocked = false;
        public CorrelatorType Correlator = CorrelatorType.NA;
        public int SatelliteSystem = 0;
        public bool Grouped = false;
        public int SignalType = 0;              // GPS: 0 = L1 C/A, 5 = L2 P, 9 = L2 P codeless, 17 = L2C. GLONASS: 0 = L1 C/A, 5 = L2 P. SBAS: 0 = L1 C/A
        public bool FEC = false;
        public bool HalfCycleAdded = false;
        public bool PrimaryL1 = false;          // If 0, L2
        public bool PRNlocked = false;
        public bool ChannelForced = false;
    }

    class Obs
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

    class Sat
    {
        public int PRN;     // For GPS, PRN. For GLONASS, slot-number.
        public char System; // From RINEX-standard; G=GPS, R=GLONASS, S=SBAS
        public Obs L1;
        public Obs L2;
    }

    class Epoch
    {
        public DateTime timestamp;
        public List<Sat> SV = new List<Sat>();
    }

    partial class Range2RINEX
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
            ulong crc = ulong.Parse(parts[1], System.Globalization.NumberStyles.HexNumber);

            return (CalculateBlockCRC32(msg) == crc);
        }

        static void Main(string[] args)
        {
            // Set cultureinfo to InvariantCulture, use dot as decimal separator in output and input
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            List<Epoch> epochs = new List<Epoch>();

            string line;

            while ((line = Console.ReadLine()) != null)
            {
                if (!line.StartsWith("#RANGEA"))
                    continue;

                // Check CRC of message
                if (!CRCok(line))
                {
                    Console.Error.WriteLine("CRC failed.");
                    continue;
                }

                string header = line.Split(';')[0];
                string data = line.Split(new char[]{ ';','*'})[1];

                string[] fields;
                fields = header.Split(',');

                Epoch e = new Epoch();
                e.timestamp = new DateTime(1980, 1, 6, 0, 0, 0);
                e.timestamp = e.timestamp.AddDays(int.Parse(fields[5]) * 7);
                e.timestamp = e.timestamp.AddSeconds(double.Parse(fields[6]));

                fields = data.Split(',');

                int i = 1;  // Skip "number of observations to follow"
                while (fields.Length - i > 9)
                {
                    int prn = int.Parse(fields[i++]);

                    int glofreq = short.Parse(fields[i++])-7;

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

                    // TO-DO; check that no observation is already in the slot
                    if (o.trackstat.PrimaryL1)
                        sat.L1 = o;
                    else
                        sat.L2 = o;

                }

                epochs.Add(e);
            }

            // Output RINEX
            ExportRinex(epochs);
        }
    }
}
