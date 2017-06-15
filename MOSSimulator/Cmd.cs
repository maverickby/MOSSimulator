using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOSSimulator
{
    public enum CmdResult { SUCCESS, BAD_START_BYTE, BAD_CHKSUM1, BAD_CHKSUM2, TIMEOUT };

    public abstract class CmdPar
    {
        public abstract byte[] GetBufToSend();
    }
        

        /// <summary>
        /// Класс Команда - управление ГСП
        /// </summary>
    public class Cmd: CmdPar
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
        const int GSP_DATA_SIZE = 10;//размер исх. пакета 10, входящего - 12
        const int GSP_PACKET_SIZE = 18;

        public Cmd()
        {
            START = 0;
            ADDRESS = 0;
            LENGTH = new byte[2];
            EVEN = false;
            CHECKSUM1 = 0;
            DATA = new byte[GSP_DATA_SIZE];//длина пакета исх. данных 10 байт для ГСП
            for (int i = 0; i < DATA.Length; i++)
                DATA[i] = 0;

            CHECKSUM2 = 0;

            result = CmdResult.BAD_START_BYTE;

            buf = new byte[GSP_PACKET_SIZE];
        }
        public Cmd(int data_size)
        {
            START = 0;
            ADDRESS = 0;
            LENGTH = new byte[2];
            EVEN = false;
            CHECKSUM1 = 0;
            DATA = new byte[data_size];
            for (int i = 0; i < DATA.Length; i++)
                DATA[i] = 0;

            CHECKSUM2 = 0;

            result = CmdResult.BAD_START_BYTE;

            buf = new byte[GSP_PACKET_SIZE];
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

            for (int i = 0; i < BitConverter.ToUInt16(LENGTH, 0); i++)
                buf[i + 6] = DATA[i];

//             for (int i = 0; i < GSP_PACKET_SIZE - 2; i++)
//                 checksum2 ^= buf[i];

            byte[] checkSumbyteArray2 = BitConverter.GetBytes(CHECKSUM2);
            buf[GSP_PACKET_SIZE-2] = checkSumbyteArray2[0];
            buf[GSP_PACKET_SIZE-1] = checkSumbyteArray2[1];            

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
