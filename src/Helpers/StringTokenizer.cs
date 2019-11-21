using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Helpers
{
    class StringTokenizer
    {
        public static IEnumerable<string> Split(string input, string[] separators)
        {
            int lastPushed = 0;
            List<string> results = new List<string>();

            for (int i = 0; i < input.Length /*&& foundCount < sepListCount*/; i++)
            {
                for (int j = 0; j < separators.Length; j++)
                {
                    String separator = separators[j];
                    if (String.IsNullOrEmpty(separator)) continue;

                    Int32 currentSepLength = separator.Length;
                    if (input[i] == separator[0] && currentSepLength <= input.Length - i)
                    {
                        if (currentSepLength == 1
                            || String.CompareOrdinal(input, i, separator, 0, currentSepLength) == 0)
                        {
                            if (i - lastPushed>0)  
                                yield return input.Substring(lastPushed, i - lastPushed);

                            yield return input.Substring(i, currentSepLength);
                            i += currentSepLength - 1;
                            lastPushed = i+1;
                            break;
                        }
                    }
                }
            }
            if (input.Length > lastPushed)
                yield return input.Substring(lastPushed, input.Length - lastPushed);
        }
    }
}