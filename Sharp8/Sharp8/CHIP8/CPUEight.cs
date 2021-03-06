using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharp8.CHIP8
{
    public class CPUEight
    {
		const int ScreenWidth = 64, ScreenHeight = 32;

		Action<bool[,]> draw;
		Action<int> beep;
		bool[,] buffer = new bool[ScreenWidth, ScreenHeight];

		// To reduce flicker, we delay clearing pixels by a frame
		bool[,] pendingClearBuffer = new bool[ScreenWidth, ScreenHeight];

		bool needsRedraw = true;

		// Registers
		byte[] V = new byte[16];
		// Timers
		byte Delay;
		// Address/Program Counters
		ushort I, PC = 0x200;
		// Stack
		byte SP;
		ushort[] Stack = new ushort[16];

		// Memory & ROM
		byte[] RAM = new byte[0x1000];

		// OpCodes
		Dictionary<byte, Action<OpcodeEight>> opCodes;
		Dictionary<byte, Action<OpcodeEight>> opCodesMisc;

		Random rnd = new Random();

		// Keys that are currently pressed.
		HashSet<byte> pressedKeys = new HashSet<byte>();


		public CPUEight(Action<bool[,]> draw, Action<int> beep)
		{
			this.draw = draw;
			this.beep = beep;

			WriteFont();

			opCodes = new Dictionary<byte, Action<OpcodeEight>>
			{
				{ 0x0, ClearOrReturn },
				{ 0x1, Jump },
				{ 0x2, CallSubroutine },
				{ 0x3, SkipIfXEqual },
				{ 0x4, SkipIfXNotEqual },
				{ 0x5, SkipIfXEqualY },
				{ 0x6, SetX },
				{ 0x7, AddX },
				{ 0x8, Arithmetic },
				{ 0x9, SkipIfXNotEqualY },
				{ 0xA, SetI },
				{ 0xB, JumpWithOffset },
				{ 0xC, Rnd },
				{ 0xD, DrawSprite },
				{ 0xE, SkipOnKey },
				{ 0xF, Misc },
			};

			opCodesMisc = new Dictionary<byte, Action<OpcodeEight>>
			{
				{ 0x07, SetXToDelay },
				{ 0x0A, WaitForKey },
				{ 0x15, SetDelay },
				{ 0x18, SetSound },
				{ 0x1E, AddXToI },
				{ 0x29, SetIForChar },
				{ 0x33, BinaryCodedDecimal },
				{ 0x55, SaveX },
				{ 0x65, LoadX },
			};
		}

		void WriteFont()
		{
			var offset = 0x0;
			WriteFont(5 * offset++, Font.D0);
			WriteFont(5 * offset++, Font.D1);
			WriteFont(5 * offset++, Font.D2);
			WriteFont(5 * offset++, Font.D3);
			WriteFont(5 * offset++, Font.D4);
			WriteFont(5 * offset++, Font.D5);
			WriteFont(5 * offset++, Font.D6);
			WriteFont(5 * offset++, Font.D7);
			WriteFont(5 * offset++, Font.D8);
			WriteFont(5 * offset++, Font.D9);
			WriteFont(5 * offset++, Font.DA);
			WriteFont(5 * offset++, Font.DB);
			WriteFont(5 * offset++, Font.DC);
			WriteFont(5 * offset++, Font.DD);
			WriteFont(5 * offset++, Font.DE);
			WriteFont(5 * offset++, Font.DF);
		}

		void WriteFont(int address, long fontData)
		{
			RAM[address + 0] = (byte)((fontData & 0xF000000000) >> (8 * 4));
			RAM[address + 1] = (byte)((fontData & 0x00F0000000) >> (8 * 3));
			RAM[address + 2] = (byte)((fontData & 0x0000F00000) >> (8 * 2));
			RAM[address + 3] = (byte)((fontData & 0x000000F000) >> (8 * 1));
			RAM[address + 4] = (byte)((fontData & 0x00000000F0) >> (8 * 0));
		}

		public void LoadProgram(byte[] data)
		{
			Array.Copy(data, 0, RAM, 0x200, data.Length);
		}

		public void Tick()
		{
			// Read the two bytes of OpCode (big endian).
			var opCode = (ushort)(RAM[PC++] << 8 | RAM[PC++]);
			Console.WriteLine("Tick: " + opCode + " / " + opCode.ToString("X"));
			//Debug.WriteLine((PC - 2).ToString("X4") + ": " + opCode.ToString("X4"));

			// Split data into the possible formats the instruction might need.
			// https://en.wikipedia.org/wiki/CHIP-8#Opcode_table
			var op = new OpcodeEight()
			{
				Data = opCode,
				NNN = (ushort)(opCode & 0x0FFF),
				NN = (byte)(opCode & 0x00FF),
				N = (byte)(opCode & 0x000F),
				X = (byte)((opCode & 0x0F00) >> 8),
				Y = (byte)((opCode & 0x00F0) >> 4),
			};

			// Loop up the OpCode using the first nibble and execute.
			opCodes[(byte)(opCode >> 12)](op);
			if (needsRedraw)
			{
				needsRedraw = false;
				draw(buffer);
			}
		}

		public void Tick60Hz()
		{
			//Debug.WriteLine("60Hz tick");
			if (Delay > 0)
				Delay--;

			if (needsRedraw)
			{
				needsRedraw = false;
				draw(buffer);
			}
		}

		// Misc has its own dictionary because it's full of random stuff.
		void Misc(OpcodeEight data)
		{
			if (opCodesMisc.ContainsKey(data.NN))
				opCodesMisc[data.NN](data);
		}

		public void KeyDown(byte key)
		{
			Console.WriteLine("Pressed Key: " + key.ToString("X"));
			pressedKeys.Add(key);
		}

		public void KeyUp(byte key)
		{
			Console.WriteLine("Released Key: " + key.ToString("X"));
			pressedKeys.Remove(key);
		}

		// http://devernay.free.fr/hacks/chip8/C8TECH10.HTM#3.1

		/// <summary>
		/// Handles 0x0... which either clears the screen or returns from a subroutine.
		/// </summary>
		void ClearOrReturn(OpcodeEight data)
		{
			if (data.NN == 0xE0)
			{
				for (var x = 0; x < ScreenWidth; x++)
					for (var y = 0; y < ScreenHeight; y++)
						buffer[x, y] = false;
			}
			else if (data.NN == 0xEE)
				PC = Pop();
		}

		/// <summary>
		/// Jumps to location nnn (not a subroutine, so old PC is not pushed to the stack).
		/// </summary>
		void Jump(OpcodeEight data)
		{
			PC = data.NNN;
		}

		/// <summary>
		/// Jumps to location nnn + v[0] (not a subroutine, so old PC is not pushed to the stack).
		/// </summary>
		void JumpWithOffset(OpcodeEight data)
		{
			PC = (ushort)(data.NNN + V[0]);
		}

		/// <summary>
		/// Jumps to subroutine nnn (unlike Jump, this pushes the previous PC to the stack to allow return).
		/// </summary>
		void CallSubroutine(OpcodeEight data)
		{
			Push(PC);
			PC = data.NNN;
		}

		/// <summary>
		/// Skips the next instruction (two bytes) if V[x] == nn.
		/// </summary>
		void SkipIfXEqual(OpcodeEight data)
		{
			if (V[data.X] == data.NN)
				PC += 2;
		}

		/// <summary>
		/// Skips the next instruction (two bytes) if V[x] != nn.
		/// </summary>
		void SkipIfXNotEqual(OpcodeEight data)
		{
			if (V[data.X] != data.NN)
				PC += 2;
		}

		/// <summary>
		/// Skips the next instruction (two bytes) if V[x] == V[y].
		/// </summary>
		void SkipIfXEqualY(OpcodeEight data)
		{
			if (V[data.X] == V[data.Y])
				PC += 2;
		}

		/// <summary>
		/// Skips the next instruction (two bytes) if V[x] != V[y].
		/// </summary>
		void SkipIfXNotEqualY(OpcodeEight data)
		{
			if (V[data.X] != V[data.Y])
				PC += 2;
		}

		/// <summary>
		/// Sets V[x] == nn.
		/// </summary>
		void SetX(OpcodeEight data)
		{
			V[data.X] = data.NN;
		}

		/// <summary>
		/// Adds nn to V[x].
		/// </summary>
		void AddX(OpcodeEight data)
		{
			V[data.X] += data.NN; // TODO: Do we need to handle overflow?
		}

		/// <summary>
		/// Sets V[x] to V[y].
		/// </summary>
		void Arithmetic(OpcodeEight data)
		{
			switch (data.N)
			{
				case 0x0:
					V[data.X] = V[data.Y];
					break;
				case 0x1:
					V[data.X] |= V[data.Y];
					break;
				case 0x2:
					V[data.X] &= V[data.Y];
					break;
				case 0x3:
					V[data.X] ^= V[data.Y];
					break;
				case 0x4:
					V[0xF] = (byte)(V[data.X] + V[data.Y] > 0xFF ? 1 : 0); // Set flag if we overflowed.
					V[data.X] += V[data.Y];
					break;
				case 0x5:
					V[0xF] = (byte)(V[data.X] > V[data.Y] ? 1 : 0); // Set flag if we underflowed.
					V[data.X] -= V[data.Y];
					break;
				case 0x6:
					V[0xF] = (byte)((V[data.X] & 0x1) != 0 ? 1 : 0); // Set flag if we shifted a 1 off the end.
					V[data.X] /= 2; // Shift right.
					break;
				case 0x7: // Note: This is Y-X, 5 was X-Y.
					V[0xF] = (byte)(V[data.Y] > V[data.X] ? 1 : 0); // Set flag if we underflowed.
					V[data.Y] -= V[data.X];
					break;
				case 0xE:
					V[0xF] = (byte)((V[data.X] & 0xF) != 0 ? 1 : 0); // Set flag if we shifted a 1 off the end.
					V[data.X] *= 2; // Shift left.
					break;
			}
		}

		/// <summary>
		/// Sets the I register.
		/// </summary>
		void SetI(OpcodeEight data)
		{
			I = data.NNN;
		}

		/// <summary>
		/// ANDs a random number with nn and stores in V[x].
		/// </summary>
		void Rnd(OpcodeEight data)
		{
			V[data.X] = (byte)(rnd.Next(0, 256) & data.NN);
		}

		/// <summary>
		/// Draws an n-byte sprite from register I at V[x], V[y]. Sets V[0xF] if it collides.
		/// </summary>
		void DrawSprite(OpcodeEight data)
		{
			var startX = V[data.X];
			var startY = V[data.Y];
			//Debug.WriteLine(string.Format("Drawing {0}-line sprite from {1} at {2}, {3}", data.N, I, startX, startY));

			// Write any pending clears
			for (var x = 0; x < ScreenWidth; x++)
			{
				for (var y = 0; y < ScreenHeight; y++)
				{
					if (pendingClearBuffer[x, y])
					{
						if (buffer[x, y])
							needsRedraw = true;

						pendingClearBuffer[x, y] = false;
						buffer[x, y] = false;
					}
				}
			}

			V[0xF] = 0;
			for (var i = 0; i < data.N; i++)
			{
				var spriteLine = RAM[I + i]; // A line of the sprite to render

				for (var bit = 0; bit < 8; bit++)
				{
					var x = (startX + bit) % ScreenWidth;
					var y = (startY + i) % ScreenHeight;

					var spriteBit = ((spriteLine >> (7 - bit)) & 1);
					var oldBit = buffer[x, y] ? 1 : 0;

					if (oldBit != spriteBit)
						needsRedraw = true;

					// New bit is XOR of existing and new.
					var newBit = oldBit ^ spriteBit;

					if (newBit != 0)
						buffer[x, y] = true;
					else // Otherwise write a pending clear
						pendingClearBuffer[x, y] = true;

					// If we wiped out a pixel, set flag for collission.
					if (oldBit != 0 && newBit == 0)
						V[0xF] = 1;
				}
			}
		}

		/// <summary>
		/// Skips the next instruction based on the key at V[x] being pressed/not pressed.
		/// </summary>
		void SkipOnKey(OpcodeEight data)
		{
			if (
				(data.NN == 0x9E && pressedKeys.Contains(V[data.X])) // 9E = IfKeyPressed
				|| (data.NN == 0xA1 && !pressedKeys.Contains(V[data.X])) // A1 = IfKeyNotPressed
			)
				PC += 2;
		}

		/// <summary>
		/// Waits for a key to be pressed by looping at the current instruction.
		/// </summary>
		void WaitForKey(OpcodeEight data)
		{
			// If we have a key pressed, store it and more on.
			if (pressedKeys.Count != 0)
				V[data.X] = pressedKeys.First();
			else
				// Otherwise, wind the PC back so we will keep executing this instruction.
				PC -= 2;
		}

		/// <summary>
		/// Sets V[x] to equal the Delay register.
		/// </summary>
		void SetXToDelay(OpcodeEight data)
		{
			V[data.X] = Delay;
		}

		/// <summary>
		/// Sets the delay register to V[x].
		/// </summary>
		void SetDelay(OpcodeEight data)
		{
			Delay = V[data.X];
		}

		/// <summary>
		/// Play sound for V[x] 60ths of a second.
		/// </summary>
		void SetSound(OpcodeEight data)
		{
			beep((int)(V[data.X] * (1000f / 60)));
		}

		/// <summary>
		/// Adds V[x] to register I.
		/// </summary>
		void AddXToI(OpcodeEight data)
		{
			I += V[data.X];
		}

		/// <summary>
		/// Sets I to the correct location of the font sprite V[x].
		/// Each font sprite is 5 bytes long.
		/// </summary>
		void SetIForChar(OpcodeEight data)
		{
			//Debug.WriteLine(string.Format("Setting I to {0} to render a {1}", V[data.X] * 5, V[data.X].ToString("X")));
			I = (ushort)(V[data.X] * 5); // 0 is at 0x0, 1 is at 0x5, ...
		}

		/// <summary>
		/// Takes the decimal representation of V[x] and puts each character into memory locations
		/// starting at I (with a maximum of 3).
		/// </summary>
		void BinaryCodedDecimal(OpcodeEight data)
		{
			RAM[I + 0] = (byte)((V[data.X] / 100) % 10);
			RAM[I + 1] = (byte)((V[data.X] / 10) % 10);
			RAM[I + 2] = (byte)(V[data.X] % 10);
		}

		/// <summary>
		/// Saves all registers to the address in register I.
		/// </summary>
		void SaveX(OpcodeEight data)
		{
			for (var i = 0; i <= data.X; i++)
				RAM[I + i] = V[i];
		}

		/// <summary>
		/// Loads all registers from the address in register I.
		/// </summary>
		void LoadX(OpcodeEight data)
		{
			for (var i = 0; i <= data.X; i++)
				V[i] = RAM[I + i];
		}

		/// <summary>
		/// Pushes a 16-bit value onto the stack, incrementing the SP.
		/// </summary>
		void Push(ushort value)
		{
			Stack[SP++] = value;
		}

		/// <summary>
		/// Retrieves a 16-bit value from the stack, decrementing the SP.
		/// </summary>
		ushort Pop()
		{
			return Stack[--SP];
		}

		public int GetScreenWidth() => ScreenWidth;
        public int GetScreenHeight() => ScreenHeight;

       

    }
}
