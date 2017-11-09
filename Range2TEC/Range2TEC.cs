using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace Range2TEC
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
        public int SignalType = 0;
        public bool FEC = false;
        public bool HalfCycleAdded = false;
        public bool PrimaryL1 = false;
    }

    class Obs
    {
        public double psr = 0;      // Pseudorange measurement (m)
        public double adr = 0;      // Carrier phase, in cycles (accumulated Doppler range)
        public double dopp = 0;
        public double snr = 0;
        public double locktime = 0;
        public TrackStat trackstat;
    }

    class Sat
    {
        public int PRN;     // For GPS, PRN. For GLONASS, slot-number.
        public char System; // From RINEX-standard; G=GPS, R=GLONASS
        public Obs L1;
        public Obs L2;
    }

    class Epoch
    {
        public DateTime timestamp;
        public List<Sat> SV = new List<Sat>();
    }

    class Range2TEC
    {
        static TrackStat ParseTrackingState(int state)
        {
            TrackStat s = new TrackStat();

            // Tracking state
            s.trackingstate = (TrackingState)(state & 31);
            state = state >> 5;

            // SV channel number
            s.SVchnum = state & 31;
            state = state >> 5;

            s.Phaselock = (state & 1) > 0;
            state = state >> 1;

            s.ParityKnown = (state & 1) > 0;
            state = state >> 1;

            s.CodeLocked = (state & 1) > 0;
            state = state >> 1;

            s.Correlator = (CorrelatorType)(state & 7);
            state = state >> 3;

            s.SatelliteSystem = (state & 7);
            state = state >> 3;

            // Skip reserved bit
            state = state >> 1;

            s.Grouped = (state & 1) > 0;
            state = state >> 1;

            s.SignalType = (state & 31);
            state = state >> 5;

            s.FEC = (state & 1) > 0;
            state = state >> 1;

            s.PrimaryL1 = (state & 1) > 0;
            state = state >> 1;

            s.HalfCycleAdded = (state & 1) > 0;
            state = state >> 1;

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
                string data = line.Split(';')[1];

                string[] fields;
                fields = header.Split(',');

                Epoch e = new Epoch();
                e.timestamp = new DateTime(1980, 1, 6, 0, 0, 0);
                e.timestamp = e.timestamp.AddDays(int.Parse(fields[5]) * 7);
                e.timestamp = e.timestamp.AddSeconds(double.Parse(fields[6]));

                fields = data.Split(',');

                int i = 0;
                while (fields.Length - i > 11)
                {
                    int prn = int.Parse(fields[i + 1]);
                    int glofreq = short.Parse(fields[i + 2]);

                    Obs o = new Obs();
                    o.psr = double.Parse(fields[i + 3]);
                    o.adr = Math.Abs(double.Parse(fields[i + 5]));
                    o.dopp = double.Parse(fields[i + 7]);
                    o.snr = double.Parse(fields[i + 8]);
                    o.locktime = float.Parse(fields[i + 9]);
                    o.trackstat = ParseTrackingState(int.Parse(fields[i + 10], NumberStyles.HexNumber));

                    // Do not use phasedata if !trackstat.parityknown
                    //if (!o.trackstat.ParityKnown)
                    //    o.adr = 0;

                    i += 10;

                    // For now, accept only 0 (GPS) or 1 (GLONASS)
                    if (o.trackstat.SatelliteSystem > 1)
                        continue;

                    // Throw out observations that are not phaselocked..
                    if (!o.trackstat.Phaselock)
                        continue;

                    // GLONASS PRN's are shown +37; correct.
                    if (o.trackstat.SatelliteSystem == 1)
                        prn -= 37;

                    // Add observation to Sat, if already exists. Else create new
                    Sat sat = e.SV.Find(s => s.PRN == prn);
                    if (sat == null)
                    {
                        sat = new Sat();

                        if (o.trackstat.SatelliteSystem == 0)
                            sat.System = 'G';   // GPS
                        else
                            sat.System = 'R';   // GLONASS

                        sat.PRN = prn;
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

            // Output TEC estimate
            foreach(Epoch e in epochs)
            {
                //double codeTEC = 0;
                //double phaseTEC = 0;
                //int codeCnt = 0;
                //int phaseCnt = 0;

                double[] codeTECs = new double[33];
                double[] phaseTECs = new double[33];

                foreach (Sat s in e.SV)
                {
                    // Only GPS for now
                    if (s.System != 'G')
                        continue;

                    // Ignore satellites with only L1 or L2 observations
                    if (s.L1 == null || s.L2 == null)
                        continue;

                    // Average - poor results
                    //// Geometry-free combination of pseudorange (Phase)
                    //if (s.L1.adr != 0 && s.L2.adr != 0)
                    //{
                    //    phaseTEC += s.L1.adr - s.L2.adr;
                    //    phaseCnt++;
                    //}

                    // Geometry-free combination of pseudorange (Code)
                    //if(s.L1.psr != 0 && s.L2.psr != 0)
                    //{
                    //    codeTEC += s.L2.psr - s.L1.psr;
                    //    codeCnt++; 
                    //}

                    // Plot each SV in its own column
                    // Geometry-free combination of pseudorange (Phase)
                    if (s.L1.adr != 0 && s.L2.adr != 0)
                    {
                        phaseTECs[s.PRN] = s.L1.adr - s.L2.adr;
                    }

                    // Geometry-free combination of pseudorange (Code)
                    if(s.L1.psr != 0 && s.L2.psr != 0)
                    {
                        codeTECs[s.PRN] = s.L2.psr - s.L1.psr; 
                    }

                    /*
                     * The data, when plotted, shows "phase jumps". Ambiguities?
                     * 
                     * Possible way to solve for this specific case - may/will result in offset of the whole arc:
                     * 1. L1 and L2 need to be dealt with separately - ambiguity on L2 does not necessarily correspond to ambiguity on L1(?)
                     * 2. For each SV
                     * 3. Find first observation. StartIndex
                     * 4. Find last observation. EndIndex
                     * 5. Take "data arc", differentiate, calculate Median Absolute Deviation
                     * 6. Remove outliers, Integrate back
                     * 6. (opt) the Geometry-free combination of low elevation satellites will be larger than high 
                     *      elevation satellites - more ionosphere to pass through. Remove qubic fit from data arc?
                     * 7. Write back to observations
                     * 8. Take Geometry-free combinations as before.
                     */

                    //Console.WriteLine("{0};{1};{2}", e.timestamp, codeTEC / codeCnt, phaseTEC/phaseCnt);
                }

                Console.Write("{0};", e.timestamp);
                for(int i = 1;i<32;i++)
                    Console.Write("{0};{1};", phaseTECs[i], codeTECs[i]);

                Console.WriteLine();


            }
        }

    }
}
