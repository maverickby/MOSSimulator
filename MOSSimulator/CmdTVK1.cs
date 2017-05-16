using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOSSimulator
{
    //public enum CmdResult { SUCCESS, BAD_START_BYTE, BAD_CHKSUM1, BAD_CHKSUM2, TIMEOUT };
    

    /// <summary>
    /// Класс Команда - управление ТВК1
    /// </summary>
    public class CmdTVK1 : CmdPar
    {
        public byte START;
        public byte ADDRESS;
        public byte[] LENGTH;
        public bool EVEN;
        public ushort CHECKSUM1;
        public byte[] DATA;
        public ushort CHECKSUM2;

        public CmdResult result;

        private byte[] buf;
        const int TVK1_DATA_SIZE = 18;
        const int TVK1_PACKET_SIZE = 26;

        public CmdTVK1()
        {
            START = 0;
            ADDRESS = 0;
            LENGTH = new byte[2];
            EVEN = false;
            CHECKSUM1 = 0;
            DATA = new byte[TVK1_DATA_SIZE + 2];//должно быть 18 байт, а не 20 ? длина пакета данных 18 байт для ТВК1
            for (int i = 0; i < DATA.Length; i++)
                DATA[i] = 0;

            CHECKSUM2 = 0;

            result = CmdResult.BAD_START_BYTE;

            buf = new byte[TVK1_PACKET_SIZE];
        }

        /// <summary>
        /// Собирает все данные команды в один буфер, считает chksum.
        /// </summary>
        /// <returns>Возвращает собранный буфер (18 байт)</returns>
        public override byte[] GetBufToSend()
        {
            //ushort checksum1=0;
            //ushort checksum2=0;
            //chksum = 0;
            buf[0] = START;
            buf[1] = ADDRESS;

            buf[2] = LENGTH[0];
            buf[3] = LENGTH[1];

            if (EVEN)
                buf[3] |= 1<<7;

//             for (int i = 0; i < 4; i++)
//                 checksum1 ^= buf[i];            

            byte[] checkSumbyteArray = BitConverter.GetBytes(CHECKSUM1);
            buf[4] = checkSumbyteArray[0];
            buf[5] = checkSumbyteArray[1];

            for (int i = 0; i < TVK1_DATA_SIZE; i++)
                buf[i + 6] = DATA[i];

//             for (int i = 0; i < GSP_PACKET_SIZE - 2; i++)
//                 checksum2 ^= buf[i];

            byte[] checkSumbyteArray2 = BitConverter.GetBytes(CHECKSUM2);
            buf[TVK1_PACKET_SIZE - 2] = checkSumbyteArray2[0];
            buf[TVK1_PACKET_SIZE - 1] = checkSumbyteArray2[1];            

            return buf;
        }

        public void DiscardDataBuf()
        {
            START = 0;
            ADDRESS = 0;

            LENGTH[0] = 0;
            LENGTH[1] = 0;
            EVEN = false;
            CHECKSUM1 = 0;

            for (int i = 0; i < DATA.Length; i++)
                DATA[i] = 0;
            CHECKSUM2 = 0;
        }

    }
}
