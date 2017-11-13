using System;
using System.Collections.Generic;
using System.Globalization;
using Range2RINEX;

namespace Range2TEC
{
    class Range2TEC
    {
        static void Main(string[] args)
        {
            // Set cultureinfo to InvariantCulture, use dot as decimal separator in output and input
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            List<Epoch> epochs = new List<Epoch>();

            string line;
            RangeParser rp = new RangeParser();
            while ((line = Console.ReadLine()) != null)
            {
                Epoch e = rp.Parse(line);
                if(e != null)
                    epochs.Add(e);
            }

            // Output TEC estimate
            foreach (Epoch e in epochs)
            {
                //double codeTEC = 0;
                //double phaseTEC = 0;
                //int codeCnt = 0;
                //int phaseCnt = 0;

                double[] codeTECs = new double[33];
                double[] phaseTECs = new double[33];

                foreach (Sat s in e)
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
