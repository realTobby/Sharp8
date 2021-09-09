using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp8.CHIP8
{
    public struct OpcodeEight
    {
        public ushort Data { get; set; }
        public byte Set { get; set; }
        public ushort NNN { get; set; }
        public byte NN { get; set; }
        public byte N { get; set; }
        public byte X { get; set; }
        public byte Y { get; set;  }
    }
}
