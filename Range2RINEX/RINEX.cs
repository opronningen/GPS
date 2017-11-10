using System;
using System.Collections.Generic;
using System.Linq;

namespace Range2RINEX
{
    partial class Range2RINEX
    {
        /*
         * Export a List of Epoch to RINEX 2.11 format
         */
        static void ExportRinex(List<Epoch> epochs)
        {

            double interval = 0;
            if (epochs.Count > 1)
                interval = (epochs[1].timestamp - epochs[0].timestamp).Seconds;

            // Count satellites
            List<string> satList = new List<string>();
            epochs.ForEach(e => e.SV.ForEach(s => { string sat = String.Format("{0}{1}", s.System, s.PRN); if (!satList.Contains(sat)) { satList.Add(sat); } }));
            
            // Header
            Console.WriteLine("{0,9}{1,11}{2,-20}{3,-20}{4,-20}", "2.11", "", "OBSERVATION DATA", "M (MIXED)", "RINEX VERSION / TYPE");
            Console.WriteLine("{0,-20}{1,-20}{2,-20}{3,-20}", "Range2Rinex", Environment.GetEnvironmentVariable("USERNAME"), DateTime.Now.ToShortDateString(), "PGM / RUN BY / DATE");
            Console.WriteLine("{0,-60}{1,-20}", "Novatel OEMV-3", "MARKER NAME");
            Console.WriteLine("{0,-20}{0,-40}{1,-20}", "None", "OBSERVER / AGENCY");
            Console.WriteLine("{0,-20}{1,-20}{2,-20}{3,-20}", "1", "NOV OEMV3", "3.907", "REC # / TYPE / VERS");
            Console.WriteLine("{0,-20}{1,-20}{2,-20}{3,-20}", "1", "LEIAT502", "", "ANT # / TYPE");
            Console.WriteLine("{0,14}{1,14}{2,14}{3,18}{4,-20}", "3239768.2650", "301305.8344", "5467528.5499", "", "APPROX POSITION XYZ");
            Console.WriteLine("{0,14}{1,14}{2,14}{3,18}{4,-20}", "0.0000", "0.0000", "0.0000", "", "ANTENNA: DELTA H/E/N");
            Console.WriteLine("     8    C1    L1    D1    S1    P2    L2    D2    S2      # / TYPES OF OBSERV");
            Console.WriteLine("{0,10}{1, 50}{2,-20}", interval, "", "INTERVAL");
            Console.WriteLine("{0,6}{1,6}{2,6}{3,6}{4,6}{5,13:F7}{6,8}{7,9}{8,-20}", 
                epochs[0].timestamp.Year, 
                epochs[0].timestamp.Month, 
                epochs[0].timestamp.Day, 
                epochs[0].timestamp.Hour, 
                epochs[0].timestamp.Minute, 
                epochs[0].timestamp.Second, 
                "GPS", "", "TIME OF FIRST OBS");
            Console.WriteLine("{0,6}{1,6}{2,6}{3,6}{4,6}{5,13:F7}{6,8}{7,9}{8,-20}",
                epochs.Last().timestamp.Year, 
                epochs.Last().timestamp.Month, 
                epochs.Last().timestamp.Day, 
                epochs.Last().timestamp.Hour, 
                epochs.Last().timestamp.Minute, 
                epochs.Last().timestamp.Second, 
                "GPS","", "TIME OF LAST OBS");
            Console.WriteLine("{0,6}{1,54}{2,-20}", satList.Count, "", "# OF SATELLITES");
            Console.WriteLine("{0,60}{1, -20}", "", "END OF HEADER");

            // Iterate over all epochs, output observables _in the same order as given in the header_
            // C1    L1    D1    S1    P2    L2    D2    S2 
            foreach (Epoch e in epochs)
            {
                string sats = "";
                

                // Epoch header
                Console.Write(" {0,2:00} {1,2} {2,2} {3,2} {4,2}{5,11:F7}  0{6,3}",
                    e.timestamp.Year - 2000,
                    e.timestamp.Month,
                    e.timestamp.Day,
                    e.timestamp.Hour,
                    e.timestamp.Minute,
                    e.timestamp.Second,
                    e.SV.Count);

                // If > 12 sats, use continuation-lines
                int written = 0;
                while(written < e.SV.Count)
                {
                    List<Sat> toWrite = e.SV.Skip(written).Take(e.SV.Count - written > 12 ? 12: e.SV.Count - written).ToList<Sat>();
                    written += toWrite.Count();
                    toWrite.ForEach(s => sats += String.Format("{0}{1:00}", s.System, s.PRN));
                    Console.WriteLine(sats);
                    sats = "                                ";
                }

                int snr = 0;

                // Observations
                foreach (Sat s in e.SV)
                {
                    // Loss of Lock indicators:
                    // Bit 0: Lost lock between previous and current observation: cycle slip possible
                    // Bit 1: Opposite wavelength factor to the one defined for the satellite by a previous
                    //          WAVELENGTH FACT L1 / 2 line or opposite to the default. Valid for the current epoch only.
                    // Bit 2: Observation under Antispoofing (may suffer from increased noise) 
                    //
                    // Bits 0 and 1 for phase only.
                    short L1LLI = 0, P2LLI = 0, L2LLI = 0;

                    if (s.L1 != null)
                    {
                        if (s.L1.locktime < interval)
                            L1LLI = 1;

                        if (!s.L1.trackstat.ParityKnown || s.L1.trackstat.HalfCycleAdded)
                            L1LLI += 2;
                    }

                    if (s.L2 != null)
                    {
                        // Bit 0, lost lock
                        if (s.L2.locktime < interval)
                            L2LLI = 1;

                        // Bit 1, may have half-cycle ambiguity
                        if (!s.L2.trackstat.ParityKnown || s.L2.trackstat.HalfCycleAdded)   
                            L2LLI += 2;

                        // Signal type 5: L2P, 9: L2P (semi)codeless, 17: L2C
                        // Bit 2, Anti-spoofing on
                        if (s.L2.trackstat.SignalType == 9)
                        {
                            P2LLI += 4;      
                            L2LLI += 4;
                        }
                    }

                    // max 5 observations per line - C1 L1 D1 S1 P2

                    if (s.L1 != null)
                    {
                        snr = (int)Math.Min(Math.Max(Math.Round(s.L1.snr / 6.0), 0), 9);
                        Console.Write("{0,14:F3}{1,1:#}{2,1:#}{3,14:F3}{4,1:#}{5,1:#}{6,14:F3}{7,1:#}{8,1:#}{9,14:F3}{10,1:#}{11,1:#}",
                            s.L1.psr, 0, snr,
                            s.L1.adr, L1LLI, snr,
                            s.L1.dopp, 0, 0,
                            s.L1.snr, 0, 0
                            );
                    }
                    else
                    {
                        Console.Write("{0, 64}", "");                               // No L1 - dummy blank
                    }

                    if (s.L2 != null) {
                        snr = (int)Math.Min(Math.Max(Math.Round(s.L2.snr / 6.0), 0), 9);
                        Console.WriteLine("{0,14:F3}{1,1:#}{2,1:#}\n{3,14:F3}{4,1:#}{5,1:#}{6,14:F3}{7,1:#}{8,1:#}{9,14:F3}{10,1:#}{11,1:#}",
                            s.L2.psr, P2LLI, snr,
                            s.L2.adr, L2LLI, snr,
                            s.L2.dopp, 0, snr,
                            s.L2.snr, 0, 0
                            );                  
                    }
                    else
                    {
                        Console.WriteLine("{0,16}\n{0,80}", "");                    // No L2 - dummy blanks
                    }
                }
            }
        }
    }
}