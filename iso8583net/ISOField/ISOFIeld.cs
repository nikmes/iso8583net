using ISO8583Net.Packager;
using Microsoft.Extensions.Logging;
using System;

namespace ISO8583Net.Field
{
    /// <summary>
    /// 
    /// </summary>
    public class ISOField : ISOComponent
    {
        private ISOPackager m_packager;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="packager"></param>
        /// <param name="number"></param>
        /// <param name="value"></param>
        public ISOField(ILogger logger, ISOPackager packager, int number, String value) : base(logger, number, value)
        {          
            m_packager = packager;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="packager"></param>
        /// <param name="number"></param>
        public ISOField(ILogger logger, ISOPackager packager, int number) : base(logger, number)
        {
            m_packager = packager;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="fieldValue"></param>
        public override void Set(int fieldNumber, string fieldValue)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <returns></returns>
        public override String GetFieldValue(int fieldNumber)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldNumber"></param>
        /// <param name="subField"></param>
        /// <returns></returns>
        public override String GetFieldValue(int fieldNumber, int subField)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 
        /// </summary>
        public override void Trace()
        {
            Logger.LogInformation("F[" + m_number.ToString().PadLeft(3, '0') + "]".PadRight(1, ' ') + "[" + value + "]");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override String ToString()
        {
            return (String.Format("F[{0}]{1}[{2}]\n{3}", m_number.ToString().PadLeft(3, '0')," ".PadRight(1, ' '), value, m_packager.InterpretField(value)));
        }
    }
}
