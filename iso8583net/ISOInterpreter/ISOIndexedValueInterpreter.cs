using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace ISO8583Net.Interpreter
{
    public class ISOIndexedValueInterpreter : ISOInterpreter
    {
        private Dictionary<int, int>  m_indexLength = new Dictionary<int, int>();
        
        private Dictionary<int, Dictionary<String, String>> m_interpreter = new Dictionary<int, Dictionary<string, string>>();

        public ISOIndexedValueInterpreter(ILogger logger) : base (logger)
        {

        }

        public override String ToString(string fieldValue)
        {
            StringBuilder strBuild = new StringBuilder();

            foreach (KeyValuePair<int, int> indexLengthEntry in m_indexLength)
            {
                String subStr = fieldValue.Substring(indexLengthEntry.Key, indexLengthEntry.Value);

                String value;

                if (m_interpreter[indexLengthEntry.Key].TryGetValue(subStr, out value))
                {
                    String desc;

                    m_interpreter[indexLengthEntry.Key].TryGetValue("", out desc);

                    strBuild.Append(" ".PadRight(7, ' ') + "[" + subStr.PadRight(2, ' ') + " - " + value + "] [" + desc + "]\n");
                }
                else
                {
                    strBuild.Append("Unkown Value");
                }
            }

            return strBuild.ToString();
        }

        public void AddIndexLength(int index, int length)
        {
            if (m_indexLength.ContainsKey(index))
            {
                if (Logger.IsEnabled(LogLevel.Warning)) Logger.LogWarning("IindexLength dictionary already has an entry for index [" + index.ToString() + "] length [ " + length.ToString() + "]");
            }
            else
            {
                m_indexLength.Add(index, length);
            }
        }

        public void AddIndexValueDescriptionDic(int index, Dictionary<string,string> dic )
        {
            if (m_interpreter.ContainsKey(index))
            {
                if (Logger.IsEnabled(LogLevel.Warning)) Logger.LogWarning("Interpreter dictionary already has an entry for index [" + index.ToString() + "]");
            }
            else
            {
                m_interpreter.Add(index, dic);
            }
        }

    }
}
